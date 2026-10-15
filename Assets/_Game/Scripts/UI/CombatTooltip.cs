using DarkestBasement.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DarkestBasement.UI
{
    // Tooltip-ul luptei: o bucată de pergament cu titlu și explicație (statusuri, intenții, cărți care nu se pot juca).
    // Unul singur pentru tot ecranul; cine îl deschide îl și închide.
    public sealed class CombatTooltip : MonoBehaviour
    {
        const float Width = 132f, Pad = 6f;
        static CombatTooltip _instance;

        RectTransform _rt;
        Image _bg, _icon;
        TextMeshProUGUI _title, _body;
        object _owner;

        static CombatTooltip Instance
        {
            get
            {
                if (_instance != null) return _instance;
                if (UIRoot.Instance == null) return null;
                var rt = UiKit.Rect(UIRoot.Instance.Overlay, "CombatTooltip", Vector2.zero, new Vector2(Width, 40), new Vector2(0.5f, 0f));
                _instance = rt.gameObject.AddComponent<CombatTooltip>();
                _instance.Build(rt);
                return _instance;
            }
        }

        void Build(RectTransform rt)
        {
            _rt = rt;
            _bg = CombatUi.Panel(rt, "tooltip", new Vector2(Width / 2f, 20), new Vector2(Width, 40));
            _icon = CombatUi.Image(rt, (Sprite)null, new Vector2(Pad + 6, 30), new Vector2(12, 12));
            _title = CombatUi.Text(rt, "", new Vector2(Width / 2f, 30), new Vector2(Width - Pad * 2, 9), UiFont.LabelBold, Palette.Ink, TextAlignmentOptions.Left);
            _body = CombatUi.Text(rt, "", new Vector2(Width / 2f, 16), new Vector2(Width - Pad * 2, 20), UiFont.Body, Palette.Ink, TextAlignmentOptions.TopLeft);
            _body.textWrappingMode = TextWrappingModes.Normal;
            gameObject.SetActive(false);
        }

        // anchor = punctul de sub care/deasupra căruia apare (în pixeli de referință).
        public static void Show(object owner, string title, string body, Vector2 anchor, Sprite icon = null, bool below = false)
        {
            var t = Instance;
            if (t == null) return;
            t._owner = owner;
            t.Layout(title, body, icon);
            t.Place(anchor, below);
            t.gameObject.SetActive(true);
            t.transform.SetAsLastSibling();
        }

        public static void Hide(object owner)
        {
            if (_instance == null || (owner != null && _instance._owner != owner)) return;
            _instance._owner = null;
            _instance.gameObject.SetActive(false);
        }

        // Pentru QA: tooltip-ul e pe ecran și cine l-a deschis.
        public static bool IsShown => _instance != null && _instance.gameObject.activeSelf;
        public static object Owner => _instance != null ? _instance._owner : null;
        public static string Title => _instance != null ? _instance._title.text : null;
        // Dreptunghiul tooltip-ului în pixeli de referință (pivotul e jos, la mijloc).
        public static Rect UIRect
        {
            get
            {
                if (_instance == null) return default;
                Vector2 p = _instance._rt.anchoredPosition, size = _instance._rt.sizeDelta;
                return new Rect(p.x - size.x / 2f, p.y, size.x, size.y);
            }
        }

        public static void HideAll()
        {
            if (_instance != null) { _instance._owner = null; _instance.gameObject.SetActive(false); }
        }

        void Layout(string title, string body, Sprite icon)
        {
            bool hasIcon = icon != null;
            _icon.enabled = hasIcon;
            _icon.sprite = icon;
            _title.text = title ?? "";
            _body.text = body ?? "";
            float textW = Width - Pad * 2;
            float bodyH = string.IsNullOrEmpty(body) ? 0f : Mathf.Ceil(_body.GetPreferredValues(body, textW, 0f).y);
            float titleH = string.IsNullOrEmpty(title) ? 0f : 10f;
            float height = Mathf.Round(Pad + titleH + (bodyH > 0f && titleH > 0f ? 3f : 0f) + bodyH + Pad - 1f);
            _rt.sizeDelta = new Vector2(Width, height);
            _bg.rectTransform.anchoredPosition = new Vector2(Width / 2f, height / 2f);
            _bg.rectTransform.sizeDelta = new Vector2(Width, height);
            float top = height - Pad;
            _icon.rectTransform.anchoredPosition = new Vector2(Pad + 6, top - 4);
            _title.rectTransform.anchoredPosition = new Vector2(Width / 2f + (hasIcon ? 8f : 0f), top - 4);
            _title.rectTransform.sizeDelta = new Vector2(textW - (hasIcon ? 16f : 0f), 9);
            _body.rectTransform.sizeDelta = new Vector2(textW, Mathf.Max(bodyH, 7f));
            _body.rectTransform.anchoredPosition = new Vector2(Width / 2f, top - titleH - (titleH > 0f ? 3f : 0f) - bodyH / 2f);
        }

        void Place(Vector2 anchor, bool below)
        {
            float h = _rt.sizeDelta.y;
            float y = below ? anchor.y - h - 6f : anchor.y + 6f;
            if (y + h > CombatUi.H - 2f) y = anchor.y - h - 6f;
            if (y < 2f) y = 2f;
            float x = Mathf.Clamp(anchor.x, Width / 2f + 2f, CombatUi.W - Width / 2f - 2f);
            _rt.anchoredPosition = CombatUi.Snap(new Vector2(x, y));
        }
    }
}
