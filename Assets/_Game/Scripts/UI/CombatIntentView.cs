using System.Collections.Generic;
using DarkestBasement.Combat;
using DarkestBasement.Core;
using DarkestBasement.FX;
using DarkestBasement.Model.Combat;
using DarkestBasement.Model.Data;
using UnityEngine;
using UnityEngine.UI;

namespace DarkestBasement.UI
{
    // Intenția inamicului, deasupra capului (claritatea din Slay the Spire): iconiță + damage-ul prezis.
    // La hover pe inamic apar săgeți spre țintele lui. Mom în faza 2 are două intenții (două sloturi).
    public sealed class CombatIntentView : MonoBehaviour
    {
        sealed class Slot
        {
            public RectTransform Root;
            public Image Icon;
            public OutlinedText Text;
            public bool Active;
            public IntentKind Kind;
            public int Predicted, Hits;
            public string Name;
            public readonly List<string> Targets = new List<string>();
            public float Pop;
        }

        CombatController _c;
        UnitView _view;
        RectTransform _rt;
        readonly Slot[] _slots = new Slot[2];
        bool _arrows;
        readonly List<Image> _dots = new List<Image>();
        RectTransform _arrowLayer;

        public static CombatIntentView Create(Transform parent, CombatController c, UnitView view)
        {
            var rt = UiKit.Rect(parent, "Intent " + view.Id, Vector2.zero, Vector2.zero);
            // Totul ×2 (scară întreagă = pixeli curați): intenția trebuie citită dintr-o privire, lângă personaje mari.
            rt.localScale = Vector3.one * 2f;
            var v = rt.gameObject.AddComponent<CombatIntentView>();
            v._c = c; v._view = view; v._rt = rt;
            v._arrowLayer = UiKit.Rect(parent, "IntentArrows " + view.Id, Vector2.zero, Vector2.zero);
            for (int i = 0; i < 2; i++) v._slots[i] = v.MakeSlot(i);
            return v;
        }

        Slot MakeSlot(int index)
        {
            var root = UiKit.Rect(_rt, "Slot" + index, Vector2.zero, new Vector2(30, 14));
            var icon = CombatUi.Image(root, (Sprite)null, new Vector2(6, 7), new Vector2(12, 12), "Icon", true);
            var text = CombatUi.Outlined(root, "", new Vector2(22, 7), new Vector2(24, 10), UiFont.LabelBold, Palette.White, 1, TMPro.TextAlignmentOptions.Left);
            var slot = new Slot { Root = root, Icon = icon, Text = text };
            CombatHoverTrigger.On(icon.gameObject, () => ShowTooltip(slot), () => { CombatTooltip.Hide(this); SetArrows(false); });
            root.gameObject.SetActive(false);
            return slot;
        }

        public void Set(int index, IntentKind kind, int predicted, int hits, IReadOnlyList<string> targets, string name)
        {
            if (index < 0 || index > 1) return;
            var s = _slots[index];
            s.Active = true; s.Kind = kind; s.Predicted = predicted; s.Hits = Mathf.Max(1, hits); s.Name = name;
            s.Targets.Clear(); if (targets != null) s.Targets.AddRange(targets);
            // Host închis („Closed Shell”) se apără: scutul spune asta mai clar decât semnul întrebării.
            s.Icon.sprite = CombatUi.IntentIcon(name == "Closed Shell" ? IntentKind.Block : kind);
            s.Text.Text = Label(s);
            s.Root.gameObject.SetActive(true);
            s.Pop = 1f;
            s.Root.localScale = Vector3.one * 1.6f;
        }

        string Label(Slot s)
        {
            if (s.Kind == IntentKind.Attack || s.Kind == IntentKind.AttackAll)
            {
                if (s.Predicted <= 0) return "";
                // Damage pe lovitură × lovituri, ca în Slay the Spire („2x3”).
                return s.Hits > 1 ? (s.Predicted / s.Hits) + "x" + s.Hits : s.Predicted.ToString();
            }
            if (s.Kind == IntentKind.Block)
            {
                var info = _c.State.Unit(_view.Id);
                if (info != null && EnemyDatabase.All.TryGetValue(info.DefId, out var def))
                    foreach (var a in def.Abilities) if (a.Name == s.Name) foreach (var fx in a.Effects) if (fx.Kind == EffectKind.Block) return fx.Amount.ToString();
            }
            return "";
        }

        // Inamicul tocmai acționează: intenția „pulsează” și dispare.
        public void Consume(int index)
        {
            if (index < 0 || index > 1) return;
            var s = _slots[index];
            if (!s.Active) return;
            s.Active = false;
            var root = s.Root;
            root.localScale = Vector3.one * 1.5f;
            Tween.Value(0.22f, k => { if (root != null) { root.localScale = Vector3.one * Mathf.Lerp(1.5f, 0.2f, k); } }, Ease.InBack,
                () => { if (root != null) root.gameObject.SetActive(false); }, 0f, true, root);
        }

        public void Clear()
        {
            foreach (var s in _slots) { s.Active = false; s.Root.gameObject.SetActive(false); }
            SetArrows(false);
        }

        public bool HasIntent => _slots[0].Active || _slots[1].Active;

        public void SetArrows(bool on)
        {
            _arrows = on && HasIntent;
            if (!_arrows) foreach (var d in _dots) d.enabled = false;
        }

        public string Describe()
        {
            var parts = new List<string>();
            foreach (var s in _slots)
            {
                if (!s.Active) continue;
                string who = TargetNames(s);
                string what = s.Kind == IntentKind.Attack || s.Kind == IntentKind.AttackAll
                    ? (s.Predicted > 0 ? (s.Hits > 1 ? (s.Predicted / s.Hits) + " damage x" + s.Hits : s.Predicted + " damage") : "Attack")
                    : s.Name == "Closed Shell" ? "Hides in its shell: takes 75% less damage" : s.Name == "Reposition" ? "Moves forward"
                    : s.Kind == IntentKind.Block ? "Gains Block" : s.Kind == IntentKind.Debuff ? "Curses your party" : s.Kind == IntentKind.Buff ? "Buffs itself"
                    : s.Kind == IntentKind.Summon ? "Summons allies" : "Something strange";
                parts.Add(s.Name + ": " + what + (string.IsNullOrEmpty(who) ? "" : " to " + who) + ".");
            }
            return string.Join("\n", parts);
        }

        string TargetNames(Slot s)
        {
            if (s.Kind == IntentKind.AttackAll || s.Kind == IntentKind.Debuff) return "the whole party";
            var names = new List<string>();
            foreach (var id in s.Targets)
            {
                var u = _c.State.Unit(id);
                if (u != null && u.IsHero) names.Add(u.Name);
            }
            return string.Join(" and ", names);
        }

        void ShowTooltip(Slot s)
        {
            if (!s.Active) return;
            SetArrows(true);
            CombatTooltip.Show(this, _view.DisplayName.ToUpperInvariant(), Describe(), CombatUi.Snap(_rt.anchoredPosition + new Vector2(0, 16)), s.Icon.sprite);
        }

        void LateUpdate()
        {
            if (_view == null) { Destroy(_arrowLayer.gameObject); Destroy(gameObject); return; }
            // Deasupra capului, cu o legănare de 1 pixel (atrage privirea fără să obosească).
            Vector2 head = CombatCamera.WorldToUI(_view.Home + Vector3.up * (_view.TopHeight + 0.15f));
            // Boss-ii foarte înalți (piciorul lui Mom coboară din tavan): intenția rămâne sub bara de sus.
            head.y = Mathf.Min(head.y, 270f);
            float bob = Mathf.Round(Mathf.Sin(Time.unscaledTime * 3f + _view.Home.x) * 1f);
            _rt.anchoredPosition = CombatUi.Snap(head + new Vector2(0f, 16f + bob * 2f));
            int active = (_slots[0].Active ? 1 : 0) + (_slots[1].Active ? 1 : 0);
            int shown = 0;
            for (int i = 0; i < 2; i++)
            {
                var s = _slots[i];
                if (!s.Active) continue;
                float width = 14f + s.Text.Main.preferredWidth;
                float x = active == 2 ? (shown == 0 ? -width / 2f - 2f : width / 2f + 2f) : 0f;
                // Rădăcina are 30 px cu pivot la mijloc; conținutul (iconiță + cifră) începe la marginea ei stângă.
                s.Root.anchoredPosition = CombatUi.Snap(new Vector2(x + 15f - width / 2f, 0f));
                s.Root.localScale = Vector3.Lerp(s.Root.localScale, Vector3.one, 1f - Mathf.Exp(-12f * Time.unscaledDeltaTime));
                shown++;
            }
            if (_arrows) DrawArrows();
        }

        // Săgeți punctate (curbe Bézier pătratice) de la intenție la fiecare țintă.
        // B(t) = (1−t)²·A + 2(1−t)t·C + t²·B, unde C e un punct de control ridicat deasupra mijlocului.
        void DrawArrows()
        {
            int dot = 0;
            Vector2 a = _rt.anchoredPosition + new Vector2(-12f, 4f);
            foreach (var s in _slots)
            {
                if (!s.Active) continue;
                var targets = new List<string>(s.Targets);
                if (s.Kind == IntentKind.AttackAll || s.Kind == IntentKind.Debuff)
                {
                    targets.Clear();
                    foreach (var u in _c.State.Units.Values) if (u.IsHero && !u.Dead) targets.Add(u.Id);
                }
                foreach (var id in targets)
                {
                    var v = _c.View(id);
                    if (v == null || v.Dead) continue;
                    Vector2 b = CombatCamera.WorldToUI(v.Home + Vector3.up * (v.TopHeight + 0.1f));
                    Vector2 c = (a + b) * 0.5f + Vector2.up * (30f + Mathf.Abs(a.x - b.x) * 0.15f);
                    int steps = Mathf.Clamp(Mathf.RoundToInt(Vector2.Distance(a, b) / 9f), 6, 22);
                    float phase = (Time.unscaledTime * 2.5f) % 1f;
                    for (int i = 1; i <= steps; i++)
                    {
                        float t = (i - 1 + phase) / steps;
                        Vector2 p = (1 - t) * (1 - t) * a + 2 * (1 - t) * t * c + t * t * b;
                        var img = Dot(dot++);
                        img.sprite = PixelSprites.Square2;
                        img.rectTransform.sizeDelta = new Vector2(2, 2);
                        img.rectTransform.localRotation = Quaternion.identity;
                        img.rectTransform.anchoredPosition = CombatUi.Snap(p);
                        img.color = new Color(1f, 0.35f, 0.3f, 0.35f + 0.65f * t);
                    }
                    // Vârful săgeții, orientat pe direcția ultimului segment.
                    Vector2 tail = 0.01f * a + 0.18f * c + 0.81f * b;   // B(0.9) = 0,1²·A + 2·0,1·0,9·C + 0,9²·B
                    var head = Dot(dot++);
                    head.sprite = PixelSprites.Chevron;
                    head.rectTransform.sizeDelta = new Vector2(7, 7);
                    head.rectTransform.anchoredPosition = CombatUi.Snap(b);
                    head.rectTransform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(b.y - tail.y, b.x - tail.x) * Mathf.Rad2Deg);
                    head.color = new Color(1f, 0.35f, 0.3f, 1f);
                }
            }
            for (int i = dot; i < _dots.Count; i++) _dots[i].enabled = false;
        }

        Image Dot(int i)
        {
            while (_dots.Count <= i)
            {
                var img = CombatUi.Image(_arrowLayer, PixelSprites.Square2, Vector2.zero, new Vector2(2, 2), "Dot");
                _dots.Add(img);
            }
            _dots[i].enabled = true;
            return _dots[i];
        }

        void OnDestroy()
        {
            if (_arrowLayer != null) Destroy(_arrowLayer.gameObject);
        }
    }
}
