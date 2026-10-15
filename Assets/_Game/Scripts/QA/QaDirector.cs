using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using DarkestBasement.Combat;
using DarkestBasement.Core;
using DarkestBasement.Exploration;
using DarkestBasement.Model.Core;
using DarkestBasement.Model.Data;
using DarkestBasement.Model.Map;
using DarkestBasement.Model.Sim;
using DarkestBasement.UI;
using UnityEngine;

namespace DarkestBasement.QA
{
    // Pilotul automat (contractul -qa din docs/VIEW-CONTRACTS.md). Joacă jocul singur, apăsând aceleași
    // "butoane" ca un jucător (GameFlow.TravelTo, TreasureScreen.Choose...), cu deciziile lui Model.Sim.AutoPlayer.
    // Salvează capturi la momentele cheie și qa-log.txt cu toate avertismentele, erorile și excepțiile, apoi iese
    // cu codul 0 (curat) sau 1 (erori, blocaj, timeout).
    //
    // Argumente: -qa <folder> [-seed N] [-qa-script tour|full|combat|input] [-capture-fps 30] [-combat-sandbox [id]] [-qa-timeout s]
    //   tour   — etajul 1 cu camerele speciale, boss, trapa, cartonașul etajului 2, ecranul de final
    //   full   — un run întreg până la victorie sau moarte
    //   combat — prima luptă, cu multe capturi (pentru echipa C)
    //   input  — joacă prin mouse și tastatură virtuale, ca un om (QaDirector.Input.cs), cu PASS/FAIL pe fiecare pas
    public sealed partial class QaDirector : MonoBehaviour
    {
        string _dir, _script;
        int _seed, _captureFps, _shot, _frame;
        float _timeout, _started, _lastProgress;
        bool _sandbox, _finishing;
        int _errors, _envErrors, _warnings, _exceptions;
        StreamWriter _log;
        readonly object _logLock = new object();
        readonly Queue<string> _pending = new Queue<string>();
        readonly Dictionary<string, int> _tagCount = new Dictionary<string, int>();
        int _maxPerTag, _maxShots;
        GameFlow _flow;
        FlowState _lastState;
        FlowPrompt _lastPrompt;
        int _roomsVisited, _combats;
        string _outcome = "unfinished";

        // Erori care vin din mediul fără placă video/sunet (Xvfb), nu din joc: le notăm, dar nu pică testul.
        static readonly string[] EnvironmentNoise = { "FMOD", "ALSA", "pulse", "Vulkan", "libGL", "GLX", "XRandR", "Xinerama", "XInput", "dbus", "PulseAudio" };

        void Awake()
        {
            QaHooks.Active = true;
            _dir = Path.GetFullPath(CommandLine.Value("-qa", "Logs/qa"));
            _script = (CommandLine.Value("-qa-script", "tour") ?? "tour").ToLowerInvariant();
            _sandbox = CommandLine.Has("-combat-sandbox");
            if (_sandbox) _script = "sandbox";
            _seed = CommandLine.Int("-seed", 12345);
            _captureFps = CommandLine.Int("-capture-fps", 0);
            // Sub plafonul de timp al pasului QA din forge.sh (5400 s), ca să ieșim curat (cu raport) înainte să ne oprească systemd.
            // Un run complet are ~18 lupte animate (×2) și ~50 de holuri: ~45 de minute pe Forge.
            _manual = _script == "input";
            float defaultTimeout = _script == "full" ? 5200f : _script == "tour" || _manual ? 1500f : 900f;
            _timeout = CommandLine.Float("-qa-timeout", defaultTimeout);
            _maxPerTag = _script == "combat" || _script == "sandbox" ? 40 : _script == "full" ? 2 : 3;
            _maxShots = _script == "full" ? 220 : 140;

            Directory.CreateDirectory(_dir);
            if (_captureFps > 0) Directory.CreateDirectory(Path.Combine(_dir, "frames"));
            _log = new StreamWriter(Path.Combine(_dir, "qa-log.txt"), false, new UTF8Encoding(false)) { AutoFlush = true };
            Application.logMessageReceivedThreaded += OnLog;
            QaHooks.MomentRequested += OnMoment;
            Note("QA start: script=" + _script + " seed=" + _seed + " timeout=" + _timeout + "s capture-fps=" + _captureFps
                 + " screen=" + Screen.width + "x" + Screen.height + " gfx=" + SystemInfo.graphicsDeviceType);

            if (_captureFps > 0) Time.captureFramerate = _captureFps;
            // Scriptul "input" ține singur tasta W, la viteza reală de mers.
            QaHooks.ManualInput = _manual;
            GameInput.AutoWalk = !_manual;
            PartyWalker.GlobalSpeedScale = _script == "combat" || _manual || _captureFps > 0 ? 1f : _script == "full" ? 2.5f : 1.5f;
        }

        void Start()
        {
            _started = Time.realtimeSinceStartup;
            _lastProgress = _started;
            StartCoroutine(CaptureLoop());
            StartCoroutine(Drive());
        }

        // ---- Jurnalul ----

        void OnLog(string condition, string stackTrace, LogType type)
        {
            if (type == LogType.Log && !condition.StartsWith("[QA]") && !condition.StartsWith("[GameFlow]")) return;
            bool env = false;
            foreach (var n in EnvironmentNoise) if (condition.IndexOf(n, StringComparison.OrdinalIgnoreCase) >= 0) env = true;
            lock (_logLock)
            {
                if (type == LogType.Exception) _exceptions++;
                else if (type == LogType.Error || type == LogType.Assert) { if (env) _envErrors++; else _errors++; }
                else if (type == LogType.Warning) _warnings++;
                if (_log == null) return;
                float t = Time.realtimeSinceStartup - _started;
                _log.WriteLine("[" + t.ToString("0.0") + "s] [" + type + "] " + condition);
                if (type == LogType.Exception || type == LogType.Error || type == LogType.Assert)
                    if (!string.IsNullOrEmpty(stackTrace)) _log.WriteLine("    " + stackTrace.Replace("\n", "\n    ").TrimEnd());
            }
        }

        static void Note(string message) => Debug.Log("[QA] " + message);

        // ---- Capturi ----

        void OnMoment(string tag)
        {
            if (_shot >= _maxShots) return;
            // Scriptul "input" își face singur capturile, la fiecare pas (Shot); momentele automate ar umple folderul.
            if (_manual && tag != "stall") return;
            string key = _script == "full" && _flow != null && _flow.Run != null ? tag + "@" + _flow.Run.State.FloorIndex : tag;
            _tagCount.TryGetValue(key, out int count);
            if (count >= _maxPerTag) return;
            _tagCount[key] = count + 1;
            _pending.Enqueue(tag);
        }

        IEnumerator CaptureLoop()
        {
            var endOfFrame = new WaitForEndOfFrame();
            while (true)
            {
                yield return endOfFrame;
                if (_captureFps > 0) Save(Path.Combine(_dir, "frames", "frame_" + (_frame++).ToString("00000") + ".png"));
                if (_pending.Count > 0)
                {
                    string tag = _pending.Dequeue();
                    Save(Path.Combine(_dir, (++_shot).ToString("000") + "_" + tag + ".png"));
                    Note("captură " + _shot + " " + tag);
                }
            }
        }

        static void Save(string path)
        {
            var tex = ScreenCapture.CaptureScreenshotAsTexture();
            try { File.WriteAllBytes(path, tex.EncodeToPNG()); }
            finally { Destroy(tex); }
        }

        // ---- Pilotul ----

        IEnumerator Drive()
        {
            yield return null;
            _flow = GameFlow.Instance;
            _flow.StateChanged += OnState;
            ConfigureTeamCHooks();
            StartCoroutine(Watchdog());
            StartCoroutine(PeriodicShots());

            if (_sandbox)
            {
                while (!_flow.SandboxFinished) yield return null;
                yield return Seconds(0.5f);
                Finish("sandbox done");
                yield break;
            }

            if (_manual)
            {
                yield return DriveInput();
                yield break;
            }

            yield return Until(() => _flow.Prompt == FlowPrompt.MainMenu);
            yield return Seconds(1.8f);
            Note("pornesc run-ul cu seed " + _seed);
            _flow.Menu.StartWith(_seed);

            bool floorTwoShown = false;
            while (true)
            {
                var prompt = _flow.Prompt;
                var run = _flow.Run;
                switch (prompt)
                {
                    case FlowPrompt.ChooseRoom:
                        yield return Seconds(1.0f);
                        if (_flow.Prompt != FlowPrompt.ChooseRoom) break;
                        if (_script == "tour" && run.State.FloorIndex >= 2)
                        {
                            if (!floorTwoShown) { floorTwoShown = true; yield return Seconds(0.8f); }
                            Note("turul s-a încheiat pe etajul 2: arăt ecranul de final");
                            _flow.ForceEnd(false);
                            break;
                        }
                        if (run.CanDescend) { Note("cobor pe trapă"); _flow.DescendNow(); break; }
                        int dest = _script == "tour" ? TourDestination(run) : new AutoPlayer().ChooseDestination(run);
                        Note("merg spre camera " + dest + " (" + (run.Map.GetRoom(dest).Revealed ? run.Map.GetRoom(dest).Type.ToString() : "?") + ")");
                        _roomsVisited++;
                        _flow.TravelTo(dest);
                        break;

                    case FlowPrompt.Treasure:
                        yield return Seconds(1.6f);
                        if (_flow.Treasure == null) break;
                        int best = 0; double value = double.MinValue;
                        var player = new AutoPlayer();
                        for (int i = 0; i < run.TreasureChoices.Count; i++)
                        {
                            double v = player.CardValue(run.TreasureChoices[i]);
                            if (v > value) { value = v; best = i; }
                        }
                        Note("comoară: aleg " + run.TreasureChoices[best].Id);
                        _flow.Treasure.Choose(best);
                        yield return Until(() => _flow.Prompt != FlowPrompt.Treasure);
                        break;

                    case FlowPrompt.Bedroom:
                        yield return Seconds(1.6f);
                        if (_flow.Bedroom == null) break;
                        yield return Bedroom(run);
                        yield return Until(() => _flow.Prompt != FlowPrompt.Bedroom);
                        break;

                    case FlowPrompt.Curio:
                        yield return Seconds(1.4f);
                        Note("caut în curio");
                        _flow.SearchCurio();
                        yield return Until(() => _flow.Prompt != FlowPrompt.Curio);
                        break;

                    case FlowPrompt.EndScreen:
                        yield return Seconds(2.6f);
                        _outcome = _flow.State == FlowState.Victory ? "victory" : "game over";
                        Finish(_outcome);
                        yield break;
                }

                if (_script == "combat" && _combats > 0 && _flow.State != FlowState.Combat && _flow.State != FlowState.Reward
                    && (_flow.Prompt == FlowPrompt.ChooseRoom || _flow.Prompt == FlowPrompt.Walk))
                {
                    yield return Seconds(1f);
                    Finish("first combat done");
                    yield break;
                }
                yield return null;
            }
        }

        // Dormitorul: în tur arătăm și alegătorul de cărți (upgrade); altfel vindecare, ca în simulator.
        IEnumerator Bedroom(Run run)
        {
            if (_script != "tour")
            {
                Note("dormitor: vindecare");
                _flow.Bedroom.ChooseHeal();
                yield break;
            }
            PartyHero hero = null;
            foreach (var h in run.State.Party) if (h.IsAlive && (hero == null || h.Deck.Count > hero.Deck.Count)) hero = h;
            _flow.Bedroom.OpenPicker(BedroomAction.UpgradeCard, hero.Def.Id);
            yield return Seconds(0.6f);
            QaHooks.Moment("bedroom_picker");
            yield return Seconds(1.0f);
            foreach (var card in hero.Deck)
            {
                if (card.Upgraded) continue;
                Note("dormitor: upgrade " + card.Def.Id + " (" + hero.Def.Id + ")");
                _flow.Bedroom.Choose(BedroomAction.UpgradeCard, hero.Def.Id, card.Uid);
                yield break;
            }
            _flow.Bedroom.ChooseHeal();
        }

        // Turul vrea să vadă tot ce e diferit: camerele speciale cele mai apropiate, apoi explorare, apoi boss-ul.
        static int TourDestination(Run run)
        {
            var map = run.Map;
            int current = run.State.CurrentRoom;
            var dist = map.Distances(current);
            int visited = 0;
            foreach (var r in map.Rooms) if (r.Visited) visited++;

            Room goal = null;
            foreach (var r in map.Rooms)
            {
                bool special = r.Revealed && !r.Cleared && (r.Type == RoomType.Treasure || r.Type == RoomType.Bedroom || r.Type == RoomType.Curio);
                if (special && (goal == null || dist[r.Id] < dist[goal.Id])) goal = r;
            }
            if (goal == null && visited < 7)
                foreach (var r in map.Rooms)
                    if (!r.Visited && r.Id != map.BossId && (goal == null || dist[r.Id] < dist[goal.Id])) goal = r;
            if (goal == null) goal = map.GetRoom(map.BossId);

            var toGoal = map.Distances(goal.Id);
            int next = -1, best = int.MaxValue;
            foreach (int n in map.GetRoom(current).Neighbors)
                if (toGoal[n] < best) { best = toGoal[n]; next = n; }
            return next;
        }

        void OnState(FlowState state)
        {
            _lastProgress = Time.realtimeSinceStartup;
            if (state == FlowState.Combat) _combats++;
            Note("stare " + state + (_flow.Run != null ? " (etaj " + _flow.Run.State.FloorIndex + ", faza " + _flow.Run.Phase + ")" : ""));
        }

        // Capturi regulate în luptă (cărți în mână, lovituri) și pe hol (mersul), pentru momentele fără eveniment explicit.
        IEnumerator PeriodicShots()
        {
            if (_manual) yield break;
            float combatTimer = 0f, walkTimer = 0f;
            while (true)
            {
                float dt = Time.unscaledDeltaTime;
                if (_flow.State == FlowState.Combat)
                {
                    combatTimer += dt;
                    float every = _script == "combat" || _script == "sandbox" ? 1.5f : 4f;
                    if (combatTimer > every) { combatTimer = 0f; QaHooks.Moment("combat"); }
                }
                else combatTimer = 0f;

                if (_flow.Prompt == FlowPrompt.Walk)
                {
                    walkTimer += dt;
                    if (walkTimer > 2.5f) { walkTimer = -6f; QaHooks.Moment("corridor_walk"); }
                }
                yield return null;
            }
        }

        // Un test care se blochează e tot un test picat: timeout global și "nicio schimbare de stare prea mult timp".
        IEnumerator Watchdog()
        {
            while (true)
            {
                float now = Time.realtimeSinceStartup;
                if (_flow.Prompt != _lastPrompt || _flow.State != _lastState)
                {
                    _lastPrompt = _flow.Prompt;
                    _lastState = _flow.State;
                    _lastProgress = now;
                }
                // O luptă (mai ales de boss) poate sta minute întregi fără schimbare de stare; restul jocului, nu.
                bool fighting = _flow.State == FlowState.Combat || _flow.State == FlowState.Reward;
                float limit = fighting ? 900f : 150f;
                if (now - _started > _timeout) { Debug.LogError("[QA] timeout global după " + _timeout + " s"); Finish("timeout"); yield break; }
                if (now - _lastProgress > limit)
                {
                    Debug.LogError("[QA] blocaj: nicio schimbare în " + limit + " s (stare " + _flow.State + ", prompt " + _flow.Prompt + ")");
                    QaHooks.Moment("stall");
                    yield return Seconds(0.5f);
                    Finish("stall");
                    yield break;
                }
                yield return null;
            }
        }

        // Pilotul automat al echipei C: eroii sunt jucați de AutoPlayer (cu toate animațiile), iar recompensa
        // e luată sau sărită după aceeași politică ca în simulator.
        void ConfigureTeamCHooks()
        {
            if (_manual)
            {
                // Mâinile sunt ale scriptului: fără AutoPlay, fără alegere automată, viteza o schimbă tasta F.
                CombatController.Fast = false;
                CombatController.AutoPlay = false;
                RewardScreen.AutoDecide = null;
                Note("echipa C: AutoPlay nu, AutoDecide nu, Fast nu (input real)");
                return;
            }
            bool fast = _script != "combat" && _script != "sandbox" && _captureFps == 0;
            CombatController.Fast = fast;
            CombatController.AutoPlay = true;
            RewardScreen.AutoDecide = card => new AutoPlayer().ShouldTake(GameFlow.Instance.Run.State, card);
            Note("echipa C: AutoPlay da, AutoDecide da, Fast " + fast);
        }

        // ---- Final ----

        void Finish(string reason)
        {
            if (_finishing) return;
            _finishing = true;
            StartCoroutine(FinishRoutine(reason));
        }

        IEnumerator FinishRoutine(string reason)
        {
            // Lăsăm capturile cerute să se scrie.
            for (int i = 0; i < 10 && _pending.Count > 0; i++) yield return null;
            int failedSteps = _manual ? CloseSteps() : 0;
            int code = _errors + _exceptions > 0 || failedSteps > 0 || reason == "timeout" || reason == "stall" ? 1 : 0;
            var run = _flow != null ? _flow.Run : null;
            var summary = new StringBuilder();
            summary.AppendLine("script: " + _script);
            summary.AppendLine("seed: " + _seed);
            summary.AppendLine("result: " + reason);
            summary.AppendLine("exit code: " + code);
            summary.AppendLine("duration: " + (Time.realtimeSinceStartup - _started).ToString("0") + " s");
            summary.AppendLine("screenshots: " + _shot + (_captureFps > 0 ? ", frames: " + _frame : ""));
            summary.AppendLine("errors: " + _errors + ", exceptions: " + _exceptions + ", warnings: " + _warnings + ", environment errors (ignored): " + _envErrors);
            if (run != null && run.State != null)
            {
                summary.AppendLine("floor: " + run.State.FloorIndex + ", rooms travelled: " + _roomsVisited + ", combats: " + run.State.Stats.Combats + ", kills: " + run.State.Stats.Kills);
                foreach (var h in run.State.Party) summary.AppendLine("  " + h.Def.Id + " hp " + h.Hp + "/" + h.Def.MaxHp + " deck " + h.Deck.Count);
            }
            if (_manual) AppendSteps(summary);
            File.WriteAllText(Path.Combine(_dir, "qa-summary.txt"), summary.ToString());
            Note("QA gata: " + reason + ", cod " + code);
            Application.logMessageReceivedThreaded -= OnLog;
            lock (_logLock) { _log?.Flush(); _log?.Dispose(); _log = null; }
            GameInput.AutoWalk = false;
            _input?.Dispose();
            _input = null;
            Application.Quit(code);
        }

        static IEnumerator Seconds(float s)
        {
            float end = Time.unscaledTime + s;
            while (Time.unscaledTime < end) yield return null;
        }

        static IEnumerator Until(Func<bool> condition)
        {
            while (!condition()) yield return null;
        }

        void OnDestroy()
        {
            Application.logMessageReceivedThreaded -= OnLog;
            QaHooks.MomentRequested -= OnMoment;
            lock (_logLock) { _log?.Dispose(); _log = null; }
        }
    }
}
