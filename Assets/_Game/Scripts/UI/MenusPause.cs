using System;
using DarkestBasement.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DarkestBasement.UI
{
    // Meniul de pauză (Esc): jocul stă (Time.timeScale = 0), UI-ul merge cu timp "unscaled".
    public sealed class PauseMenu
    {
        public RectTransform Root { get; }
        public Button ResumeButton { get; }
        public Button AbandonButton { get; }

        public PauseMenu(Transform parent, int seed, Action onResume, Action onMainMenu, Action onQuit)
        {
            Root = UiKit.Stretch(parent, "PauseMenu");
            UiKit.Dim(Root, 0.6f);
            var panel = UiKit.Panel(Root, "panel_paper", new Vector2(320, 186), new Vector2(200, 196)).rectTransform;
            UiKit.Text(panel, "PAUSED", new Vector2(100, 166), new Vector2(190, 34), UiFont.Title, Palette.Ink, TextAlignmentOptions.Center, 2);
            ResumeButton = UiKit.BigButton(panel, "RESUME", new Vector2(100, 128), new Vector2(150, 24), onResume);
            AbandonButton = UiKit.Button(panel, "ABANDON RUN", new Vector2(100, 96), new Vector2(150, 18), onMainMenu);
            UiKit.Button(panel, "QUIT GAME", new Vector2(100, 72), new Vector2(150, 18), onQuit);
            UiKit.Text(panel, "SEED " + seed, new Vector2(100, 46), new Vector2(180, 10), UiFont.Label, Palette.Ink);
            UiKit.Text(panel, "W/RIGHT/LMB WALK  M MAP\nF FAST COMBAT  ESC RESUME", new Vector2(100, 26), new Vector2(180, 20), UiFont.Label,
                Palette.WithAlpha(Palette.Ink, 0.6f));

            panel.localScale = new Vector3(1f, 0.05f, 1f);
            Tween.Value(0.22f, k => { if (panel != null) panel.localScale = new Vector3(1f, Mathf.LerpUnclamped(0.05f, 1f, k), 1f); },
                Ease.OutBack, null, 0f, true, panel);
        }

        public void Close() { if (Root != null) UnityEngine.Object.Destroy(Root.gameObject); }
    }
}
