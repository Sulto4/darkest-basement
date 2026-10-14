using System;
using System.Collections;
using System.Collections.Generic;
using DarkestBasement.Audio;
using DarkestBasement.Combat;
using DarkestBasement.Exploration;
using DarkestBasement.FX;
using DarkestBasement.Model.Combat;
using DarkestBasement.Model.Core;
using DarkestBasement.Model.Data;
using DarkestBasement.Model.Map;
using DarkestBasement.Model.Rewards;
using DarkestBasement.Model.Sim;
using DarkestBasement.QA;
using DarkestBasement.UI;
using UnityEngine;

namespace DarkestBasement.Core
{
    // Secvențele din interiorul unui hol sau al unei camere: lupta (predată echipei C), recompensa,
    // comoara, patul, curio-urile și capcanele.
    public sealed partial class GameFlow
    {
        // ---- Lupta (contractul W ↔ C din docs/VIEW-CONTRACTS.md) ----

        IEnumerator CombatSequence(CombatStageInfo stage)
        {
            var returnState = State;
            // 1. Modelul creează lupta din encounter-ul care așteaptă (cameră sau ambuscadă).
            var engine = Run.CreateCombat(Run.PendingEncounter);
            SetState(FlowState.Combat);

            bool victory = false;
            yield return PresentCombat(engine, stage, v => victory = v);

            // 4. Rezultatul intră în model o singură dată: HP, morți, statistici, recompensa în așteptare.
            Run.ApplyCombatResult(engine);
            if (!engine.Victory || Run.IsRunOver)
            {
                Moment("defeat");
                yield return Wait(1.2f);
                yield return _ui.FadeTo(0.55f, 0.8f).Wait();
                yield break;
            }

            yield return RewardSequence();
            yield return LeaveSideView(stage);
            SetState(returnState);
        }

        // Pașii 1–3: camera trece în vedere laterală (eroii pleacă din formație spre locurile lor),
        // apoi echipa C preia scena prin CombatController.Begin și ne cheamă înapoi la final.
        IEnumerator PresentCombat(CombatEngine engine, CombatStageInfo stage, Action<bool> onDone)
        {
            AudioManager.PlayMusic(stage.IsBoss ? "music_boss" : "music_combat");
            if (_hud != null) _hud.SetVisible(false);
            if (MapPanel.Current != null) MapPanel.Current.Close();
            Hint.HideAll();
            // Recuzita de pe partea camerei dispare abia când camera trece prin perete (pe la 40% din arc):
            // la început e încă în cadru și ar "clipi" vizibil.
            Tween.Delay(0.5f, () => SetCutaway(true));

            var swing = _rig.GoToSideView(stage, 1.2f);
            if (_party != null) _party.MoveToStage(stage, 1.1f);
            ShowStageLight(stage);
            yield return swing.Wait();
            if (_party != null) _party.SetVisible(false);
            Moment("side_view");

            bool? result = null;
            float started = Time.time;
            CombatController controller = null;
            try { controller = CombatController.Begin(engine, stage, v => result = v); }
            catch (Exception e) { Debug.LogException(e); }

            while (result == null)
            {
                // Plasă de siguranță: dacă nimeni n-a pornit lupta în 8 s (CombatController e un stub sau a crăpat
                // înainte de StartCombat), sau controller-ul a dispărut fără să răspundă, rezolvăm lupta în model,
                // ca jocul să meargă mai departe. Lupta adevărată pornește după un intro de ~1 s.
                if (controller == null && engine.IsOver) { result = engine.Victory; break; }
                bool neverStarted = engine.State.Round == 0 && Time.time - started > 8f;
                bool vanished = controller == null && !engine.IsOver;
                if (neverStarted || vanished)
                {
                    Debug.LogWarning("[GameFlow] CombatController nu a condus lupta (" + (vanished ? "a dispărut" : "nu a pornit în 8 s") + "): o rezolv automat în model.");
                    if (controller != null) Destroy(controller.gameObject);
                    var placeholder = StagePlaceholder.Create(_worldRoot, engine, stage);
                    StartCoroutine(DelayedMoment("combat_placeholder", 0.6f));
                    yield return placeholder.Resolve(engine, QaHooks.Active ? 0.15f : 0.35f);
                    Destroy(placeholder.gameObject);
                    result = engine.Victory;
                    break;
                }
                yield return null;
            }
            onDone(result.Value);
        }

        // Pasul 5: eroii reapar pe scenă (cu HP-ul și morții actualizați) și se întorc în formație,
        // în timp ce camera revine în spatele lor.
        IEnumerator LeaveSideView(CombatStageInfo stage)
        {
            if (_party != null)
            {
                _party.Rebuild(Run.State);
                _party.PlaceOnStage(stage);
                _party.SetVisible(true);
                _party.MoveToFormation(0.95f);
            }
            HideStageLight();
            yield return _rig.ReturnToFollow(1.0f).Wait();
            SetCutaway(false);
            RefreshHud();
            if (_hud != null) _hud.SetVisible(true);
            AudioManager.PlayMusic("music_explore");
        }

        // O lumină caldă deasupra scenei, ca la teatru: unitățile (colorate după LocalLight) rămân lizibile
        // oriunde ar cădea lupta, chiar și pe un hol între două torțe.
        Light _stageLight;

        void ShowStageLight(CombatStageInfo stage)
        {
            HideStageLight();
            var go = new GameObject("StageLight");
            go.transform.SetParent(_worldRoot, false);
            Vector3 right = stage.Right.sqrMagnitude > 0.0001f ? stage.Right.normalized : Vector3.forward;
            Vector3 toCamera = Vector3.Cross(Vector3.up, right).normalized;
            go.transform.position = stage.Center + Vector3.up * 3.2f + toCamera * 1.2f;
            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.78f, 0.58f);
            light.range = 11f;
            light.intensity = 0f;
            light.shadows = LightShadows.None;
            LocalLight.Register(light);
            _stageLight = light;
            Tween.Value(1.2f, k => { if (light != null) light.intensity = 1.3f * k; }, Ease.InOutSine, null, 0f, false, light);
        }

        void HideStageLight()
        {
            var light = _stageLight;
            _stageLight = null;
            if (light == null) return;
            float from = light.intensity;
            Tween.Value(0.8f, k => { if (light != null) light.intensity = from * (1f - k); }, Ease.OutQuad,
                () => { if (light != null) { LocalLight.Unregister(light); Destroy(light.gameObject); } }, 0f, false, light);
        }

        // ---- Recompensa după luptă (sau din curio): RewardScreen al echipei C, apoi Take / Skip în model ----
        IEnumerator RewardSequence()
        {
            var returnState = State;
            SetState(FlowState.Reward);
            var card = Run.RollCardReward();
            bool? take = null;
            try { RewardScreen.Show(card, card.Rarity == Rarity.Rare, t => take = t); }
            catch (Exception e) { Debug.LogException(e); take = false; }
            Prompt = FlowPrompt.Reward;
            StartCoroutine(DelayedMoment("reward", 0.9f));

            float waited = 0f;
            while (take == null)
            {
                waited += Time.unscaledDeltaTime;
                // În QA, dacă ecranul nu răspunde (nu are încă AutoDecide), alege politica AutoPlayer.
                if (QaHooks.Active && waited > 15f)
                {
                    Debug.LogWarning("[GameFlow] RewardScreen nu a răspuns în 15 s; QA alege singur.");
                    take = new AutoPlayer().ShouldTake(Run.State, card);
                }
                yield return null;
            }
            Prompt = FlowPrompt.None;
            if (take.Value)
            {
                Run.TakeCard();
                RefreshHud();
                if (_hud != null) _hud.Flash(card.HeroId, Palette.Gold);
            }
            else Run.SkipCard();
            SetState(returnState);
        }

        // ---- Comoara: piedestal, alegi 1 din 3 ----
        IEnumerator TreasureSequence()
        {
            SetState(FlowState.Treasure);
            yield return Wait(0.5f);
            int chosen = -1;
            Treasure = new TreasureScreen(_ui.Screens, Run.TreasureChoices, i => chosen = i);
            Prompt = FlowPrompt.Treasure;
            StartCoroutine(DelayedMoment("treasure", 1.0f));
            while (chosen < 0) yield return null;

            Prompt = FlowPrompt.None;
            var card = Run.TreasureChoices[chosen];
            Run.ChooseTreasure(chosen);
            Treasure.Close();
            Treasure = null;
            if (_room != null) _room.ConsumeFeature();
            RefreshHud();
            if (_hud != null) _hud.Flash(card.HeroId, Palette.Gold);
            Banner.Show(card.Name.ToUpperInvariant(), TreasureScreen.RarityColor(card.Rarity), "ADDED TO " + HeroDatabase.Get(card.HeroId).Name.ToUpperInvariant() + "'S DECK", 1f, 250f);
            yield return Wait(1.2f);
        }

        // ---- Dormitorul: vindecare / scoți o carte / upgrade ----
        IEnumerator BedroomSequence()
        {
            SetState(FlowState.Bedroom);
            yield return Wait(0.5f);
            BedroomAction? action = null;
            string heroId = null, cardUid = null;
            Bedroom = new BedroomScreen(_ui.Screens, Run.State, (a, h, u) => { action = a; heroId = h; cardUid = u; });
            Prompt = FlowPrompt.Bedroom;
            StartCoroutine(DelayedMoment("bedroom", 1.0f));
            while (action == null) yield return null;

            Prompt = FlowPrompt.None;
            var before = HpSnapshot();
            string cardName = null;
            if (cardUid != null)
                foreach (var h in Run.State.Party)
                    foreach (var c in h.Deck)
                        if (c.Uid == cardUid) cardName = c.Def.Name;
            Run.UseBedroom(action.Value, heroId, cardUid);
            Bedroom.Close();
            Bedroom = null;

            if (action == BedroomAction.Heal) ShowHpChanges(before, "SLEPT LIKE A BABY");
            else if (action == BedroomAction.UpgradeCard) Banner.Show(cardName + "+", Palette.Gold, "CARD UPGRADED", 1f, 250f);
            else Banner.Show(cardName, Palette.Blood, "REMOVED FROM THE DECK", 1f, 250f);
            if (heroId != null && _hud != null) _hud.Flash(heroId, action == BedroomAction.UpgradeCard ? Palette.Gold : Palette.Blood);
            RefreshHud();
            yield return Wait(1.3f);
        }

        // ---- Curio în cameră: cufărul se poate apăsa direct sau prin butonul SEARCH ----
        IEnumerator RoomCurioSequence()
        {
            SetState(FlowState.Curio);
            yield return Wait(0.3f);
            _curioSearch = false;
            bool chest = _room != null && _room.FeatureSprite != null && _room.FeatureSprite.Sprite != null && _room.FeatureSprite.Sprite.name == "chest_closed";
            _curioPrompt = new CurioPrompt(_ui.Screens, chest ? "A DUSTY CHEST" : "SOMETHING IN THE DIRT",
                chest ? "It might hold a gift. Or teeth." : "It smells awful. Something glints inside.", () => _curioSearch = true);
            Prompt = FlowPrompt.Curio;
            StartCoroutine(DelayedMoment("curio_room", 0.8f));
            while (!_curioSearch)
            {
                if (GameInput.ClickedThisFrame && !GameInput.PointerOverUI() && _room != null && _room.Feature != null)
                {
                    var ray = _rig.ScreenToWorldRay(GameInput.PointerPosition);
                    if (Physics.Raycast(ray, out var hit, 60f) && hit.transform.IsChildOf(_room.Feature)) _curioSearch = true;
                }
                yield return null;
            }
            Prompt = FlowPrompt.None;
            _curioPrompt.Close();
            _curioPrompt = null;

            Vector3 at = _room.Feature.position + Vector3.up * 1.2f;
            yield return SearchAnimation(_room.FeatureSprite, chest ? "chest_open" : "punch");
            var before = HpSnapshot();
            var outcome = Run.ResolveCurio();
            _room.ConsumeFeature();
            yield return CurioOutcome(outcome, before, at);
        }

        // ---- Evenimentele de pe hol ----

        IEnumerator CorridorEvent(CorridorSegment segment, int slot)
        {
            switch (segment.Event)
            {
                case CorridorEventKind.Trap: yield return TrapSequence(); break;
                case CorridorEventKind.Curio: yield return CorridorCurioSequence(slot); break;
                case CorridorEventKind.Ambush: yield return AmbushSequence(); break;
            }
        }

        // Țepi din podea: un erou ia damage, iar cel mai rapid are 50% șanse să se ferească.
        IEnumerator TrapSequence()
        {
            Banner.Show("TRAP!", Palette.Mark, null, 0.7f, 240f);
            var spikes = Billboard.Create(_worldRoot, "Spikes", SpriteLibrary.Get("Sprites/Props/spikes_0"), _party.transform.position + new Vector3(0f, 0f, 0.9f));
            spikes.SetScale(1.6f);
            spikes.transform.localScale = new Vector3(1f, 0.05f, 1f);
            Tween.Scale(spikes.transform, Vector3.one, 0.12f, Ease.OutBack);
            AudioManager.Play("trap_spikes");
            ScreenShake.Add(0.35f);
            var before = HpSnapshot();
            var outcome = Run.ResolveTrap();
            yield return Wait(0.15f);
            ShowOutcome(outcome, before);
            StartCoroutine(DelayedMoment("trap", 0.35f));
            yield return Wait(1.1f);
            // Țepii se retrag în podea.
            if (spikes != null) spikes.SetSprite(SpriteLibrary.Get("Sprites/Props/spikes_1"));
            yield return Wait(0.6f);
            if (spikes != null) Destroy(spikes.gameObject);
        }

        IEnumerator CorridorCurioSequence(int slot)
        {
            var pile = _corridor != null ? _corridor.CurioPiles[slot] : null;
            Vector3 at = pile != null ? pile.transform.position + Vector3.up * 1f : _party.HeadOf("isaac") + Vector3.forward;
            Banner.Show("CURIO", Palette.Gold, "THE PARTY RUMMAGES THROUGH IT", 0.8f, 240f);
            yield return SearchAnimation(pile, "punch");
            var before = HpSnapshot();
            var outcome = Run.ResolveCurio();
            if (pile != null) pile.SetSprite(SpriteLibrary.Get("Sprites/Props/poop_2"));
            yield return CurioOutcome(outcome, before, at);
        }

        // Căutarea: obiectul tremură de trei ori, cu câte un zgomot înfundat, apoi "se deschide".
        IEnumerator SearchAnimation(Billboard target, string finalSound)
        {
            for (int i = 0; i < 3; i++)
            {
                AudioManager.Play("punch", 0.35f, 0.7f + i * 0.1f);
                if (target != null)
                {
                    var t = target.transform;
                    Vector3 home = t.localPosition;
                    Tween.Value(0.18f, k => { if (t != null) t.localPosition = home + new Vector3(Mathf.Sin(k * 40f) * 0.06f * (1f - k), 0f, 0f); },
                        Ease.Linear, () => { if (t != null) t.localPosition = home; }, 0f, false, t);
                }
                yield return Wait(0.28f);
            }
            AudioManager.Play(finalSound);
            if (target != null) target.Flash(Color.white, 0.12f);
            ScreenShake.Add(0.15f);
        }

        IEnumerator CurioOutcome(ExplorationOutcome outcome, Dictionary<string, int> before, Vector3 at)
        {
            switch (outcome.Kind)
            {
                case ExplorationOutcomeKind.Heal:
                    FloatingText.Spawn(at, "A SOOTHING WARMTH", Palette.Poison);
                    ShowHpChanges(before, null);
                    break;
                case ExplorationOutcomeKind.Card:
                    FloatingText.Spawn(at, "A CARD!", Palette.Gold, true);
                    AudioManager.Play("card_flip_reward");
                    break;
                case ExplorationOutcomeKind.Trap:
                case ExplorationOutcomeKind.Avoided:
                    FloatingText.Spawn(at, "IT WAS A TRAP!", Palette.Mark);
                    yield return Wait(0.35f);
                    ShowOutcome(outcome, before);
                    break;
            }
            StartCoroutine(DelayedMoment("curio_result", 0.4f));
            yield return Wait(1.1f);
            if (Run.Phase == RunPhase.CardReward) yield return RewardSequence();
            RefreshHud();
        }

        IEnumerator AmbushSequence()
        {
            Banner.Show("AMBUSH!", Palette.Blood, null, 0.9f);
            AudioManager.Play("monster_roar");
            ScreenShake.Add(0.5f);
            WorldView.Instance.PulseVignette(0.3f, 0.7f);
            StartCoroutine(DelayedMoment("ambush", 0.3f));
            yield return Wait(0.8f);
            yield return CombatSequence(_corridor.AmbushStage(_walker.Z, Run.State.FloorIndex));
        }

        // ---- Feedback de HP în explorare ----

        Dictionary<string, int> HpSnapshot()
        {
            var hp = new Dictionary<string, int>();
            foreach (var h in Run.State.Party) hp[h.Def.Id] = h.Hp;
            return hp;
        }

        // Capcană: damage pe un erou (sau eschiva celui mai rapid).
        void ShowOutcome(ExplorationOutcome outcome, Dictionary<string, int> before)
        {
            if (outcome.Kind == ExplorationOutcomeKind.Avoided)
            {
                _party.Hop(outcome.HeroId);
                FloatingText.Spawn(_party.HeadOf(outcome.HeroId), "DODGED!", Palette.Stun);
                AudioManager.Play("ui_hover", 1f, 1.4f);
                return;
            }
            ShowHpChanges(before, null);
        }

        // Compară HP-ul de dinainte cu cel de acum și arată diferențele: damage (alb), vindecare (verde), moarte.
        void ShowHpChanges(Dictionary<string, int> before, string healTitle)
        {
            bool anyHeal = false;
            foreach (var hero in Run.State.Party)
            {
                if (!before.TryGetValue(hero.Def.Id, out int old)) continue;
                int delta = hero.Hp - old;
                if (delta == 0) continue;
                Vector3 head = _party.HeadOf(hero.Def.Id);
                if (delta < 0)
                {
                    _party.Hurt(hero.Def.Id);
                    FloatingText.Spawn(head, delta.ToString(), Palette.White, true);
                    AudioManager.Play("hit_flesh");
                    ScreenShake.Add(0.3f);
                    WorldView.Instance.PulseVignette(0.18f);
                    var member = _party.Find(hero.Def.Id);
                    if (member != null)
                        EnvKit.FloorDecal(_worldRoot, MaterialsFor(), "blood_decal_" + UnityEngine.Random.Range(0, 5), member.Root.position, 1.3f, UnityEngine.Random.Range(0f, 360f));
                    if (!hero.IsAlive)
                    {
                        _party.Die(hero.Def.Id);
                        AudioManager.Play("hero_death");
                        FloatingText.Spawn(head + Vector3.up * 0.4f, hero.Def.Name.ToUpperInvariant() + " DIED", Palette.Blood, false, 0.4f);
                    }
                }
                else
                {
                    anyHeal = true;
                    _party.Heal(hero.Def.Id);
                    FloatingText.Spawn(head, "+" + delta, Palette.Poison, true);
                }
            }
            if (anyHeal)
            {
                AudioManager.Play("heal");
                if (!string.IsNullOrEmpty(healTitle)) Banner.Show(healTitle, Palette.Poison, null, 0.9f, 250f);
            }
            _party.SyncRanks(Run.State);
            RefreshHud();
        }

        MaterialSet MaterialsFor() => _corridor != null ? _corridor.MaterialSet : _room != null ? _room.MaterialSet : _fallbackMaterials;
        readonly MaterialSet _fallbackMaterials = new MaterialSet();

        // ---- Sandbox de luptă (pentru echipa C): -combat-sandbox [encounterId] ----
        // encounterId e citit de CombatSandbox.Encounter (echipa C), ca ambele intrări să înțeleagă aceleași id-uri:
        // "floor2", "ambush3", "boss2", "monstro" / "gurdy" / "mom", o listă "gaper,fly,horf", "showcase"; gol = etajul 1.
        // Opțiuni: -combat-autoplay, -combat-fast, -combat-showcase, -combat-once (o singură luptă, fără repetare).
        IEnumerator SandboxState()
        {
            string id = CommandLine.Value("-combat-sandbox");
            int seed = CommandLine.Int("-seed", 7);
            if (CommandLine.Has("-combat-autoplay")) CombatController.AutoPlay = true;
            if (CommandLine.Has("-combat-fast")) CombatController.Fast = true;
            bool once = CommandLine.Has("-combat-once") || QaHooks.Active;
            bool showcase = CommandLine.Has("-combat-showcase") || id == "showcase";

            while (true)
            {
                Run = new Run(seed); // echipa proaspătă, cu HP plin, la fiecare repetare
                if (showcase) AddShowcaseCards(Run.State);
                var rng = new GameRng(seed).Fork("sandbox");
                var encounter = CombatSandbox.Encounter(id, rng);
                int floor = encounter.Floor;
                Debug.Log("[GameFlow] sandbox de luptă: " + string.Join(",", encounter.EnemyIds) + " (etaj " + floor + ", seed " + seed + ")");
                _theme = FloorTheme.ForFloor(floor);
                _theme.Apply();
                AudioManager.PlayAmbience(_theme.Ambience);

                ClearWorld();
                _room = RoomBuilder.Build(_worldRoot, _theme, encounter.IsBoss ? RoomType.Boss : RoomType.Combat, false, false, seed);
                SpawnParty(RoomBuilder.RestZ);
                _rig.SnapToFollow(_party.transform);
                if (_ui.CurtainAlpha > 0.01f) yield return _ui.FadeIn(0.4f).Wait();
                yield return Wait(0.6f);

                var engine = new CombatEngine(new CombatState(Run.State.Party, encounter, rng), rng);
                bool won = false;
                var stage = _room.StageInfo(floor, encounter.IsBoss);
                stage.IsAmbush = encounter.IsAmbush;
                yield return PresentCombat(engine, stage, v => won = v);
                if (won)
                {
                    // Doar pentru privit: recompensa nu intră în niciun run.
                    var card = RewardGenerator.RollCard(Run.State, new GameRng(seed).Fork("sandbox_loot"), encounter.IsBoss);
                    bool done = false;
                    RewardScreen.Show(card, card.Rarity == Rarity.Rare, _ => done = true);
                    while (!done) yield return null;
                }
                Banner.Show(won ? "VICTORY" : "DEFEAT", won ? Palette.Gold : Palette.Blood, "SANDBOX  SEED " + seed, 1.4f);
                StartCoroutine(DelayedMoment("sandbox_end", 0.4f));
                yield return Wait(2.2f);
                if (once) { SandboxFinished = true; yield break; }
                yield return _ui.FadeOut(0.4f).Wait();
                seed++;
            }
        }

        // Cărțile cu cele mai multe efecte vizuale (aceeași listă ca în CombatSandbox al echipei C).
        static void AddShowcaseCards(RunState state)
        {
            var extra = new Dictionary<string, string[]>
            {
                { "azazel", new[] { "charged_brimstone", "maw_of_the_void", "pentagram" } },
                { "cain", new[] { "moms_knife", "dr_fetus", "midas_touch", "crickets_head" } },
                { "isaac", new[] { "polyphemus", "ipecac", "20_20", "holy_mantle" } },
                { "lilith", new[] { "lil_brimstone", "twisted_pair", "guardian_angel", "moms_eyeshadow" } },
            };
            foreach (var hero in state.Party)
            {
                if (!extra.TryGetValue(hero.Def.Id, out var ids)) continue;
                foreach (var cardId in ids) hero.Deck.Add(state.NewCard(CardDatabase.Get(cardId)));
                if (hero.Deck.Count > 0) hero.Deck[0].Upgraded = true;
            }
        }
    }
}
