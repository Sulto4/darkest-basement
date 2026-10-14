using System.Collections;
using DarkestBasement.Core;
using TMPro;
using UnityEngine;

namespace DarkestBasement.UI
{
    // Cartonașul de etaj: pe negru, numele etajului cu gotica Jacquarda ("BASEMENT", "CAVES", "DEPTHS"),
    // ca titlurile de nivel din Isaac. Apare după fade-ul la negru și dispare înainte să se vadă harta.
    public static class FloorCard
    {
        public static IEnumerator Show(FloorTheme theme, string subtitle, string note = null)
        {
            var ui = UIRoot.Instance;
            var root = UiKit.Rect(ui.Overlay, "FloorCard", new Vector2(320, 190), new Vector2(400, 120));
            var group = root.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;

            var title = UiKit.Label(root, theme.Title, new Vector2(200, 64), new Vector2(400, 50), UiFont.Title, theme.Accent, TextAlignmentOptions.Center, 3);
            // Linia de sub titlu se "desenează" din centru spre margini.
            var line = UiKit.Fill(root, Palette.WithAlpha(theme.Accent, 0.8f), new Vector2(200, 34), new Vector2(0, 2));
            UiKit.Label(root, subtitle, new Vector2(200, 22), new Vector2(400, 12), UiFont.Label, Palette.Parchment);
            if (!string.IsNullOrEmpty(note))
                UiKit.Label(root, note, new Vector2(200, 4), new Vector2(400, 12), UiFont.Label, Palette.Poison);

            title.Rect.localScale = Vector3.one * 1.15f;
            yield return Tween.Value(0.6f, k =>
            {
                group.alpha = k;
                title.Rect.localScale = Vector3.one * Mathf.Lerp(1.15f, 1f, k);
                line.rectTransform.sizeDelta = new Vector2(Mathf.Round(k * 120f) * 2f, 2f);
            }, Ease.OutCubic, null, 0f, true, root).Wait();

            yield return Wait(1.5f);
            yield return Tween.Value(0.5f, k => group.alpha = 1f - k, Ease.InQuad, null, 0f, true, root).Wait();
            Object.Destroy(root.gameObject);
        }

        static IEnumerator Wait(float seconds)
        {
            float end = Time.unscaledTime + seconds;
            while (Time.unscaledTime < end) yield return null;
        }
    }
}
