using System;
using System.Collections;
using System.Collections.Generic;
using DarkestBasement.Audio;
using DarkestBasement.Combat;
using DarkestBasement.Exploration;
using DarkestBasement.Model.Core;
using DarkestBasement.Model.Map;
using DarkestBasement.QA;
using DarkestBasement.UI;
using UnityEngine;

namespace DarkestBasement.Core
{
    // Stările mari ale jocului. Fiecare are o corutină în GameFlow care o "joacă" de la cap la coadă
    // și la final spune care e starea următoare (GoTo). Combat, Reward, Treasure, Bedroom și Curio sunt
    // sub-stări: se petrec în interiorul unui hol sau al unei camere, dar le anunțăm ca să le vadă QA-ul și UI-ul.
    public enum FlowState { None, MainMenu, FloorIntro, Map, Corridor, Room, Combat, Reward, Treasure, Bedroom, Curio, Descending, GameOver, Victory, Sandbox }

    // Ce așteaptă jocul de la jucător chiar acum (QA-ul se uită aici ca să știe ce "buton" să apese).
    public enum FlowPrompt { None, MainMenu, ChooseRoom, Walk, Reward, Treasure, Bedroom, Curio, EndScreen }

    // Mașina de stări care conduce un run (Model.Core.Run) și tot ce se vede:
    //   MainMenu → FloorIntro → Map → Corridor → Room → (luptă → recompensă / comoară / pat / curio) → Map → …
    //   … → boss → trapa (Descending) → FloorIntro (etajul următor) → … → Victory sau GameOver.
    // Modelul decide regulile (ce e în cameră, cine moare); GameFlow doar le pune în scenă și așteaptă alegerile jucătorului.
    public sealed partial class GameFlow : MonoBehaviour
    {
        public static GameFlow Instance { get; private set; }
        public static bool Paused { get; private set; }

        public Run Run { get; private set; }
        public FlowState State { get; private set; } = FlowState.None;
        public FlowPrompt Prompt { get; private set; }
        public float RunSeconds { get; private set; }
        public bool SandboxFinished { get; private set; }
        public event Action<FlowState> StateChanged;

        // Ecranele active, ca QA-ul să le poată folosi exact cum le-ar folosi jucătorul.
        public MainMenu Menu { get; private set; }
        public TreasureScreen Treasure { get; private set; }
        public BedroomScreen Bedroom { get; private set; }
        public EndScreen End { get; private set; }

        UIRoot _ui;
        CameraRig _rig;
        Transform _worldRoot;          // tot ce e construit pentru scena curentă (un hol sau o cameră)
        FloorTheme _theme;
        CorridorBuilder _corridor;
        RoomBuilder _room;
        int _roomViewId = -1, _roomViewFloor = -1;
        PartyFormation _party;
        PartyWalker _walker;
        PartyHud _hud;
        PauseMenu _pause;
        CurioPrompt _curioPrompt;

        FlowState _next;
        int _errors;
        bool _firstCorridor;

        // Alegerile jucătorului: le scriu UI-ul (click) sau QA-ul, le citesc corutinele.
        int _travelTarget = -1;
        bool _descend, _curioSearch;
        Corridor _travel;
        bool _travelReverse;

        void Awake()
        {
            Instance = this;
            _ui = UIRoot.Instance;
            _rig = CameraRig.Instance;
        }

        void Start()
        {
            _worldRoot = new GameObject("[WorldRoot]").transform;
            FlowState first = CommandLine.Has("-combat-sandbox") ? FlowState.Sandbox : FlowState.MainMenu;
            StartCoroutine(Machine(first));
        }

        // ---- Motorul mașinii de stări ----

        IEnumerator Machine(FlowState first)
        {
            _next = first;
            while (_next != FlowState.None)
            {
                FlowState state = _next;
                _next = FlowState.None;
                SetState(state);
                Exception error = null;
                yield return Safe(Routine(state), e => error = e);
                if (error == null) continue;

                // O excepție în View nu trebuie să înghețe jocul: o raportăm și ne întoarcem pe un teren sigur.
                _errors++;
                Debug.LogError("[GameFlow] eroare în starea " + state + ": " + error);
                Time.timeScale = 1f;
                ClearScreens();
                bool canResume = Run != null && !Run.IsRunOver && Run.Phase == RunPhase.Map && _errors < 3;
                _next = canResume ? FlowState.Map : FlowState.MainMenu;
                if (_ui.CurtainAlpha < 0.99f) yield return _ui.FadeOut(0.3f).Wait();
            }
        }

        IEnumerator Routine(FlowState state)
        {
            switch (state)
            {
                case FlowState.MainMenu: return MainMenuState();
                case FlowState.FloorIntro: return FloorIntroState();
                case FlowState.Map: return MapState();
                case FlowState.Corridor: return CorridorState();
                case FlowState.Room: return RoomState();
                case FlowState.Descending: return DescendState();
                case FlowState.GameOver: return EndState(false);
                case FlowState.Victory: return EndState(true);
                case FlowState.Sandbox: return SandboxState();
                default: return Nothing();
            }
        }

        static IEnumerator Nothing() { yield break; }

        // Rulează o corutină cu sub-corutinele ei (yield return AltăCorutină()), prinzând orice excepție.
        // Unity, singur, ar opri corutina în tăcere și jocul ar rămâne blocat pe ecranul curent.
        static IEnumerator Safe(IEnumerator root, Action<Exception> onError)
        {
            var stack = new Stack<IEnumerator>();
            stack.Push(root);
            while (stack.Count > 0)
            {
                var top = stack.Peek();
                bool moved;
                try { moved = top.MoveNext(); }
                catch (Exception e) { onError(e); yield break; }
                if (!moved) { stack.Pop(); continue; }
                if (top.Current is IEnumerator nested) { stack.Push(nested); continue; }
                yield return top.Current;
            }
        }

        void SetState(FlowState state)
        {
            if (State == state) return;
            State = state;
            StateChanged?.Invoke(state);
        }

        void GoTo(FlowState state) => _next = state;

        public bool RunActive => Run != null && State != FlowState.MainMenu && State != FlowState.GameOver
                                 && State != FlowState.Victory && State != FlowState.None && State != FlowState.Sandbox;

        // ---- Update: pauză, hartă, cronometru ----

        void Update()
        {
            if (RunActive && !Paused) RunSeconds += Time.unscaledDeltaTime;
            if (GameInput.PausePressed) TogglePause();
            if (!Paused && GameInput.MapPressed) ToggleMap();
        }

        // Un hit-stop care se termină în timpul pauzei ar reporni timpul; pauza are ultimul cuvânt.
        void LateUpdate()
        {
            if (Paused) Time.timeScale = 0f;
        }

        public void TogglePause()
        {
            if (Paused) { Resume(); return; }
            bool canPause = RunActive || State == FlowState.Sandbox;
            if (!canPause) return;
            Paused = true;
            Time.timeScale = 0f;
            _pause = new PauseMenu(_ui.Overlay, Run != null ? Run.State.Seed : 0, Resume, AbandonRun, Quit);
            AudioManager.Play("ui_click");
        }

        public void Resume()
        {
            if (!Paused) return;
            Paused = false;
            // Revenim la viteza "normală" a luptei (×1 sau ×2 cu F), nu neapărat la 1.
            Time.timeScale = FX.HitStop.BaseTimeScale;
            _pause?.Close();
            _pause = null;
        }

        void AbandonRun()
        {
            Resume();
            StopAllCoroutines();
            ClearScreens();
            StopCombatViews();
            StartCoroutine(AbandonThenMenu());
        }

        IEnumerator AbandonThenMenu()
        {
            yield return _ui.FadeOut(0.4f).Wait();
            StartCoroutine(Machine(FlowState.MainMenu));
        }

        public static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        // M: harta. În starea Map e harta "de călătorie"; pe hol sau în cameră e doar de citit.
        void ToggleMap()
        {
            if (Run == null) return;
            if (MapPanel.Current != null)
            {
                MapPanel.Current.Close();
                return;
            }
            if (State == FlowState.Map && Prompt == FlowPrompt.ChooseRoom) OpenTravelMap();
            else if ((State == FlowState.Corridor || State == FlowState.Room) && (Prompt == FlowPrompt.Walk || Prompt == FlowPrompt.None))
                MapPanel.Open(Run, false, null, null);
        }

        void OpenTravelMap()
        {
            MapPanel.Open(Run, true, id => _travelTarget = id, Run.CanDescend ? (Action)(() => _descend = true) : null);
        }

        // ---- Comenzi publice (le folosesc UI-ul și QA-ul) ----

        public void TravelTo(int roomId)
        {
            if (Prompt != FlowPrompt.ChooseRoom) return;
            if (MapPanel.Current != null && MapPanel.Current.IsInteractive) MapPanel.Current.Click(roomId);
            else _travelTarget = roomId;
        }

        public void DescendNow()
        {
            if (Prompt == FlowPrompt.ChooseRoom && Run != null && Run.CanDescend) _descend = true;
        }

        public void SearchCurio()
        {
            if (Prompt == FlowPrompt.Curio) _curioSearch = true;
        }

        // QA: sare direct la ecranul de final (pentru captura lui), chiar dacă run-ul nu s-a terminat.
        public void ForceEnd(bool victory)
        {
            Resume();
            StopAllCoroutines();
            ClearScreens();
            StopCombatViews();
            StartCoroutine(Machine(victory ? FlowState.Victory : FlowState.GameOver));
        }

        // O luptă întreruptă (abandon din pauză): oprim prezentarea echipei C și readucem timpul la normal.
        static void StopCombatViews()
        {
            foreach (var c in FindObjectsByType<CombatController>(FindObjectsSortMode.None)) Destroy(c.gameObject);
            RewardScreen.Cancel();
            FX.HitStop.Reset();
        }

        // ---- Ajutoare ----

        void BeginRun(int seed)
        {
            Run = new Run(seed);
            RunSeconds = 0f;
            _errors = 0;
            _firstCorridor = true;
            Hint.ResetSeen();
            if (_hud == null) _hud = new PartyHud(_ui.Hud);
            _hud.SetVisible(false, 0f);
            Debug.Log("[GameFlow] run nou, seed " + seed);
        }

        void ClearWorld()
        {
            if (MapPanel.Current != null) MapPanel.Current.Close(true);
            Hint.HideAll();
            Tooltip.Hide();
            for (int i = _worldRoot.childCount - 1; i >= 0; i--) Destroy(_worldRoot.GetChild(i).gameObject);
            _corridor = null;
            _room = null;
            _party = null;
            _walker = null;
            _roomViewId = -1;
        }

        void ClearScreens()
        {
            if (MapPanel.Current != null) MapPanel.Current.Close(true);
            Menu?.Close(); Menu = null;
            Treasure?.Close(); Treasure = null;
            Bedroom?.Close(); Bedroom = null;
            End?.Close(); End = null;
            _curioPrompt?.Close(); _curioPrompt = null;
            Hint.HideAll();
            Tooltip.Hide();
            Prompt = FlowPrompt.None;
        }

        void SpawnParty(float z)
        {
            _party = PartyFormation.Create(_worldRoot, Run.State);
            _party.transform.localPosition = new Vector3(0f, 0f, z);
            _walker = PartyWalker.Attach(_party);
            _walker.TargetZ = z;
        }

        void SetCutaway(bool on)
        {
            if (_corridor != null) _corridor.SetCutaway(on);
            if (_room != null) _room.SetCutaway(on);
        }

        void RefreshHud()
        {
            if (_hud != null && Run != null) _hud.Refresh(Run.State, _theme);
        }

        static void Moment(string tag) => QaHooks.Moment(tag);

        IEnumerator DelayedMoment(string tag, float seconds)
        {
            yield return Wait(seconds);
            Moment(tag);
        }

        static IEnumerator Wait(float seconds)
        {
            if (seconds > 0f) yield return new WaitForSeconds(seconds);
        }

        int CorridorSeed(Corridor c) => unchecked(Run.State.Seed * 31 + Run.State.FloorIndex * 1009 + Math.Min(c.From, c.To) * 53 + Math.Max(c.From, c.To));
        int RoomSeed(Room r) => unchecked(Run.State.Seed * 17 + Run.State.FloorIndex * 101 + r.Id * 7);
    }
}
