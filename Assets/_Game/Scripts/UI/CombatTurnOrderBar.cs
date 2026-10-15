using System.Collections.Generic;
using DarkestBasement.Combat;
using DarkestBasement.Core;
using UnityEngine;
using UnityEngine.UI;

namespace DarkestBasement.UI
{
    // Bara de inițiativă de sus: portretele în ordinea turelor din runda curentă.
    // Cel activ e ridicat și strălucește; cei care au jucat deja „ies” pe stânga, ceilalți alunecă după ei.
    public sealed class CombatTurnOrderBar : MonoBehaviour
    {
        const float Y = 334f, Spacing = 30f;

        sealed class Slot
        {
            public string Id;
            public RectTransform Root;
            public Image Frame, Portrait, Side;
            public Vector2 Pos;
            public float Alpha, Scale = 1f;
            public bool Leaving;
            public CanvasGroup Group;
        }

        CombatController _c;
        RectTransform _rt;
        Image _panel;
        OutlinedText _round;
        readonly List<Slot> _slots = new List<Slot>();
        string _active;
        float _panelWidth;

        public static CombatTurnOrderBar Create(Transform parent, CombatController c)
        {
            var rt = UiKit.Rect(parent, "TurnOrder", Vector2.zero, Vector2.zero, Vector2.zero);
            var bar = rt.gameObject.AddComponent<CombatTurnOrderBar>();
            bar._c = c;
            bar._rt = rt;
            bar._panel = CombatUi.Panel(rt, "panel_dark", new Vector2(320, Y), new Vector2(60, 40));
            bar._round = CombatUi.Outlined(rt, "", new Vector2(320, Y), new Vector2(70, 10), UiFont.LabelBold, Palette.Gold, 1, TMPro.TextAlignmentOptions.Right);
            // Până la prima rundă nu avem ordine: nu arătăm un panou gol.
            rt.gameObject.SetActive(false);
            return bar;
        }

        public void SetOrder(IReadOnlyList<string> order)
        {
            foreach (var s in _slots) if (s.Root != null) Destroy(s.Root.gameObject);
            _slots.Clear();
            _active = null;
            for (int i = 0; i < order.Count; i++)
            {
                var s = MakeSlot(order[i]);
                if (s == null) continue;
                // Intră din dreapta, în cascadă.
                s.Pos = new Vector2(TargetX(i, order.Count) + 40f + i * 12f, Y);
                s.Alpha = 0f;
                _slots.Add(s);
            }
            _round.Text = "ROUND " + _c.State.Round;
            gameObject.SetActive(true);
            // Panoul sare direct la lățimea nouă; doar portretele alunecă (altfel ar ieși din panou o clipă).
            _panelWidth = Mathf.Max(40f, order.Count * Spacing + 10f);
        }

        Slot MakeSlot(string id)
        {
            var view = _c.View(id);
            if (view == null) return null;
            var root = UiKit.Rect(_rt, "Slot " + id, new Vector2(320, Y), new Vector2(28, 32));
            var group = root.gameObject.AddComponent<CanvasGroup>();
            var frame = CombatUi.Image(root, "Sprites/UI/portrait_frame", new Vector2(14, 16), null, true);
            var portrait = CombatUi.Image(root, view.Frames.Portrait, new Vector2(14, 18), new Vector2(24, 24), "Portrait");
            // O dungă colorată sub portret: albastru = erou, roșu = inamic.
            var side = UiKit.Fill(root, view.IsHero ? Palette.Block : Palette.Blood, new Vector2(14, 5), new Vector2(20, 1));
            var slot = new Slot { Id = id, Root = root, Frame = frame, Portrait = portrait, Side = side, Group = group };
            CombatHoverTrigger.On(frame.gameObject, () =>
            {
                var info = _c.State.Unit(id);
                if (info == null) return;
                string body = "HP " + info.Hp + "/" + info.MaxHp + (id == _active ? "\nActing now." : "");
                CombatTooltip.Show(this, info.Name.ToUpperInvariant(), body, CombatUi.Snap(slot.Pos + new Vector2(0, -22)), null, true);
                // Conturul îl desenează HUD-ul (care știe și de mouse-ul de pe scenă), nu direct unitatea.
                _c.Hud?.HighlightUnit(id, true);
            }, () => { CombatTooltip.Hide(this); _c.Hud?.HighlightUnit(id, false); });
            return slot;
        }

        // Rama portretului unei unități din bară (pentru QA); null dacă nu e în bară.
        public RectTransform SlotRect(string id)
        {
            foreach (var s in _slots) if (s.Id == id && !s.Leaving) return s.Frame.rectTransform;
            return null;
        }

        public void SetActive(string id)
        {
            _active = id;
            // Cei de dinaintea celui activ au jucat deja în runda asta: pleacă din bară.
            int index = _slots.FindIndex(s => s.Id == id);
            for (int i = 0; i < index; i++) _slots[i].Leaving = true;
            foreach (var s in _slots)
            {
                s.Frame.sprite = SpriteLibrary.Get(s.Id == id ? "Sprites/UI/portrait_frame_active" : "Sprites/UI/portrait_frame");
                if (s.Id == id) s.Scale = 1.25f;
            }
        }

        public void Remove(string id)
        {
            foreach (var s in _slots) if (s.Id == id) s.Leaving = true;
        }

        float TargetX(int index, int count) => 320f + (index - (count - 1) / 2f) * Spacing;

        void LateUpdate()
        {
            float dt = Time.unscaledDeltaTime;
            float k = 1f - Mathf.Exp(-12f * dt);
            int visible = 0;
            foreach (var s in _slots) if (!s.Leaving) visible++;
            int i = 0;
            for (int n = _slots.Count - 1; n >= 0; n--)
            {
                var s = _slots[n];
                if (s.Leaving && s.Alpha <= 0.02f) { Destroy(s.Root.gameObject); _slots.RemoveAt(n); }
            }
            foreach (var s in _slots)
            {
                Vector2 target;
                if (s.Leaving) target = s.Pos + new Vector2(-20f, 0f);
                else
                {
                    target = new Vector2(TargetX(i, visible), Y + (s.Id == _active ? 4f : 0f));
                    i++;
                }
                s.Pos = Vector2.Lerp(s.Pos, target, k);
                s.Alpha = Mathf.MoveTowards(s.Alpha, s.Leaving ? 0f : 1f, dt * (s.Leaving ? 5f : 4f));
                s.Scale = Mathf.Lerp(s.Scale, 1f, k);
                s.Group.alpha = s.Alpha;
                s.Root.anchoredPosition = CombatUi.Snap(s.Pos);
                s.Root.localScale = Vector3.one * (s.Leaving ? Mathf.Lerp(0.6f, 1f, s.Alpha) : s.Scale);
                var frameColor = s.Id == _active ? Color.Lerp(Color.white, Palette.Gold, 0.3f + 0.3f * Mathf.Sin(Time.unscaledTime * 6f)) : Color.white;
                s.Frame.color = frameColor;
            }
            // Panoul se strânge odată cu portretele.
            float width = Mathf.Max(40f, visible * Spacing + 10f);
            _panelWidth = Mathf.Lerp(_panelWidth <= 0f ? width : _panelWidth, width, k);
            float w = Mathf.Round(_panelWidth / 2f) * 2f;
            _panel.rectTransform.sizeDelta = new Vector2(w, 42f);
            _panel.rectTransform.anchoredPosition = new Vector2(320f, Y + 1f);
            _round.Rect.anchoredPosition = CombatUi.Snap(new Vector2(320f - w / 2f - 40f, Y + 1f));
        }
    }
}
