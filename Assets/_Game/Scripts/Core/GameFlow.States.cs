using System.Collections;
using DarkestBasement.Audio;
using DarkestBasement.Exploration;
using DarkestBasement.Model.Core;
using DarkestBasement.Model.Map;
using DarkestBasement.UI;
using UnityEngine;

namespace DarkestBasement.Core
{
    // Stările mari: meniu, cartonașul de etaj, harta, holul, camera, trapa, finalul.
    public sealed partial class GameFlow
    {
        // ---- Meniul principal: echipa stă într-un hol luminat de torțe, camera plutește încet ----
        IEnumerator MainMenuState()
        {
            Prompt = FlowPrompt.None;
            ClearScreens();
            ClearWorld();
            if (_hud != null) { _hud.Destroy(); _hud = null; }
            Run = null;
            RunSeconds = 0f;

            _theme = FloorTheme.ForFloor(1);
            _theme.Apply();
            _corridor = CorridorBuilder.Build(_worldRoot, _theme, "normal", 1337, null);
            _party = PartyFormation.Create(_worldRoot, new RunState(1));
            _party.transform.localPosition = new Vector3(0.35f, 0f, 2.4f);
            // Camera stă jos, în spatele echipei, și privește de-a lungul holului spre torțe și ușa din fund.
            // Echipa iese puțin spre dreapta cadrului; în stânga rămâne loc pentru panoul meniului.
            var camPos = new Vector3(-1.25f, 1.5f, -3.6f);
            _rig.SetManualPose(camPos, Quaternion.LookRotation(new Vector3(0.1f, 1.15f, 10f) - camPos), 48f);
            _rig.ManualDrift = 1f;
            WorldView.Instance.ExtraVignette = 0.1f;

            AudioManager.PlayMusic("music_menu");
            AudioManager.PlayAmbience("basement_ambience");

            bool chosen = false;
            int seed = 0;
            Menu = new MainMenu(_ui.Screens, CommandLine.Int("-seed", MainMenu.RandomSeed()), s => { seed = s; chosen = true; }, Quit);
            yield return _ui.FadeIn(0.9f).Wait();
            Prompt = FlowPrompt.MainMenu;
            StartCoroutine(DelayedMoment("menu", 1.2f));
            while (!chosen) yield return null;

            Prompt = FlowPrompt.None;
            Menu.Close();
            Menu = null;
            yield return _ui.FadeOut(0.5f).Wait();
            _rig.ManualDrift = 0f;
            WorldView.Instance.ExtraVignette = 0f;
            BeginRun(seed);
            GoTo(FlowState.FloorIntro);
        }

        // ---- Cartonașul de etaj, pe negru ----
        IEnumerator FloorIntroState()
        {
            if (_ui.CurtainAlpha < 0.99f) yield return _ui.FadeOut(0.4f).Wait();
            ClearWorld();
            _theme = FloorTheme.ForFloor(Run.State.FloorIndex);
            _theme.Apply();
            AudioManager.PlayAmbience(_theme.Ambience);
            int floor = Run.State.FloorIndex;
            StartCoroutine(DelayedMoment("floor_card_" + floor, 1.0f));
            yield return FloorCard.Show(_theme, "FLOOR " + floor + " OF 3", floor > 1 ? "THE PARTY CATCHES ITS BREATH  +25% HP" : null);
            GoTo(FlowState.Map);
        }

        // ---- Harta: echipa stă în camera curentă, harta se desfășoară deasupra ----
        IEnumerator MapState()
        {
            Prompt = FlowPrompt.None;
            if (Run.IsRunOver) { GoTo(Run.IsVictory ? FlowState.Victory : FlowState.GameOver); yield break; }

            // Mom a murit: nu mai e nicio trapă, run-ul e câștigat.
            if (Run.CanDescend && Run.State.FloorIndex == 3)
            {
                Banner.Show("MOM IS DEAD", Palette.Gold, "THE BASEMENT FALLS SILENT", 1.6f);
                yield return Wait(2.2f);
                yield return _ui.FadeOut(0.8f).Wait();
                Run.Descend();
                GoTo(FlowState.Victory);
                yield break;
            }

            EnsureRoomView();
            RefreshHud();
            _hud.SetVisible(true);
            AudioManager.PlayMusic("music_explore");
            AudioManager.PlayAmbience(_theme.Ambience);
            if (_ui.CurtainAlpha > 0.01f) yield return _ui.FadeIn(0.5f).Wait();

            _travelTarget = -1;
            _descend = false;
            OpenTravelMap();
            if (!Run.CanDescend) Hint.Show("map_travel", "Click a glowing room to travel. M hides the map.", new Vector2(320, 20), 7f);
            Prompt = FlowPrompt.ChooseRoom;
            StartCoroutine(DelayedMoment("map", 0.6f));
            while (_travelTarget < 0 && !_descend) yield return null;

            Prompt = FlowPrompt.None;
            Hint.Hide("map_travel");
            if (MapPanel.Current != null) MapPanel.Current.Close();
            if (_descend) { GoTo(FlowState.Descending); yield break; }

            _travel = Run.BeginTravel(_travelTarget);
            // Holul are segmentele de la From la To; dacă mergem invers, le întâlnim în ordine inversă.
            _travelReverse = _travel.To == Run.State.CurrentRoom;
            GoTo(FlowState.Corridor);
        }

        // Camera în care stă echipa (construită o singură dată, apoi refolosită cât timp rămânem în ea).
        void EnsureRoomView()
        {
            int id = Run.State.CurrentRoom, floor = Run.State.FloorIndex;
            if (_room != null && _roomViewId == id && _roomViewFloor == floor && _party != null) return;
            ClearWorld();
            var room = Run.Map.GetRoom(id);
            _room = RoomBuilder.Build(_worldRoot, _theme, room.Type, room.Cleared, Run.CanDescend, RoomSeed(room));
            _roomViewId = id;
            _roomViewFloor = floor;
            SpawnParty(RoomBuilder.RestZ);
            _rig.SnapToFollow(_party.transform);
        }

        // ---- Holul: mergi ținând W, te oprești la evenimente, deschizi ușa ----
        IEnumerator CorridorState()
        {
            var destination = Run.Map.GetRoom(_travelTarget);
            yield return _ui.FadeOut(0.35f).Wait();
            ClearWorld();

            var slots = new CorridorEventKind[3];
            for (int i = 0; i < 3; i++)
            {
                var seg = _travel.Segments[_travelReverse ? 2 - i : i];
                slots[i] = seg.Resolved ? CorridorEventKind.None : seg.Event;
            }
            _corridor = CorridorBuilder.Build(_worldRoot, _theme, Door.TypeFor(destination.Type), CorridorSeed(_travel), slots);
            SpawnParty(0f);
            // Holurile deja curățate se parcurg de 3 ori mai repede (GDD §2: fără timp mort).
            _walker.SpeedMultiplier = _travel.TravelSpeedMultiplier;
            _rig.SnapToFollow(_party.transform);
            RefreshHud();
            _hud.SetVisible(true);
            yield return _ui.FadeIn(0.45f).Wait();
            StartCoroutine(DelayedMoment("corridor", 0.8f));
            if (_firstCorridor)
            {
                Hint.Show("walk", "Hold W, RIGHT or LEFT MOUSE to walk", new Vector2(320, 236), 8f);
                _firstCorridor = false;
            }

            for (int i = 0; i < 3; i++)
            {
                yield return WalkTo(CorridorBuilder.TriggerZ[i]);
                var segment = Run.AdvanceTravel();
                if (segment == null) break;
                if (segment.Resolved) continue;
                yield return CorridorEvent(segment, i);
                if (Run.IsRunOver) { GoTo(FlowState.GameOver); yield break; }
            }

            yield return WalkTo(CorridorBuilder.StopBeforeDoorZ);
            Hint.Hide("walk");
            if (MapPanel.Current != null) MapPanel.Current.Close();
            yield return _corridor.Door.Open();
            Moment("door");
            yield return Wait(0.25f);

            // Echipa intră pe ușă; ecranul se stinge din mers.
            StartCoroutine(FadeOutAfter(0.25f, 0.45f));
            yield return _walker.WalkTo(CorridorBuilder.EnterDoorZ, 2.8f);
            while (_ui.CurtainAlpha < 0.99f) yield return null;
            Run.EnterRoom();
            GoTo(FlowState.Room);
        }

        IEnumerator FadeOutAfter(float delay, float duration)
        {
            yield return Wait(delay);
            yield return _ui.FadeOut(duration).Wait();
        }

        // Mers ținând tasta până la z (următorul eveniment sau ușa).
        IEnumerator WalkTo(float z)
        {
            _walker.TargetZ = z;
            _walker.InputEnabled = true;
            Prompt = FlowPrompt.Walk;
            float startZ = _walker.Z;
            while (!_walker.Arrived)
            {
                if (_walker.Z - startZ > 2.5f) Hint.Hide("walk"); // a înțeles, nu-l mai bâzâim
                yield return null;
            }
            _walker.InputEnabled = false;
            Prompt = FlowPrompt.None;
        }

        // ---- Camera de la capătul holului ----
        IEnumerator RoomState()
        {
            var room = Run.Map.GetRoom(Run.State.CurrentRoom);
            ClearWorld();
            _room = RoomBuilder.Build(_worldRoot, _theme, room.Type, room.Cleared, Run.CanDescend, RoomSeed(room));
            _roomViewId = room.Id;
            _roomViewFloor = Run.State.FloorIndex;
            SpawnParty(RoomBuilder.EntryZ);
            _rig.SnapToFollow(_party.transform);
            RefreshHud();
            yield return _ui.FadeIn(0.5f).Wait();
            yield return _walker.WalkTo(RoomBuilder.RestZ, 2.6f);
            StartCoroutine(DelayedMoment("room_" + room.Type.ToString().ToLowerInvariant(), 0.1f));

            if (Run.PendingEncounter != null)
            {
                bool boss = room.Type == RoomType.Boss;
                if (boss) yield return BossIntro();
                yield return CombatSequence(_room.StageInfo(Run.State.FloorIndex, boss));
                if (Run.IsRunOver) { GoTo(Run.IsVictory ? FlowState.Victory : FlowState.GameOver); yield break; }
                if (boss && Run.State.FloorIndex < 3)
                {
                    _room.ShowTrapdoor(true);
                    Banner.Show("THE WAY DOWN IS OPEN", _theme.Accent, null, 1.2f, 250f);
                    StartCoroutine(DelayedMoment("trapdoor", 0.6f));
                    yield return Wait(1.2f);
                }
            }
            else if (Run.Phase == RunPhase.Treasure) yield return TreasureSequence();
            else if (Run.Phase == RunPhase.Bedroom) yield return BedroomSequence();
            else if (Run.Phase == RunPhase.Curio) yield return RoomCurioSequence();

            if (Run.IsRunOver) GoTo(Run.IsVictory ? FlowState.Victory : FlowState.GameOver);
            else GoTo(FlowState.Map);
        }

        IEnumerator BossIntro()
        {
            string boss = Model.Data.EnemyDatabase.Get(Run.CurrentFloor.BossId).Name.ToUpperInvariant();
            AudioManager.PlayMusic("music_boss");
            AudioManager.Play("monster_roar");
            FX.ScreenShake.Add(0.6f);
            WorldView.Instance.PulseVignette(0.3f, 0.8f);
            Banner.Show(boss, Palette.Blood, "FLOOR BOSS", 1.3f);
            StartCoroutine(DelayedMoment("boss_intro", 0.5f));
            yield return Wait(1.6f);
        }

        // ---- Trapa: echipa sare în gaură, camera o urmărește în jos, apoi negru ----
        IEnumerator DescendState()
        {
            Prompt = FlowPrompt.None;
            _hud.SetVisible(false);
            if (MapPanel.Current != null) MapPanel.Current.Close();
            EnsureRoomView();
            if (_room.Trapdoor == null) _room.ShowTrapdoor(false);

            // Echipa e deja lângă trapă (în stânga ei); sare direct, pe un arc.
            yield return Wait(0.2f);
            Vector3 hole = _room.transform.TransformPoint(_room.TrapdoorLocal);
            AudioManager.Play("trapdoor_descend");
            float jump = _party.JumpInto(hole);

            // Camera se apleacă peste gaură și "cade" după eroi.
            var from = new Pose(_rig.transform.position, _rig.transform.rotation);
            Vector3 above = hole + new Vector3(0.9f, 3.4f, -2.6f);
            var to = new Pose(above, Quaternion.LookRotation(hole - above));
            _rig.SetManualPose(from.position, from.rotation, _rig.FollowFov);
            _cameraTween = Tween.Value(jump + 0.4f, k =>
            {
                Vector3 p = Vector3.Lerp(from.position, to.position, k) + Vector3.down * (k * k * 1.5f);
                _rig.SetManualPose(p, Quaternion.Slerp(from.rotation, to.rotation, k), Mathf.Lerp(_rig.FollowFov, 38f, k));
            }, Ease.InOutCubic, null, 0f, false, _rig);
            StartCoroutine(DelayedMoment("descend", jump * 0.6f));
            yield return Wait(jump * 0.7f);
            yield return _ui.FadeOut(0.5f).Wait();
            StopCameraTween();

            Run.Descend();
            GoTo(Run.IsVictory ? FlowState.Victory : FlowState.FloorIntro);
        }

        // ---- Finalul: statistici, pachete, seed ----
        IEnumerator EndState(bool victory)
        {
            Prompt = FlowPrompt.None;
            if (_hud != null) _hud.SetVisible(false);
            if (MapPanel.Current != null) MapPanel.Current.Close();
            if (victory) AudioManager.PlayMusic("music_menu");
            else AudioManager.StopMusic();
            if (_ui.CurtainAlpha > 0.5f && _worldRoot.childCount > 0) yield return _ui.FadeIn(0.6f).Wait();
            else if (_ui.CurtainAlpha > 0.5f) _ui.FadeTo(0.6f, 0.6f);
            yield return Wait(0.5f);

            bool retry = false, fresh = false, menu = false;
            End = new EndScreen(_ui.Screens, Run, victory, RunSeconds, () => retry = true, () => fresh = true, () => menu = true);
            Prompt = FlowPrompt.EndScreen;
            StartCoroutine(DelayedMoment(victory ? "victory" : "game_over", 1.5f));
            while (!retry && !fresh && !menu) yield return null;

            Prompt = FlowPrompt.None;
            yield return _ui.FadeOut(0.5f).Wait();
            End.Close();
            End = null;
            int seed = Run.State.Seed;
            if (menu) { GoTo(FlowState.MainMenu); yield break; }
            BeginRun(retry ? seed : MainMenu.RandomSeed());
            GoTo(FlowState.FloorIntro);
        }
    }
}
