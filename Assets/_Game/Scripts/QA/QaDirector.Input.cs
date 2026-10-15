using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using DarkestBasement.Audio;
using DarkestBasement.Combat;
using DarkestBasement.Core;
using DarkestBasement.Model.Combat;
using DarkestBasement.Model.Core;
using DarkestBasement.Model.Data;
using DarkestBasement.Model.Map;
using DarkestBasement.Model.Sim;
using DarkestBasement.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;

namespace DarkestBasement.QA
{
    // Scriptul QA "input" (-qa-script input): joacă jocul prin mouse și tastatură VIRTUALE (VirtualInput),
    // pe drumul pe care îl folosește un om — hover, drag & drop, click pe carte și apoi pe țintă, tastele 1–9,
    // Space, F, M, Esc, butoanele ecranelor. Nu cheamă funcțiile jocului direct; doar citește starea ca să verifice
    // că ecranul a reacționat. Fiecare pas se scrie PASS/FAIL în qa-log.txt și în tabelul din qa-summary.txt.
    //
    // FAIL = reacția așteptată n-a venit (în timpul dat), o excepție, sau un blocaj. Deciziile de joc (ce carte,
    // pe cine) le ia o euristică simplă peste model; execuția trece mereu prin input.
    public sealed partial class QaDirector
    {
        bool _manual;
        VirtualInput _input;
        readonly List<string> _stepOrder = new List<string>();
        readonly Dictionary<string, StepResult> _stepResults = new Dictionary<string, StepResult>();
        readonly List<string> _sounds = new List<string>();
        bool _waitOk;

        sealed class StepResult { public int Pass, Fail; public string Detail; }

        // Pașii pe care scriptul TREBUIE să-i atingă; unul neatins la final = FAIL „not reached”.
        static readonly string[] RequiredSteps =
        {
            "virtual_devices", "coords_1920x1080", "menu_button_hover", "seed_field_focus", "seed_type", "new_run_press", "new_run_click",
            "map_open", "map_key_m", "map_hover_rooms", "map_click_room",
            "walk_hold_w", "walk_release_stops", "walk_hold_lmb", "map_readonly_key_m",
            "card_hover_lift", "card_keyword_tooltip", "enemy_hover_tooltip", "intent_hover_tooltip", "status_hover_tooltip",
            "turn_order_hover", "key_f_fast",
            "play_drag_to_enemy", "play_click_card_then_target", "play_key_digit_then_target", "move_button",
            "end_turn_space", "end_turn_button", "combat_won", "reward_take",
            "bedroom_study", "bedroom_hero_tab", "bedroom_upgrade_preview", "bedroom_pick",
            "pause_esc", "pause_blocks_input", "pause_resume_click", "pause_esc_again", "abandon_to_menu_clean",
        };

        // Starea scriptului.
        bool _mapKeyTested, _walkTested, _readonlyMapTested, _batteryDone, _statusHoverDone, _cantPlayDone, _moveDone;
        bool _bedroomDone, _treasureDone, _wrapUp, _rewardTaken, _skipTested, _endButtonTested, _abandoned;
        int _playMethod, _endTurns, _combatsWon, _rooms, _mapsSinceBedroom;
        string _turnKey;
        int _turnFails;

        // ---------- pornirea ----------

        IEnumerator DriveInput()
        {
            AudioManager.Played += OnSound;
            CombatController.Moment += OnCombatMoment;
            StartCoroutine(FpsNotes());

            yield return Until(() => _flow.Prompt == FlowPrompt.MainMenu);
            yield return Seconds(1.6f);   // meniul apare cu fade (~1,1 s)

            _input = VirtualInput.Create();
            Note("dispozitive virtuale: " + _input.Describe());
            yield return CheckDevices();
            yield return MenuSteps();

            float started = Time.realtimeSinceStartup;
            while (!_finishing && !_abandoned)
            {
                // Plasă de timp: dacă dormitorul nu apare, închidem oricum cu testul de pauză.
                if (!_wrapUp && Time.realtimeSinceStartup - started > _timeout * 0.7f)
                {
                    Note("timpul scriptului e pe terminate: trec la pauză + abandon");
                    _wrapUp = true;
                }
                yield return Tick();
                // Fiecare pas consumă cel puțin un frame (altfel un pas care nu așteaptă nimic ar bloca jocul).
                yield return null;
            }
            if (_abandoned) yield return AfterAbandon();
            _input?.ReleaseAll();
            yield return Seconds(0.5f);
            Finish(_abandoned ? "input script done" : "input script stopped");
        }

        // Un pas din bucla principală: ce așteaptă jocul de la jucător chiar acum?
        IEnumerator Tick()
        {
            var prompt = _flow.Prompt;
            // Pe hol, W stă apăsat cât timp jocul vrea mers (testele de mers îl apasă singure).
            bool wantWalk = prompt == FlowPrompt.Walk && _walkTested;
            if (wantWalk != _input.IsHeld(Key.W)) _input.SetKey(Key.W, wantWalk);

            switch (prompt)
            {
                case FlowPrompt.ChooseRoom: yield return MapTurn(); yield break;
                case FlowPrompt.Walk: if (!_walkTested) yield return WalkTests(); yield break;
                case FlowPrompt.Reward: yield return RewardTurn(); yield break;
                case FlowPrompt.Treasure: yield return TreasureTurn(); yield break;
                case FlowPrompt.Bedroom: yield return BedroomTurn(); yield break;
                case FlowPrompt.Curio: yield return CurioTurn(); yield break;
                case FlowPrompt.EndScreen: yield return EndScreenTurn(); yield break;
            }
            var c = CombatController.Active;
            if (_flow.State == FlowState.Combat && c != null && c.AcceptingInput) { yield return CombatDecision(c); yield break; }
            yield return null;
        }

        // Câte cadre pe secundă desenează player-ul (sub Xvfb, OpenGL e emulat pe procesor): timpii de reacție
        // din jurnal (ridicarea cărții etc.) se citesc raportat la asta, nu ca timpi de pe un PC cu placă video.
        IEnumerator FpsNotes()
        {
            while (true)
            {
                float start = Time.realtimeSinceStartup;
                int frames = 0;
                while (Time.realtimeSinceStartup - start < 20f) { frames++; yield return null; }
                Note("fps " + (frames / (Time.realtimeSinceStartup - start)).ToString("0.0") + " (stare " + _flow.State + ")");
            }
        }

        // ---------- jurnalul de pași ----------

        bool Check(string step, bool ok, string detail = "")
        {
            if (!_stepResults.TryGetValue(step, out var r)) { r = new StepResult(); _stepResults[step] = r; _stepOrder.Add(step); }
            if (ok) r.Pass++; else r.Fail++;
            // Păstrăm primul motiv de FAIL (cel mai util); altfel ultimul detaliu.
            if (!ok && r.Fail == 1) r.Detail = detail;
            else if (r.Fail == 0) r.Detail = detail;
            Note((ok ? "PASS " : "FAIL ") + step + (string.IsNullOrEmpty(detail) ? "" : " - " + detail));
            if (!ok) Shot("FAIL_" + step, 2);
            return ok;
        }

        int CloseSteps()
        {
            foreach (var step in RequiredSteps)
                if (!_stepResults.ContainsKey(step)) Check(step, false, "not reached");
            int failed = 0;
            foreach (var r in _stepResults.Values) if (r.Fail > 0) failed++;
            return failed;
        }

        void AppendSteps(StringBuilder sb)
        {
            sb.AppendLine("steps (PASS/FAIL, de câte ori):");
            foreach (var step in _stepOrder)
            {
                var r = _stepResults[step];
                string verdict = r.Fail > 0 ? "FAIL" : "PASS";
                sb.AppendLine("  " + verdict + "  " + step.PadRight(30) + " " + r.Pass + " ok, " + r.Fail + " fail" + (string.IsNullOrEmpty(r.Detail) ? "" : "  | " + r.Detail));
            }
        }

        // Captură explicită (cel mult `max` pe etichetă).
        void Shot(string tag, int max = 1)
        {
            string key = "in_" + tag;
            _tagCount.TryGetValue(key, out int n);
            if (n >= max || _shot >= _maxShots) return;
            _tagCount[key] = n + 1;
            _pending.Enqueue(key);
        }

        // Captură a stării de ACUM: se scrie la sfârșitul frame-ului, deci așteptăm să plece din coadă
        // înainte de următoarea acțiune (altfel poza ar arăta deja reacția la ea).
        IEnumerator Snap(string tag, int max = 1)
        {
            Shot(tag, max);
            for (int i = 0; i < 4 && _pending.Count > 0; i++) yield return null;
        }

        void OnSound(string id) { lock (_sounds) _sounds.Add(id); }

        int SoundCount(string id)
        {
            int n = 0;
            lock (_sounds) foreach (var s in _sounds) if (s == id) n++;
            return n;
        }

        void OnCombatMoment(string m)
        {
            if (m == "victory") _combatsWon++;
        }

        IEnumerator WaitFor(Func<bool> condition, float timeout)
        {
            float end = Time.unscaledTime + timeout;
            _waitOk = false;
            while (true)
            {
                if (condition()) { _waitOk = true; yield break; }
                if (Time.unscaledTime > end) yield break;
                yield return null;
            }
        }

        // ---------- coordonate ----------

        static Vector2 ScreenOf(RectTransform rt) => RectTransformUtility.WorldToScreenPoint(null, rt.TransformPoint(rt.rect.center));
        static Vector2 ScreenOfUI(Vector2 ui) => UIRoot.Instance.UIToScreen(ui);
        static Vector2 ScreenOfUnit(UnitView v) => ScreenOfUI(v.ScreenRectUI().center);
        static bool Near(Vector2 a, Vector2 b, float tolerance = 1.5f) => (a - b).sqrMagnitude <= tolerance * tolerance;
        static string Fmt(Vector2 v) => "(" + v.x.ToString("0.#") + "," + v.y.ToString("0.#") + ")";

        // Dreptunghiul unui element UI oarecare (cu scările părinților) în pixeli de referință 640×360.
        static Rect UIRectOf(RectTransform rt)
        {
            Vector2 lo = UIRoot.Instance.ScreenToUI(RectTransformUtility.WorldToScreenPoint(null, rt.TransformPoint(rt.rect.min)));
            Vector2 hi = UIRoot.Instance.ScreenToUI(RectTransformUtility.WorldToScreenPoint(null, rt.TransformPoint(rt.rect.max)));
            return Rect.MinMaxRect(Mathf.Min(lo.x, hi.x), Mathf.Min(lo.y, hi.y), Mathf.Max(lo.x, hi.x), Mathf.Max(lo.y, hi.y));
        }

        static bool OnScreen(Rect r) => r.width > 0f && r.xMin >= 0f && r.yMin >= 0f && r.xMax <= UIRoot.RefWidth && r.yMax <= UIRoot.RefHeight;

        static bool Highlighted(Button b) => b != null && b.image != null && b.image.overrideSprite != null && b.image.overrideSprite == b.spriteState.highlightedSprite;
        static bool Pressed(Button b) => b != null && b.image != null && b.image.overrideSprite != null && b.image.overrideSprite == b.spriteState.pressedSprite;

        // ---------- dispozitivele și coordonatele ----------

        IEnumerator CheckDevices()
        {
            var ui = UIRoot.Instance;
            Vector2 a = ScreenOfUI(Vector2.zero), b = ScreenOfUI(new Vector2(UIRoot.RefWidth, UIRoot.RefHeight)), mid = ScreenOfUI(new Vector2(320, 180));
            bool sizeOk = Screen.width == 1920 && Screen.height == 1080 && ui.Scale == 3;
            Check("coords_1920x1080", sizeOk && Near(a, Vector2.zero) && Near(b, new Vector2(1920, 1080)) && Near(mid, new Vector2(960, 540)),
                "screen " + Screen.width + "x" + Screen.height + " scale x" + ui.Scale + ", UI(0,0)->" + Fmt(a) + " UI(640,360)->" + Fmt(b) + " UI(320,180)->" + Fmt(mid));

            _input.Warp(mid);
            yield return null;
            yield return null;
            bool current = Mouse.current == _input.Mouse && Keyboard.current == _input.Keyboard;
            Vector2 read = Mouse.current != null ? Mouse.current.position.ReadValue() : new Vector2(-1, -1);
            Vector2 back = CombatUi.MouseUI();
            Check("virtual_devices", current && Near(read, mid) && Near(back, new Vector2(320, 180), 0.6f),
                "Mouse.current=" + (Mouse.current != null ? Mouse.current.name : "null") + " Keyboard.current=" + (Keyboard.current != null ? Keyboard.current.name : "null")
                + " position " + Fmt(read) + " -> UI " + Fmt(back));
        }

        // ---------- meniul: seed tastat + NEW RUN ----------

        IEnumerator MenuSteps()
        {
            var menu = _flow.Menu;
            if (menu == null) { Check("menu_button_hover", false, "no main menu"); yield break; }
            yield return Snap("menu");

            // Hover pe NEW RUN: sprite-ul de hover + sunetul.
            var newRun = menu.NewRunButton;
            int hoverSounds = SoundCount("ui_hover");
            yield return _input.Glide(ScreenOf((RectTransform)newRun.transform), 0.25f);
            yield return Seconds(0.15f);
            bool overUi = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
            Check("menu_button_hover", Highlighted(newRun) && overUi && SoundCount("ui_hover") > hoverSounds,
                "highlight " + Highlighted(newRun) + ", pointer over UI " + overUi + ", hover sound " + (SoundCount("ui_hover") > hoverSounds));
            yield return Snap("menu_hover_new_run");

            // Câmpul de seed: click (focus), golit cu End + Backspace, apoi cifrele seed-ului.
            var field = menu.SeedInput;
            yield return _input.Click(ScreenOf((RectTransform)field.transform));
            yield return WaitFor(() => field.isFocused, 1f);
            Check("seed_field_focus", _waitOk, "isFocused " + field.isFocused);

            string target = _seed.ToString();
            yield return TypeKey(field, KeyCode.End, Key.End, '\0');
            int len = field.text.Length;
            for (int i = 0; i <= len; i++) yield return TypeKey(field, KeyCode.Backspace, Key.Backspace, '\0');
            bool cleared = field.text.Length == 0;
            yield return Snap("seed_cleared");
            foreach (char ch in target) yield return TypeKey(field, KeyCode.None, ch == '0' ? Key.Digit0 : Key.Digit1 + (ch - '1'), ch);
            yield return null;
            Check("seed_type", cleared && field.text == target, "cleared " + cleared + ", text '" + field.text + "', expected '" + target + "'");
            yield return Snap("seed_typed");

            // NEW RUN: apăsat (sprite-ul de apăsare), eliberat (click) → run nou cu seed-ul tastat.
            yield return _input.Glide(ScreenOf((RectTransform)newRun.transform), 0.2f);
            yield return Seconds(0.1f);
            int clicks = SoundCount("ui_click");
            _input.SetButton(MouseButton.Left, true);
            yield return null;
            yield return null;
            Check("new_run_press", Pressed(newRun), "pressed sprite " + Pressed(newRun));
            yield return Snap("new_run_pressed");
            yield return Seconds(0.08f);
            _input.SetButton(MouseButton.Left, false);
            yield return WaitFor(() => _flow.Run != null, 4f);
            Check("new_run_click", _waitOk && _flow.Run.State.Seed == _seed && SoundCount("ui_click") > clicks,
                _flow.Run != null ? "run seed " + _flow.Run.State.Seed + ", click sound " + (SoundCount("ui_click") > clicks) : "run did not start");
        }

        // O tastă în câmpul de text. TMP_InputField citește evenimentele IMGUI (Event), nu Input System-ul,
        // deci îi dăm evenimentul exact cum l-ar primi de la sistemul de operare; tasta trece și prin tastatura virtuală.
        IEnumerator TypeKey(TMPro.TMP_InputField field, KeyCode code, Key key, char ch)
        {
            _input.SetKey(key, true);
            if (ch != '\0') _input.Text(ch);
            field.ProcessEvent(new Event { type = EventType.KeyDown, keyCode = code, character = ch });
            field.ForceLabelUpdate();
            yield return null;
            _input.SetKey(key, false);
            yield return Seconds(0.04f);
        }

        // ---------- harta ----------

        IEnumerator MapTurn()
        {
            yield return WaitFor(() => MapPanel.Current != null && MapPanel.Current.IsInteractive, 3f);
            if (!Check("map_open", _waitOk, "interactive map " + (MapPanel.Current != null))) { yield return Seconds(1f); yield break; }
            yield return Seconds(0.45f);   // harta se desfășoară
            var run = _flow.Run;

            if (_bedroomDone) _mapsSinceBedroom++;
            // După dormitor mai facem o cameră (de obicei o luptă: abandonul se testează în luptă); altfel închidem de aici.
            if (_wrapUp || (_bedroomDone && _rewardTaken && _mapsSinceBedroom >= 2))
            {
                yield return PauseAndAbandon(null);
                yield break;
            }

            if (!_mapKeyTested)
            {
                _mapKeyTested = true;
                yield return Snap("map");
                yield return _input.Tap(Key.M);
                yield return WaitFor(() => MapPanel.Current == null, 1f);
                bool closed = _waitOk;
                yield return Snap("map_closed_by_m");
                yield return _input.Tap(Key.M);
                yield return WaitFor(() => MapPanel.Current != null && MapPanel.Current.IsInteractive, 1f);
                Check("map_key_m", closed && _waitOk, "M closes " + closed + ", M reopens " + _waitOk);
                yield return Seconds(0.45f);
            }

            var map = MapPanel.Current;
            if (map == null) yield break;

            // Hover pe fiecare cameră în care se poate merge: informația de jos se schimbă.
            bool allOk = true;
            string detail = "";
            foreach (var room in run.AvailableDestinations)
            {
                var cell = map.CellRect(room.Id);
                if (cell == null) { allOk = false; detail += " no cell " + room.Id; continue; }
                yield return _input.Glide(ScreenOf(cell), 0.15f);
                yield return Seconds(0.12f);
                bool ok = map.HoveredRoom == room.Id && map.Info.Contains("CLICK TO TRAVEL");
                if (!ok) { allOk = false; detail += " room " + room.Id + " hovered=" + map.HoveredRoom + " info='" + map.Info + "'"; }
            }
            Check("map_hover_rooms", allOk, allOk ? run.AvailableDestinations.Count + " rooms" : detail);

            int dest = InputDestination(run);
            if (dest < 0)
            {
                Note("nicio cameră potrivită: închei cu pauză + abandon");
                yield return PauseAndAbandon(null);
                yield break;
            }
            var destCell = map.CellRect(dest);
            yield return _input.Glide(ScreenOf(destCell), 0.15f);
            yield return Seconds(0.15f);
            yield return Snap("map_hover_destination", 2);
            var r = run.Map.GetRoom(dest);
            Note("merg spre camera " + dest + " (" + (r.Revealed ? r.Type.ToString() : "?") + ")");
            yield return _input.Click(ScreenOf(destCell), 0.05f);
            yield return WaitFor(() => _flow.State == FlowState.Corridor, 3f);
            Check("map_click_room", _waitOk, "state " + _flow.State + ", prompt " + _flow.Prompt);
            if (_waitOk) _rooms++;
        }

        // Spre camerele speciale cunoscute (dormitorul întâi), altfel cea mai apropiată nevizitată; boss-ul niciodată.
        int InputDestination(Run run)
        {
            var map = run.Map;
            int current = run.State.CurrentRoom;
            var dist = map.Distances(current);
            Room goal = null;
            foreach (var r in map.Rooms)
            {
                bool wanted = r.Revealed && !r.Cleared && ((r.Type == RoomType.Bedroom && !_bedroomDone) || (r.Type == RoomType.Treasure && !_treasureDone));
                if (wanted && (goal == null || dist[r.Id] < dist[goal.Id])) goal = r;
            }
            // Fără nicio luptă câștigată încă: întâi o cameră cu monștri cunoscută (lupta e partea cea mai importantă a testului).
            if (goal == null && _combatsWon == 0)
                foreach (var r in map.Rooms)
                    if (r.Revealed && !r.Cleared && r.Type == RoomType.Combat && (goal == null || dist[r.Id] < dist[goal.Id])) goal = r;
            if (goal == null)
                foreach (var r in map.Rooms)
                    if (!r.Visited && r.Id != map.BossId && (goal == null || dist[r.Id] < dist[goal.Id])) goal = r;
            if (goal == null) return -1;
            var toGoal = map.Distances(goal.Id);
            int next = -1, best = int.MaxValue;
            foreach (var room in run.AvailableDestinations)
                if (room.Id != map.BossId && toGoal[room.Id] < best) { best = toGoal[room.Id]; next = room.Id; }
            return next;
        }

        // ---------- holul ----------

        IEnumerator WalkTests()
        {
            _walkTested = true;
            yield return Seconds(0.3f);
            float z0 = _flow.WalkerZ;
            _input.SetKey(Key.W, true);
            yield return Seconds(1.2f);
            float z1 = _flow.WalkerZ;
            yield return Snap("walk_hold_w");
            Check("walk_hold_w", z1 - z0 > 0.4f, "moved " + (z1 - z0).ToString("0.00") + " in 1.2 s");
            _input.SetKey(Key.W, false);
            yield return Seconds(0.9f);
            float z2 = _flow.WalkerZ;
            yield return Seconds(0.5f);
            float z3 = _flow.WalkerZ;
            Check("walk_release_stops", Mathf.Abs(z3 - z2) < 0.02f, "drift after release " + (z3 - z2).ToString("0.000"));

            // Click stânga ținut pe lume (nu pe UI) merge și el.
            if (_flow.Prompt == FlowPrompt.Walk)
            {
                yield return _input.Glide(ScreenOfUI(new Vector2(320, 200)), 0.15f);
                bool overUi = GameInput.PointerOverUI();
                _input.SetButton(MouseButton.Left, true);
                yield return Seconds(1.0f);
                _input.SetButton(MouseButton.Left, false);
                float z4 = _flow.WalkerZ;
                Check("walk_hold_lmb", !overUi && z4 - z3 > 0.3f, "moved " + (z4 - z3).ToString("0.00") + ", pointer over UI " + overUi);
            }

            // M pe hol: harta doar de citit, M o închide.
            if (!_readonlyMapTested && _flow.Prompt == FlowPrompt.Walk)
            {
                _readonlyMapTested = true;
                yield return _input.Tap(Key.M);
                yield return WaitFor(() => MapPanel.Current != null && !MapPanel.Current.IsInteractive, 1f);
                bool opened = _waitOk;
                yield return Seconds(0.4f);
                yield return Snap("map_readonly");
                yield return _input.Tap(Key.M);
                yield return WaitFor(() => MapPanel.Current == null, 1f);
                Check("map_readonly_key_m", opened && _waitOk, "opens " + opened + ", closes " + _waitOk);
            }
        }

        // ---------- lupta ----------

        IEnumerator CombatDecision(CombatController c)
        {
            var hand = c.Hud.Hand;
            string key = c.InputHero.Id + "@" + c.Engine.State.Round;
            if (key != _turnKey) { _turnKey = key; _turnFails = 0; }
            // Cărțile intră din pachet una câte una: așteptăm să se așeze.
            yield return WaitFor(() => HandSettled(hand), 2.5f);
            yield return Seconds(0.25f);
            if (CombatController.Active != c || !c.AcceptingInput) yield break;

            if (_wrapUp || (_bedroomDone && _rewardTaken))
            {
                yield return PauseAndAbandon(c);
                yield break;
            }
            if (!_batteryDone) { _batteryDone = true; yield return CombatBattery(c); }
            if (!_statusHoverDone) yield return TryStatusHover(c);
            if (!_cantPlayDone) yield return TryCantPlayHover(c);
            if (!_stepResults.ContainsKey("card_keyword_tooltip")) yield return TryKeywordHover(c);
            if (CombatController.Active != c || !c.AcceptingInput) yield break;

            if (c.Hopeless())
            {
                var giveUp = c.Hud.GiveUpButton;
                yield return _input.Click(ScreenOf((RectTransform)giveUp.transform));
                yield return WaitFor(() => CombatController.Active != c || !c.AcceptingInput, 1f);
                Check("give_up_button", _waitOk);
                yield break;
            }

            // Prea multe încercări ratate în aceeași tură: terminăm tura (FAIL-urile sunt deja în jurnal).
            if (_turnFails >= 3) { yield return EndTurnByInput(c); yield break; }

            if (!_moveDone && PlanMove(c, out int direction)) { yield return DoMove(c, direction); yield break; }

            var plan = ChoosePlay(c);
            if (plan.card == null) { yield return EndTurnByInput(c); yield break; }
            yield return PlayByInput(c, plan.card, plan.target);
        }

        static bool HandSettled(CombatHand hand)
        {
            foreach (var card in hand.Cards) if (card == null || card.Alpha < 0.99f) return false;
            return true;
        }

        // Prima tură: hover pe fiecare carte, pe un inamic, pe o intenție, apoi F.
        IEnumerator CombatBattery(CombatController c)
        {
            var hand = c.Hud.Hand;
            yield return Snap("combat_first_turn");
            bool allLift = true;
            string detail = "";
            int index = 0;
            foreach (var card in new List<CardView>(hand.Cards))
            {
                if (card == null) continue;
                index++;
                int sounds = SoundCount("ui_hover");
                yield return _input.Glide(ScreenOf(card.Rect), 0.18f);
                float arrived = Time.unscaledTime;
                // Vecinii se dau la o parte când cartea de dinainte e ridicată: ochim din nou centrul ei.
                yield return Seconds(0.05f);
                if (card != null) _input.Warp(ScreenOf(card.Rect));
                float liftTime = -1f;
                int frames = 0;
                while (card != null && Time.unscaledTime - arrived < 0.5f)
                {
                    if (liftTime < 0f && card.Rect.anchoredPosition.y >= 56f) liftTime = Time.unscaledTime - arrived;
                    frames++;
                    yield return null;
                }
                if (card == null) { allLift = false; detail += " card " + index + " vanished"; continue; }
                bool onTop = true;
                foreach (var other in hand.Cards)
                    if (other != null && other != card && other.transform.GetSiblingIndex() > card.transform.GetSiblingIndex()) onTop = false;
                bool ok = card.IsHovered && card.Rect.anchoredPosition.y >= 56f && card.Rect.localScale.x >= 1.1f && onTop && SoundCount("ui_hover") > sounds;
                if (!card.Playable)
                {
                    bool tip = CombatTooltip.IsShown && CombatTooltip.Title == "CAN'T PLAY" && OnScreen(CombatTooltip.UIRect);
                    Check("card_cant_play_tooltip", tip, "'" + card.Def.Name + "': " + card.Reason);
                    _cantPlayDone = true;
                    ok &= tip;
                }
                else if (HasKeyword(card.Def))
                {
                    // Cuvintele-cheie (Mark, Block...) explicate lângă carte, fără să acopere cartea ridicată.
                    Rect tipRect = CombatTooltip.UIRect, cardRect = CardRectUI(card);
                    bool tip = CombatTooltip.IsShown && ReferenceEquals(CombatTooltip.Owner, card) && (CombatTooltip.Title ?? "").StartsWith("KEYWORD")
                               && OnScreen(tipRect) && !tipRect.Overlaps(cardRect);
                    Check("card_keyword_tooltip", tip, "'" + card.Def.Name + "' tooltip '" + CombatTooltip.Title + "' rect " + tipRect + " card " + cardRect);
                }
                detail += " " + card.Def.Id + (ok ? "" : "[hovered=" + card.IsHovered + " y=" + card.Rect.anchoredPosition.y + " scale=" + card.Rect.localScale.x.ToString("0.00") + "]")
                          + " lift " + (liftTime < 0f ? "never" : (liftTime * 1000f).ToString("0") + "ms") + " @" + (frames / 0.5f).ToString("0") + "fps";
                allLift &= ok;
                yield return Snap("card_hover_" + index);
            }
            Check("card_hover_lift", allLift && index > 0, detail.Trim());

            // Hover pe un inamic: conturul + tooltip cu HP și intenția (și săgețile spre ținte).
            var enemy = FirstEnemy(c);
            if (enemy != null)
            {
                yield return _input.Glide(ScreenOfUnit(enemy), 0.2f);
                yield return Seconds(0.2f);
                var intent = c.Hud.Intent(enemy.Id);
                bool tip = CombatTooltip.IsShown && ReferenceEquals(CombatTooltip.Owner, enemy) && OnScreen(CombatTooltip.UIRect);
                Check("enemy_hover_tooltip", tip && (intent == null || !intent.HasIntent || intent.ArrowsShown),
                    "tooltip " + CombatTooltip.IsShown + " '" + CombatTooltip.Title + "' on screen " + OnScreen(CombatTooltip.UIRect) + ", arrows " + (intent != null && intent.ArrowsShown));
                yield return Snap("enemy_hover");

                // Hover pe intenție (iconița de deasupra capului).
                var icon = intent != null ? intent.ActiveIcon : null;
                if (icon != null)
                {
                    yield return _input.Glide(ScreenOf(icon), 0.15f);
                    yield return Seconds(0.2f);
                    bool itip = CombatTooltip.IsShown && ReferenceEquals(CombatTooltip.Owner, intent) && OnScreen(CombatTooltip.UIRect);
                    Check("intent_hover_tooltip", itip && intent.ArrowsShown,
                        "tooltip " + CombatTooltip.IsShown + " '" + CombatTooltip.Title + "' owner " + (CombatTooltip.Owner != null ? CombatTooltip.Owner.GetType().Name : "null") + ", arrows " + intent.ArrowsShown);
                    yield return Snap("intent_hover");
                }
            }

            // Portretul unui inamic din bara de inițiativă: tooltip + conturul unității pe scenă.
            if (enemy != null)
            {
                var slot = c.Hud.TurnOrder.SlotRect(enemy.Id);
                if (slot != null)
                {
                    yield return _input.Glide(ScreenOf(slot), 0.2f);
                    yield return Seconds(0.2f);
                    bool tip = CombatTooltip.IsShown && ReferenceEquals(CombatTooltip.Owner, c.Hud.TurnOrder) && OnScreen(CombatTooltip.UIRect);
                    Check("turn_order_hover", tip && enemy.IsHovered, "tooltip " + CombatTooltip.IsShown + " '" + CombatTooltip.Title + "', unit outlined " + enemy.IsHovered);
                    yield return Snap("turn_order_hover");
                }
            }

            // F: viteza ×2 (și eticheta „>> x2”).
            bool before = CombatController.Fast;
            yield return _input.Tap(Key.F);
            yield return WaitFor(() => CombatController.Fast != before, 0.6f);
            Check("key_f_fast", _waitOk && c.Hud.SpeedLabelShown == CombatController.Fast && Mathf.Approximately(FX.HitStop.BaseTimeScale, CombatController.Fast ? 2f : 1f),
                "fast " + CombatController.Fast + ", label " + c.Hud.SpeedLabelShown + ", base time scale " + FX.HitStop.BaseTimeScale);
            yield return Snap("key_f");
            // Rămânem pe ×2: așa joacă un om nerăbdător (și testul merge mai repede).
            if (!CombatController.Fast) yield return _input.Tap(Key.F);
        }

        static bool HasKeyword(CardDef def)
        {
            foreach (var fx in def.Effects)
                if (fx.Kind == EffectKind.ApplyStatus || fx.Kind == EffectKind.Block || fx.Kind == EffectKind.GainStrength || fx.Kind == EffectKind.Taunt) return true;
            return false;
        }

        // Dreptunghiul unei cărți din mână în pixeli de referință (pivotul e la mijloc).
        static Rect CardRectUI(CardView card)
        {
            Vector2 size = new Vector2(CardView.Width, CardView.Height) * card.Rect.localScale.x;
            return new Rect(card.Rect.anchoredPosition - size / 2f, size);
        }

        static UnitView FirstEnemy(CombatController c)
        {
            UnitView best = null;
            foreach (var v in c.Views.Values)
            {
                if (v == null || v.IsHero || v.Dead) continue;
                var u = c.ModelUnit(v.Id);
                if (u == null || !u.IsAlive) continue;
                if (best == null || u.Rank < c.ModelUnit(best.Id).Rank) best = v;
            }
            return best;
        }

        IEnumerator TryStatusHover(CombatController c)
        {
            foreach (var v in c.Views.Values)
            {
                if (v == null || v.Dead) continue;
                var hud = c.Hud.UnitHud(v.Id);
                var icon = hud != null ? hud.VisibleStatusIcon() : null;
                if (icon == null) continue;
                _statusHoverDone = true;
                yield return _input.Glide(ScreenOf(icon), 0.15f);
                yield return Seconds(0.2f);
                bool tip = CombatTooltip.IsShown && ReferenceEquals(CombatTooltip.Owner, hud) && OnScreen(CombatTooltip.UIRect);
                Check("status_hover_tooltip", tip, v.Id + ": '" + CombatTooltip.Title + "' shown " + CombatTooltip.IsShown + " on screen " + OnScreen(CombatTooltip.UIRect));
                yield return Snap("status_hover");
                yield break;
            }
        }

        IEnumerator TryKeywordHover(CombatController c)
        {
            foreach (var card in c.Hud.Hand.Cards)
            {
                if (card == null || !card.Playable || !HasKeyword(card.Def)) continue;
                yield return _input.Glide(ScreenOf(card.Rect), 0.15f);
                yield return Seconds(0.25f);
                Rect tipRect = CombatTooltip.UIRect, cardRect = CardRectUI(card);
                bool tip = CombatTooltip.IsShown && ReferenceEquals(CombatTooltip.Owner, card) && (CombatTooltip.Title ?? "").StartsWith("KEYWORD")
                           && OnScreen(tipRect) && !tipRect.Overlaps(cardRect);
                Check("card_keyword_tooltip", tip, "'" + card.Def.Name + "' tooltip '" + CombatTooltip.Title + "' rect " + tipRect + " card " + cardRect);
                yield return Snap("card_keyword");
                yield break;
            }
        }

        IEnumerator TryCantPlayHover(CombatController c)
        {
            foreach (var card in c.Hud.Hand.Cards)
            {
                if (card == null || card.Playable || string.IsNullOrEmpty(card.Reason)) continue;
                _cantPlayDone = true;
                yield return _input.Glide(ScreenOf(card.Rect), 0.15f);
                yield return Seconds(0.25f);
                bool tip = CombatTooltip.IsShown && CombatTooltip.Title == "CAN'T PLAY" && OnScreen(CombatTooltip.UIRect);
                Check("card_cant_play_tooltip", tip, "'" + card.Def.Name + "': " + card.Reason);
                yield return Snap("card_cant_play");
                yield break;
            }
        }

        // ---- ce joacă (euristică simplă peste model) ----

        struct Plan { public CardView card; public Unit target; }

        Plan ChoosePlay(CombatController c)
        {
            var engine = c.Engine;
            var hero = c.InputHero;
            var plan = new Plan();
            double best = 0.5;
            foreach (var view in c.Hud.Hand.Cards)
            {
                if (view == null || view.Instance == null || !view.Playable || !view.Interactable) continue;
                if (!engine.CanPlay(hero, view.Instance, out _)) continue;
                var valid = engine.ValidTargets(hero, view.Instance);
                if (valid.Count == 0) continue;
                bool needsTarget = CombatHand.NeedsTarget(view.Def);
                foreach (var target in valid)
                {
                    double score = Score(hero, view.Instance, target, valid);
                    if (score > best) { best = score; plan.card = view; plan.target = needsTarget ? target : null; }
                    if (!needsTarget) break;
                }
            }
            return plan;
        }

        static double Score(HeroUnit hero, CardInstance card, Unit target, IReadOnlyList<Unit> valid)
        {
            var def = card.Def;
            double s = 0;
            foreach (var fx in def.Effects)
            {
                int v = def.EffectValue(fx, card.Upgraded);
                switch (fx.Kind)
                {
                    case EffectKind.Damage:
                        if (def.TargetType == TargetType.AllEnemies)
                            foreach (var t in valid) s += Math.Min(v * fx.Hits, t.Hp + t.Statuses[Status.Block]);
                        else s += Math.Min(v * fx.Hits, target.Hp + target.Statuses[Status.Block]) + (v * fx.Hits >= target.Hp ? 5 : 0) + 3.0 / Math.Max(1, target.Hp);
                        break;
                    case EffectKind.Heal: s += Math.Min(v, target != null ? target.MaxHp - target.Hp : 0); break;
                    case EffectKind.Block: s += v * 0.5; break;
                    case EffectKind.GainStrength: s += v * 2; break;
                    case EffectKind.ApplyStatus: s += 2; break;
                    case EffectKind.Draw:
                    case EffectKind.DiscardHandDraw: s += 1; break;
                    case EffectKind.GainEnergy: s += 3; break;
                    case EffectKind.SelfDamage: s -= hero.Hp <= v + 5 ? 100 : v * 0.5; break;
                }
            }
            return s / Math.Max(1, card.Cost);
        }

        // ---- cum joacă (doar prin input) ----

        IEnumerator PlayByInput(CombatController c, CardView card, Unit target)
        {
            var hand = c.Hud.Hand;
            bool targeted = CombatHand.NeedsTarget(card.Def);
            int method = PickMethod(targeted && !target.IsHero);
            string step;
            UnitView targetView = targeted ? c.View(target.Id) : null;
            if (targeted && targetView == null) { _turnFails++; yield break; }

            if (targeted && method == 0)
            {
                step = "play_drag_to_enemy";
                if (target.IsHero) step = "play_drag_to_ally";
                yield return DragCard(c, card, ScreenOfUnit(targetView), target.Id, step);
            }
            else if (method == 2)
            {
                // Tasta cu numărul cărții (1 = prima din stânga), apoi ținta (sau aceeași tastă din nou).
                step = targeted ? "play_key_digit_then_target" : "play_key_digit_twice";
                int index = IndexOf(hand, card);
                Key digit = Key.Digit1 + index;
                yield return _input.Tap(digit);
                yield return WaitFor(() => hand.Selected == card, 0.5f);
                bool selected = _waitOk;
                yield return Snap("key_digit_selected");
                if (targeted) yield return _input.Click(ScreenOfUnit(targetView), 0.2f);
                else yield return _input.Tap(digit);
                yield return Accepted(c, step, "key " + (index + 1) + " selects " + selected + ", card '" + card.Def.Name + "'" + (targeted ? " -> " + target.Name : ""));
            }
            else if (targeted)
            {
                step = "play_click_card_then_target";
                yield return _input.Click(ScreenOf(card.Rect), 0.18f);
                yield return WaitFor(() => hand.Selected == card, 0.5f);
                bool selected = _waitOk;
                yield return Seconds(0.1f);
                yield return Snap("card_selected");
                // Ținta trebuie să se aprindă cât timp cartea e selectată, iar săgeata să arate spre ea.
                bool lit = hand.IsValidTarget(target.Id);
                yield return _input.Glide(ScreenOfUnit(targetView), 0.2f);
                yield return Seconds(0.1f);
                bool aim = hand.AimShown && targetView.IsHovered;
                yield return Snap("click_aim", 2);
                yield return _input.Click(ScreenOfUnit(targetView), 0.05f);
                yield return Accepted(c, step, "selected " + selected + ", target lit " + lit + ", aim arrow+outline " + aim + ", '" + card.Def.Name + "' -> " + target.Name,
                    selected && lit && aim);
            }
            else if (method == 0)
            {
                step = "play_untargeted_drag_up";
                yield return DragCard(c, card, ScreenOfUI(new Vector2(320, 190)), null, step);
            }
            else
            {
                step = "play_untargeted_click_twice";
                yield return _input.Click(ScreenOf(card.Rect), 0.18f);
                yield return WaitFor(() => hand.Selected == card, 0.5f);
                bool selected = _waitOk;
                yield return Seconds(0.1f);
                if (card != null) yield return _input.Click(ScreenOf(card.Rect), 0.05f);
                yield return Accepted(c, step, "selected " + selected + ", '" + card.Def.Name + "'");
            }
            // Mouse-ul pleacă de pe scenă, ca hover-ul să nu rămână pe o unitate.
            yield return _input.Glide(ScreenOfUI(new Vector2(320, 300)), 0.1f);
        }

        // Întâi metodele obligatorii încă neîncercate (drag, click + click, tastă + click), apoi pe rând.
        int PickMethod(bool onEnemy)
        {
            if (onEnemy)
            {
                if (!_stepResults.ContainsKey("play_drag_to_enemy")) return 0;
                if (!_stepResults.ContainsKey("play_click_card_then_target")) return 1;
                if (!_stepResults.ContainsKey("play_key_digit_then_target")) return 2;
            }
            return _playMethod++ % 3;
        }

        static int IndexOf(CombatHand hand, CardView card)
        {
            for (int i = 0; i < hand.Cards.Count; i++) if (hand.Cards[i] == card) return i;
            return 0;
        }

        // Drag & drop: apasă pe carte, o trage pe un arc până la țintă, cu verificări la mijlocul drumului.
        IEnumerator DragCard(CombatController c, CardView card, Vector2 to, string targetId, string step)
        {
            var hand = c.Hud.Hand;
            yield return _input.Glide(ScreenOf(card.Rect), 0.18f);
            yield return Seconds(0.12f);
            _input.SetButton(MouseButton.Left, true);
            yield return null;
            yield return null;
            Vector2 from = _input.Position;
            bool dragging = false, lit = targetId == null, aim = targetId == null, visible = targetId == null;
            const int steps = 24;
            for (int i = 1; i <= steps; i++)
            {
                float k = i / (float)steps;
                // Un arc ușor în sus, ca o mână adevărată.
                Vector2 p = Vector2.Lerp(from, to, Mathf.SmoothStep(0f, 1f, k)) + Vector2.up * Mathf.Sin(k * Mathf.PI) * 60f;
                _input.Warp(p);
                yield return null;
                if (hand.IsDragging) dragging = true;
                if (targetId != null && hand.IsValidTarget(targetId)) lit = true;
                if (i == steps / 2) Shot("drag_mid", 2);
            }
            yield return Seconds(0.15f);
            if (targetId != null)
            {
                // Peste țintă: săgeata de ochire e pe ecran, iar cartea nu acoperă ținta.
                aim = hand.AimShown;
                var tv = c.View(targetId);
                visible = tv != null && !CardRectUI(card).Contains(tv.ScreenRectUI().center) && tv.IsHovered;
            }
            yield return Snap("drag_over_target", 2);
            _input.SetButton(MouseButton.Left, false);
            yield return null;
            yield return Accepted(c, step, "dragging " + dragging + ", target lit " + lit + ", aim arrow " + aim + ", target visible+outlined " + visible + ", '" + card.Def.Name + "'",
                dragging && lit && aim && visible);
        }

        // Comanda a fost primită: controller-ul nu mai așteaptă input (redă animațiile).
        IEnumerator Accepted(CombatController c, string step, string detail, bool extra = true)
        {
            yield return WaitFor(() => CombatController.Active != c || !c.AcceptingInput, 1f);
            if (!Check(step, _waitOk && extra, "accepted " + _waitOk + ", " + detail) && !_waitOk)
            {
                _turnFails++;
                // Curățenie: click dreapta deselectează, ca următoarea încercare să pornească de la zero.
                yield return _input.Click(ScreenOfUI(new Vector2(320, 300)), 0.1f, MouseButton.Right);
            }
        }

        bool PlanMove(CombatController c, out int direction)
        {
            direction = 0;
            var hero = c.InputHero;
            if (hero == null || hero.Energy < 1) return false;
            // Schimbul dintre rangurile 2 și 3 (Cain ↔ Isaac la început) nu strică nimănui cărțile.
            if (hero.Rank == 2) direction = +1;
            else if (hero.Rank == 3) direction = -1;
            else return false;
            return c.CanMove(direction);
        }

        IEnumerator DoMove(CombatController c, int direction)
        {
            _moveDone = true;
            var hero = c.InputHero;
            int rankBefore = hero.Rank;
            var button = c.Hud.MoveButton(direction);
            int sounds = SoundCount("ui_hover");
            yield return _input.Glide(ScreenOf((RectTransform)button.transform), 0.2f);
            yield return Seconds(0.2f);
            bool tip = CombatTooltip.IsShown && (CombatTooltip.Title ?? "").StartsWith("MOVE") && OnScreen(CombatTooltip.UIRect);
            bool hl = Highlighted(button), sound = SoundCount("ui_hover") > sounds;
            yield return Snap("move_hover");
            yield return _input.Click(ScreenOf((RectTransform)button.transform), 0.02f);
            yield return WaitFor(() => CombatController.Active != c || !c.AcceptingInput, 1f);
            bool accepted = _waitOk;
            yield return WaitFor(() => CombatController.Active != c || c.AcceptingInput, 4f);
            yield return null;
            yield return null;
            // Mouse-ul a rămas unde era butonul; butoanele s-au mutat cu eroul, deci tooltip-ul Move trebuie să fi dispărut.
            bool stale = CombatTooltip.IsShown && (CombatTooltip.Title ?? "").StartsWith("MOVE");
            Check("move_button", accepted && hero.Rank == rankBefore + direction && tip && hl && sound && !stale,
                hero.Name + " rank " + rankBefore + " -> " + hero.Rank + ", tooltip " + tip + ", highlight " + hl + ", hover sound " + sound + ", stale tooltip after move " + stale);
            yield return Snap("move_done");
        }

        IEnumerator EndTurnByInput(CombatController c)
        {
            var hand = c.Hud.Hand;
            _endTurns++;
            bool useButton = !_endButtonTested && _endTurns >= 2;
            if (useButton)
            {
                _endButtonTested = true;
                var button = c.Hud.EndTurnButton;
                int sounds = SoundCount("ui_hover");
                yield return _input.Glide(ScreenOf((RectTransform)button.transform), 0.2f);
                yield return Seconds(0.15f);
                bool hl = Highlighted(button), sound = SoundCount("ui_hover") > sounds;
                bool tip = CombatTooltip.IsShown && CombatTooltip.Title == "END TURN" && OnScreen(CombatTooltip.UIRect);
                yield return Snap("end_turn_hover");
                yield return _input.Click(ScreenOf((RectTransform)button.transform), 0.02f);
                yield return WaitFor(() => CombatController.Active != c || !c.AcceptingInput, 1f);
                Check("end_turn_button", _waitOk && hand.Count == 0, "highlight " + hl + ", hover sound " + sound + ", tooltip " + tip + ", hand left " + hand.Count);
                if (!_waitOk) _turnFails++;
                yield break;
            }
            yield return _input.Tap(Key.Space);
            yield return WaitFor(() => CombatController.Active != c || !c.AcceptingInput, 1f);
            Check("end_turn_space", _waitOk && hand.Count == 0, "accepted " + _waitOk + ", hand left " + hand.Count);
            if (!_waitOk) _turnFails++;
        }

        // ---------- recompensa ----------

        IEnumerator RewardTurn()
        {
            if (_combatsWon > 0 && !_stepResults.ContainsKey("combat_won")) Check("combat_won", true, "victories " + _combatsWon);
            yield return WaitFor(() => RewardScreen.TakeButton != null || _flow.Prompt != FlowPrompt.Reward, 8f);
            if (_flow.Prompt != FlowPrompt.Reward) yield break;
            if (!_waitOk) { Check("reward_take", false, "no Take button"); yield break; }
            yield return Seconds(0.2f);
            yield return Snap("reward", 2);
            var run = _flow.Run;
            int taken = run.State.Stats.CardsTaken;
            // A doua recompensă o sărim (Skip trebuie să meargă și el).
            bool skip = _rewardTaken && !_skipTested;
            var button = skip ? RewardScreen.SkipButton : RewardScreen.TakeButton;
            int sounds = SoundCount("ui_hover");
            yield return _input.Glide(ScreenOf((RectTransform)button.transform), 0.25f);
            yield return Seconds(0.15f);
            bool hl = Highlighted(button), sound = SoundCount("ui_hover") > sounds;
            yield return Snap(skip ? "reward_skip_hover" : "reward_take_hover");
            yield return _input.Click(ScreenOf((RectTransform)button.transform), 0.02f);
            yield return WaitFor(() => _flow.Prompt != FlowPrompt.Reward, 4f);
            if (skip)
            {
                _skipTested = true;
                Check("reward_skip", _waitOk && run.State.Stats.CardsTaken == taken, "highlight " + hl + ", hover sound " + sound);
            }
            else
            {
                _rewardTaken = true;
                Check("reward_take", _waitOk && run.State.Stats.CardsTaken == taken + 1, "highlight " + hl + ", hover sound " + sound + ", cards taken " + taken + " -> " + run.State.Stats.CardsTaken);
            }
            yield return Seconds(0.3f);
            yield return Snap("after_reward");
        }

        // ---------- dormitorul ----------

        IEnumerator BedroomTurn()
        {
            yield return Seconds(0.6f);
            var bed = _flow.Bedroom;
            if (bed == null) yield break;
            var run = _flow.Run;
            yield return Snap("bedroom");
            var study = bed.OptionPanel(BedroomAction.UpgradeCard);
            yield return _input.Glide(ScreenOf(study), 0.25f);
            yield return Seconds(0.15f);
            yield return Snap("bedroom_hover_study");
            yield return _input.Click(ScreenOf(study), 0.02f);
            yield return WaitFor(() => bed.PickerOpen, 1f);
            Check("bedroom_study", _waitOk, "picker open " + bed.PickerOpen);
            if (!_waitOk) yield break;

            // Tab-ul altui erou (al doilea erou viu).
            string first = bed.PickerHero, other = null;
            foreach (var h in run.State.Party) if (h.IsAlive && h.Def.Id != first) { other = h.Def.Id; break; }
            if (other != null)
            {
                var tab = bed.HeroTab(other);
                yield return _input.Click(ScreenOf(tab), 0.2f);
                yield return WaitFor(() => bed.PickerHero == other, 1f);
                Check("bedroom_hero_tab", _waitOk, first + " -> " + bed.PickerHero);
                yield return Seconds(0.2f);
            }

            // Hover pe o carte: tooltip-ul arată cum va fi după upgrade; apoi click.
            PartyHero hero = null;
            foreach (var h in run.State.Party) if (h.Def.Id == bed.PickerHero) hero = h;
            int pick = -1;
            for (int i = 0; hero != null && i < hero.Deck.Count; i++) if (bed.PickerCard(i) != null) { pick = i; break; }
            if (pick < 0) { Check("bedroom_upgrade_preview", false, "no upgradable card"); yield break; }
            var view = bed.PickerCard(pick);
            var card = hero.Deck[pick];
            yield return _input.Glide(ScreenOf(view.Rect), 0.25f);
            yield return Seconds(0.25f);
            // Cartea „după upgrade” apare lângă cea de sub mouse, întreagă pe ecran.
            var after = bed.UpgradePreview;
            Rect afterRect = after != null ? UIRectOf(after.Rect) : default;
            bool preview = after != null && after.Def == card.Def && after.Upgraded && OnScreen(afterRect);
            Check("bedroom_upgrade_preview", preview && view.IsHovered, "preview " + (after != null ? after.Def.Id + (after.Upgraded ? "+" : "") : "none") + " rect " + afterRect + ", hovered " + view.IsHovered);
            yield return Snap("bedroom_upgrade_preview");
            yield return _input.Click(ScreenOf(view.Rect), 0.02f);
            yield return WaitFor(() => _flow.Prompt != FlowPrompt.Bedroom, 2f);
            Check("bedroom_pick", _waitOk && card.Upgraded, card.Def.Id + " upgraded " + card.Upgraded);
            _bedroomDone = true;
            yield return Seconds(0.4f);
            yield return Snap("bedroom_after");
        }

        // ---------- comoara ----------

        IEnumerator TreasureTurn()
        {
            yield return Seconds(1.1f);   // cărțile intră de jos
            var t = _flow.Treasure;
            if (t == null) yield break;
            var run = _flow.Run;
            bool allTips = true;
            string detail = "";
            for (int i = 0; i < run.TreasureChoices.Count; i++)
            {
                var view = t.ChoiceCard(i);
                if (view == null) continue;
                yield return _input.Glide(ScreenOf(view.Rect), 0.2f);
                yield return Seconds(0.2f);
                // Cartea de sub mouse se ridică și se aprinde; e întreagă pe ecran (sunt ×2) și sună la hover.
                Rect r = UIRectOf(view.Rect);
                bool ok = view.IsHovered && OnScreen(r);
                if (!ok) detail += " card " + i + " hovered " + view.IsHovered + " rect " + r;
                allTips &= ok;
                yield return Snap("treasure_hover_" + i);
            }
            Check("treasure_hover", allTips, detail.Trim());
            int best = 0; double value = double.MinValue;
            var ai = new AutoPlayer();
            for (int i = 0; i < run.TreasureChoices.Count; i++) { double v = ai.CardValue(run.TreasureChoices[i]); if (v > value) { value = v; best = i; } }
            var chosen = run.TreasureChoices[best];
            int deckBefore = DeckOf(run, chosen.HeroId);
            yield return _input.Click(ScreenOf(t.ChoiceCard(best).Rect), 0.15f);
            yield return WaitFor(() => _flow.Prompt != FlowPrompt.Treasure, 3f);
            Check("treasure_pick", _waitOk && DeckOf(run, chosen.HeroId) == deckBefore + 1, chosen.Id);
            _treasureDone = true;
        }

        static int DeckOf(Run run, string heroId)
        {
            foreach (var h in run.State.Party) if (h.Def.Id == heroId) return h.Deck.Count;
            return -1;
        }

        // ---------- curio și finalul ----------

        IEnumerator CurioTurn()
        {
            yield return Seconds(0.5f);
            var prompt = _flow.CurioScreen;
            if (prompt == null) yield break;
            yield return Snap("curio");
            yield return _input.Click(ScreenOf((RectTransform)prompt.SearchButton.transform), 0.25f);
            yield return WaitFor(() => _flow.Prompt != FlowPrompt.Curio, 2f);
            Check("curio_search_click", _waitOk);
        }

        // Echipa a murit înainte de final: verificăm măcar ecranul de final (butonul MAIN MENU).
        IEnumerator EndScreenTurn()
        {
            yield return Seconds(1.5f);
            yield return Snap("end_screen");
            Check("party_alive", false, "the party died: " + _flow.State);
            _wrapUp = true;
            Finish("party died");
        }

        // ---------- pauza și abandonul ----------

        IEnumerator PauseAndAbandon(CombatController c)
        {
            _wrapUp = true;
            yield return _input.Glide(ScreenOfUI(new Vector2(320, 300)), 0.1f);
            yield return _input.Tap(Key.Escape);
            yield return WaitFor(() => GameFlow.Paused && _flow.PauseScreen != null, 1f);
            Check("pause_esc", _waitOk && Time.timeScale == 0f, "paused " + GameFlow.Paused + ", timeScale " + Time.timeScale);
            yield return Seconds(0.3f);
            yield return Snap("pause");

            // Cât e pauză, jocul nu ascultă: Space nu termină tura, M nu deschide harta, W nu mișcă.
            bool mapBefore = MapPanel.Current != null;
            yield return _input.Tap(Key.Space);
            yield return _input.Tap(Key.M);
            yield return Seconds(0.2f);
            bool blocked = GameFlow.Paused && (MapPanel.Current != null) == mapBefore && (c == null || (CombatController.Active == c && c.AcceptingInput));
            Check("pause_blocks_input", blocked, "still paused " + GameFlow.Paused + ", map unchanged " + ((MapPanel.Current != null) == mapBefore)
                + (c != null ? ", combat still waiting " + (CombatController.Active == c && c.AcceptingInput) : ""));

            var pause = _flow.PauseScreen;
            if (pause == null) yield break;
            int sounds = SoundCount("ui_hover");
            yield return _input.Glide(ScreenOf((RectTransform)pause.ResumeButton.transform), 0.25f);
            yield return Seconds(0.15f);
            bool hl = Highlighted(pause.ResumeButton), sound = SoundCount("ui_hover") > sounds;
            yield return Snap("pause_hover_resume");
            yield return _input.Click(ScreenOf((RectTransform)pause.ResumeButton.transform), 0.02f);
            yield return WaitFor(() => !GameFlow.Paused, 1f);
            yield return null;
            Check("pause_resume_click", _waitOk && Time.timeScale > 0f && _flow.PauseScreen == null,
                "highlight " + hl + ", hover sound " + sound + ", timeScale " + Time.timeScale);

            yield return Seconds(0.4f);
            yield return _input.Tap(Key.Escape);
            yield return WaitFor(() => GameFlow.Paused && _flow.PauseScreen != null, 1f);
            Check("pause_esc_again", _waitOk);
            pause = _flow.PauseScreen;
            if (pause == null) yield break;
            yield return Seconds(0.3f);
            yield return _input.Click(ScreenOf((RectTransform)pause.AbandonButton.transform), 0.25f);
            _abandoned = true;
        }

        IEnumerator AfterAbandon()
        {
            yield return WaitFor(() => _flow.Prompt == FlowPrompt.MainMenu && _flow.Menu != null, 8f);
            bool menu = _waitOk;
            yield return Seconds(1.4f);   // meniul apare cu fade
            var problems = new List<string>();
            if (!menu) problems.Add("not on menu (state " + _flow.State + ", prompt " + _flow.Prompt + ")");
            if (_flow.Run != null) problems.Add("run still set");
            if (GameFlow.Paused) problems.Add("still paused");
            if (!Mathf.Approximately(Time.timeScale, 1f)) problems.Add("timeScale " + Time.timeScale);
            if (CombatController.Active != null) problems.Add("combat controller alive");
            if (FindObjectsByType<UnitView>(FindObjectsSortMode.None).Length > 0) problems.Add("unit views left");
            if (MapPanel.Current != null) problems.Add("map open");
            if (RewardScreen.IsOpen) problems.Add("reward open");
            if (CombatTooltip.IsShown) problems.Add("combat tooltip shown");
            if (Tooltip.IsShown) problems.Add("tooltip shown");
            string[] bad = { "CombatHud", "CombatWorld", "CombatOverlay", "RewardScreen", "MapPanel", "PauseMenu", "BedroomScreen", "TreasureScreen", "CurioPrompt", "EndScreen", "PartyHud" };
            var ui = UIRoot.Instance;
            foreach (var layer in new[] { ui.World, ui.Hud, ui.Screens, ui.Overlay })
                foreach (Transform child in layer)
                    if (child.gameObject.activeInHierarchy && Array.IndexOf(bad, child.name) >= 0) problems.Add("leftover " + child.name);
            Check("abandon_to_menu_clean", problems.Count == 0, problems.Count == 0 ? "menu, no leftovers" : string.Join("; ", problems));
            yield return Snap("menu_after_abandon");

            // Meniul răspunde din nou la mouse.
            if (_flow.Menu != null)
            {
                var b = _flow.Menu.NewRunButton;
                yield return _input.Glide(ScreenOf((RectTransform)b.transform), 0.25f);
                yield return Seconds(0.15f);
                Check("menu_after_abandon_hover", Highlighted(b));
            }
        }
    }
}
