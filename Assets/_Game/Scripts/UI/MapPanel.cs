using System;
using System.Collections.Generic;
using DarkestBasement.Audio;
using DarkestBasement.Core;
using DarkestBasement.Model.Core;
using DarkestBasement.Model.Data;
using DarkestBasement.Model.Map;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DarkestBasement.UI
{
    // Harta etajului, pe pergament, ca în Darkest Dungeon: camerele sunt pătrate pe grila de 5×4,
    // iar holurile sunt câte trei liniuțe (un segment = o liniuță, pentru că fiecare hol are 3 segmente).
    // Ce vezi: toată forma etajului; tipul unei camere doar după ce ai vizitat o vecină; craniul boss-ului mereu.
    // Camerele vecine (unde poți merge) pulsează auriu; la hover se ridică, iar holul până la ele se colorează.
    public sealed class MapPanel : MonoBehaviour
    {
        public static MapPanel Current { get; private set; }

        const float Pitch = 52f;     // distanța dintre centrele a două camere
        const float CellSize = 32f;  // map_cell (16 px) ×2: mărire întreagă, rămâne pixel-perfect
        static readonly Vector2 PanelSize = new Vector2(300, 270);
        static readonly Vector2 GridOrigin = new Vector2(30, 34);

        sealed class Cell
        {
            public Room Room;
            public RectTransform Root;
            public Image Base, Icon;
            public Image[] Frame;
            public bool Target;
        }

        sealed class LinkPiece { public Corridor Corridor; public int Segment; public Image Image; }

        Run _run;
        bool _interactive;
        Action<int> _onTravel;
        Action _onDescend;
        RectTransform _panel;
        Image _dim;
        TextMeshProUGUI _infoPlain;
        Button _descend;
        readonly Dictionary<int, Cell> _cells = new Dictionary<int, Cell>();
        readonly List<LinkPiece> _links = new List<LinkPiece>();
        int _hovered = -1;
        bool _closing;
        float _age;

        public bool IsInteractive => _interactive;

        public static MapPanel Open(Run run, bool interactive, Action<int> onTravel, Action onDescend)
        {
            if (Current != null) Current.Close(true);
            var root = UiKit.Stretch(UIRoot.Instance.Screens, "MapPanel");
            var map = root.gameObject.AddComponent<MapPanel>();
            map.Build(run, interactive, onTravel, onDescend);
            Current = map;
            return map;
        }

        void Build(Run run, bool interactive, Action<int> onTravel, Action onDescend)
        {
            _run = run;
            _interactive = interactive;
            _onTravel = onTravel;
            _onDescend = onDescend;

            _dim = UiKit.Dim(transform, 0f);
            _dim.raycastTarget = interactive; // în modul doar-citire (M pe hol) nu blocăm nimic

            _panel = UiKit.Panel(transform, "panel_paper", new Vector2(320, 182), PanelSize).rectTransform;
            _panel.GetComponent<Image>().raycastTarget = true;

            var floor = FloorTheme.ForFloor(run.State.FloorIndex);
            UiKit.Text(_panel, floor.Title, new Vector2(PanelSize.x / 2f, 246), new Vector2(260, 26), UiFont.Title, Palette.Ink, TextAlignmentOptions.Center, 2);
            UiKit.Text(_panel, "FLOOR " + floor.Index + " OF 3", new Vector2(PanelSize.x / 2f, 228), new Vector2(200, 10), UiFont.Label, Palette.WithAlpha(Palette.Ink, 0.7f));
            _infoPlain = UiKit.Text(_panel, "", new Vector2(PanelSize.x / 2f, 22), new Vector2(270, 10), UiFont.Label, Palette.Ink);

            BuildLinks();
            BuildCells();
            SetInfo(DefaultInfo());

            if (onDescend != null)
            {
                _descend = UiKit.BigButton(transform, "DESCEND", new Vector2(320, 26), new Vector2(120, 26), () => _onDescend?.Invoke());
            }
            if (!interactive)
                UiKit.Label(transform, "[M] CLOSE", new Vector2(320, 36), new Vector2(120, 10), UiFont.Label, Palette.Parchment);

            // Harta se "desfășoară": crește pe verticală și depășește puțin mărimea finală (OutBack).
            _panel.localScale = new Vector3(1f, 0.05f, 1f);
            Tween.Value(0.3f, k =>
            {
                if (_panel == null) return;
                _panel.localScale = new Vector3(1f, Mathf.LerpUnclamped(0.05f, 1f, k), 1f);
            }, Ease.OutBack, null, 0f, true, _panel);
            Tween.Value(0.3f, k => { if (_dim != null) _dim.color = new Color(0.02f, 0.01f, 0.01f, 0.5f * k); }, Ease.OutQuad, null, 0f, true, _dim);
            AudioManager.Play("shuffle", 0.7f);
        }

        Vector2 CellCenter(Room room) => GridOrigin + new Vector2(CellSize / 2f + room.X * Pitch, CellSize / 2f + (3 - room.Y) * Pitch);

        void BuildLinks()
        {
            var map = _run.Map;
            foreach (var corridor in map.Corridors)
            {
                Vector2 a = CellCenter(map.GetRoom(corridor.From)), b = CellCenter(map.GetRoom(corridor.To));
                Vector2 dir = (b - a).normalized;
                bool horizontal = Mathf.Abs(dir.x) > 0.5f;
                Vector2 start = a + dir * (CellSize / 2f);
                for (int i = 0; i < 3; i++)
                {
                    Vector2 p = start + dir * (3f + 7f * i);
                    var sprite = SpriteLibrary.Get(horizontal ? "Sprites/UI/map_link_h" : "Sprites/UI/map_link_v");
                    var img = UiKit.Image(_panel, sprite, new Vector2(Mathf.Round(p.x), Mathf.Round(p.y)));
                    img.raycastTarget = false;
                    _links.Add(new LinkPiece { Corridor = corridor, Segment = i, Image = img });
                }
            }
            RefreshLinks();
        }

        void BuildCells()
        {
            var map = _run.Map;
            int current = _run.State.CurrentRoom;
            var targets = new HashSet<int>();
            if (_interactive) foreach (var r in _run.AvailableDestinations) targets.Add(r.Id);

            foreach (var room in map.Rooms)
            {
                var c = new Cell { Room = room, Target = targets.Contains(room.Id) };
                Vector2 center = CellCenter(room);
                c.Root = UiKit.Rect(_panel, "Room" + room.Id, center, new Vector2(CellSize, CellSize));
                c.Frame = MakeFrame(c.Root, CellSize + 6f, Palette.Gold);
                c.Base = UiKit.Image(c.Root, SpriteLibrary.Get(room.Revealed ? "Sprites/UI/map_cell" : "Sprites/UI/map_cell_fog"),
                    new Vector2(CellSize / 2f, CellSize / 2f), new Vector2(CellSize, CellSize));
                string icon = room.Id == current ? "map_current" : IconFor(room);
                c.Icon = UiKit.Image(c.Root, SpriteLibrary.Get("Sprites/UI/" + icon), new Vector2(CellSize / 2f, CellSize / 2f), new Vector2(24, 24));
                c.Icon.raycastTarget = false;
                if (!room.Revealed) c.Icon.color = new Color(1f, 1f, 1f, 0.45f);
                else if (room.Cleared && room.Id != current) { c.Icon.color = new Color(1f, 1f, 1f, 0.55f); c.Base.color = new Color(0.82f, 0.76f, 0.68f); }

                int id = room.Id;
                var events = PointerEvents.Of(c.Base.gameObject);
                events.OnEnter = () => Hover(id, true);
                events.OnExit = () => Hover(id, false);
                events.OnClick = () => Click(id);
                SetFrame(c, room.Id == current ? 1f : 0f);
                _cells[room.Id] = c;
            }
        }

        static string IconFor(Room room)
        {
            if (!room.Revealed) return "map_unknown";
            switch (room.Type)
            {
                case RoomType.Start: return "map_start";
                case RoomType.Combat: return "map_combat";
                case RoomType.Treasure: return "map_treasure";
                case RoomType.Bedroom: return "map_bedroom";
                case RoomType.Curio: return "map_curio";
                case RoomType.Boss: return "map_boss";
                default: return "map_empty";
            }
        }

        string RoomName(Room room)
        {
            if (room.Id == _run.State.CurrentRoom) return "YOU ARE HERE";
            if (!room.Revealed) return "UNKNOWN ROOM";
            switch (room.Type)
            {
                case RoomType.Start: return "ENTRANCE";
                case RoomType.Combat: return room.Cleared ? "CLEARED ROOM" : "MONSTERS";
                case RoomType.Treasure: return room.Cleared ? "EMPTY PEDESTAL" : "TREASURE";
                case RoomType.Bedroom: return room.Cleared ? "UNMADE BED" : "BEDROOM - REST";
                case RoomType.Curio: return room.Cleared ? "SEARCHED" : "CURIO";
                case RoomType.Boss:
                    string boss = EnemyDatabase.Get(FloorDatabase.Get(_run.State.FloorIndex).BossId).Name.ToUpperInvariant();
                    return room.Cleared ? boss + " IS DEAD" : "BOSS - " + boss;
                default: return "EMPTY ROOM";
            }
        }

        string DefaultInfo()
        {
            if (!_interactive) return "";
            if (_onDescend != null) return "THE TRAPDOOR AWAITS";
            return "CHOOSE A GLOWING ROOM";
        }

        void SetInfo(string text) { if (_infoPlain != null) _infoPlain.text = text; }

        Image[] MakeFrame(RectTransform parent, float size, Color color)
        {
            float half = size / 2f, c = CellSize / 2f;
            var imgs = new[]
            {
                UiKit.Fill(parent, color, new Vector2(c, c + half - 1f), new Vector2(size, 2f)),
                UiKit.Fill(parent, color, new Vector2(c, c - half + 1f), new Vector2(size, 2f)),
                UiKit.Fill(parent, color, new Vector2(c - half + 1f, c), new Vector2(2f, size)),
                UiKit.Fill(parent, color, new Vector2(c + half - 1f, c), new Vector2(2f, size)),
            };
            return imgs;
        }

        static void SetFrame(Cell c, float alpha)
        {
            foreach (var img in c.Frame) img.color = Palette.WithAlpha(img.color, alpha);
        }

        void Hover(int id, bool on)
        {
            if (_closing || !_cells.TryGetValue(id, out var c)) return;
            if (on)
            {
                _hovered = id;
                SetInfo(RoomName(c.Room) + (c.Target ? "  -  CLICK TO TRAVEL" : ""));
                if (c.Target) AudioManager.Play("ui_hover");
            }
            else if (_hovered == id)
            {
                _hovered = -1;
                SetInfo(DefaultInfo());
            }
            RefreshLinks();
        }

        // Click pe o cameră vecină = călătorie. QA-ul cheamă aceeași funcție.
        public void Click(int roomId)
        {
            if (_closing || !_interactive || !_cells.TryGetValue(roomId, out var c) || !c.Target) return;
            AudioManager.Play("ui_click");
            var onTravel = _onTravel;
            _onTravel = null;
            onTravel?.Invoke(roomId);
        }

        public void Descend() { if (!_closing) _onDescend?.Invoke(); }

        void RefreshLinks()
        {
            int current = _run.State.CurrentRoom;
            foreach (var l in _links)
            {
                var seg = l.Corridor.Segments[l.Segment];
                Color c;
                bool hoveredPath = _hovered >= 0 && l.Corridor.Connects(current, _hovered);
                if (hoveredPath) c = Palette.GoldDark;
                else if (!seg.Revealed) c = new Color(1f, 1f, 1f, 0.35f);
                else if (seg.Event == CorridorEventKind.Ambush) c = Palette.BloodDark;
                else if (seg.Event == CorridorEventKind.Trap) c = new Color(0.55f, 0.55f, 0.6f);
                else if (seg.Event == CorridorEventKind.Curio) c = new Color(0.66f, 0.45f, 0.24f);
                else c = Color.white;
                l.Image.color = c;
            }
        }

        void Update()
        {
            _age += Time.unscaledDeltaTime;
            // Pulsul auriu al camerelor în care poți merge; camera curentă are rama plină.
            float pulse = 0.35f + 0.65f * (0.5f + 0.5f * Mathf.Sin(_age * 5f));
            foreach (var c in _cells.Values)
            {
                if (c.Room.Id == _run.State.CurrentRoom) continue;
                if (!c.Target) continue;
                bool hover = c.Room.Id == _hovered;
                SetFrame(c, hover ? 1f : pulse);
                c.Base.rectTransform.anchoredPosition = new Vector2(CellSize / 2f, CellSize / 2f + (hover ? 2f : 0f));
                c.Icon.rectTransform.anchoredPosition = c.Base.rectTransform.anchoredPosition;
            }
            if (_descend != null)
                _descend.transform.localScale = Vector3.one * (1f + 0.04f * Mathf.Round(Mathf.Sin(_age * 4f) * 0.5f + 0.5f));
        }

        public void Close(bool instant = false)
        {
            if (_closing) return;
            _closing = true;
            if (Current == this) Current = null;
            if (instant || _panel == null) { Destroy(gameObject); return; }
            Tween.Value(0.18f, k =>
            {
                if (_panel != null) _panel.localScale = new Vector3(1f, Mathf.LerpUnclamped(1f, 0.05f, k), 1f);
                if (_dim != null) _dim.color = new Color(0.02f, 0.01f, 0.01f, 0.5f * (1f - k));
            }, Ease.InBack, () => { if (this != null) Destroy(gameObject); }, 0f, true, _panel);
        }
    }
}
