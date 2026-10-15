using System;
using DarkestBasement.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DarkestBasement.UI
{
    // Întrebarea din camera Curio: un obiect ciudat, un singur buton. Obiectul se poate apăsa și direct în lume.
    public sealed class CurioPrompt
    {
        public RectTransform Root { get; }
        public Button SearchButton { get; }

        public CurioPrompt(Transform parent, string title, string flavor, Action onSearch)
        {
            Root = UiKit.Rect(parent, "CurioPrompt", new Vector2(320, 62), new Vector2(240, 70));
            var panel = UiKit.Panel(Root, "panel_dark", new Vector2(120, 35), new Vector2(240, 70)).rectTransform;
            UiKit.Label(panel, title, new Vector2(120, 56), new Vector2(220, 10), UiFont.Label, Palette.Gold);
            UiKit.Text(panel, flavor, new Vector2(120, 40), new Vector2(220, 14), UiFont.Body, Palette.Parchment);
            SearchButton = UiKit.BigButton(panel, "SEARCH", new Vector2(120, 16), new Vector2(110, 22), onSearch);

            var home = Root.anchoredPosition;
            Root.anchoredPosition = home + new Vector2(0, -90);
            Tween.Value(0.35f, k => { if (Root != null) Root.anchoredPosition = Vector2.LerpUnclamped(home + new Vector2(0, -90), home, k); },
                Ease.OutBack, null, 0f, true, Root);
        }

        public void Close() { if (Root != null) UnityEngine.Object.Destroy(Root.gameObject); }
    }
}
