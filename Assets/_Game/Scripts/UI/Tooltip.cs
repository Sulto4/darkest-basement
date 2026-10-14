using System;
using DarkestBasement.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DarkestBasement.UI
{
    // Tooltip-ul care urmărește mouse-ul (pe hartă, pe iconițele de status, pe cărți). GDD §10: jucătorul trebuie să
    // înțeleagă mereu de ce, deci orice iconiță poate avea o explicație.
    //
    // Folosire (și pentru echipa C):
    //   Tooltip.Attach(icon.gameObject, () => "BLEED 2\nTakes 2 damage at turn start.");
    // Obiectul are nevoie de un Graphic cu raycastTarget = true.
    public static class Tooltip
    {
        static RectTransform _root;
        static Image _panel;
        static TextMeshProUGUI _text;
        static object _owner;

        public static void Attach(GameObject target, Func<string> text)
        {
            var events = PointerEvents.Of(target);
            var previousEnter = events.OnEnter;
            var previousExit = events.OnExit;
            events.OnEnter = () => { previousEnter?.Invoke(); Show(text(), target); };
            events.OnExit = () => { previousExit?.Invoke(); Hide(target); };
        }

        public static void Show(string text, object owner = null)
        {
            if (string.IsNullOrEmpty(text) || UIRoot.Instance == null) return;
            Ensure();
            _owner = owner;
            _text.text = text;
            _text.ForceMeshUpdate();
            Vector2 size = _text.GetPreferredValues(text, 150f, 0f);
            float w = Mathf.Ceil(Mathf.Min(size.x, 150f) / 2f) * 2f + 12f;
            float h = Mathf.Ceil(size.y / 2f) * 2f + 8f;
            _panel.rectTransform.sizeDelta = new Vector2(w, h);
            _text.rectTransform.sizeDelta = new Vector2(w - 12f, h - 6f);
            _root.gameObject.SetActive(true);
            Follow();
        }

        public static void Hide(object owner = null)
        {
            if (_root == null) return;
            if (owner != null && !ReferenceEquals(owner, _owner)) return;
            _root.gameObject.SetActive(false);
        }

        static void Ensure()
        {
            if (_root != null) return;
            _root = UiKit.Rect(UIRoot.Instance.Overlay, "Tooltip", Vector2.zero, Vector2.zero, Vector2.zero);
            _panel = UiKit.Panel(_root, "tooltip", Vector2.zero, new Vector2(60, 20), Vector2.zero);
            _panel.raycastTarget = false;
            _text = UiKit.Text(_panel.transform, "", new Vector2(6, 4), new Vector2(50, 12), UiFont.Body, Palette.Ink, TextAlignmentOptions.TopLeft);
            _text.rectTransform.pivot = Vector2.zero;
            Ticker.Attach(_root.gameObject, Follow);
            _root.gameObject.SetActive(false);
        }

        // Stă lângă mouse, dar nu iese din ecran.
        static void Follow()
        {
            if (_root == null || !_root.gameObject.activeSelf) return;
            Vector2 p = UIRoot.Instance.ScreenToUI(GameInput.PointerPosition) + new Vector2(10f, -6f);
            Vector2 size = _panel.rectTransform.sizeDelta;
            p.y -= size.y;
            if (p.x + size.x > UIRoot.RefWidth - 2f) p.x -= size.x + 20f;
            if (p.y < 2f) p.y = 2f;
            _root.anchoredPosition = new Vector2(Mathf.Round(p.x), Mathf.Round(p.y));
        }
    }
}
