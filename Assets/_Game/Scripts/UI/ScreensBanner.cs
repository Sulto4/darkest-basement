using DarkestBasement.Core;
using TMPro;
using UnityEngine;

namespace DarkestBasement.UI
{
    // Text mare care "trântește" pe ecran: "AMBUSH!", "TRAP!", numele boss-ului. Intră mare și se așază (OutBack),
    // stă puțin, apoi se stinge. Se folosește rar, ca să-și păstreze efectul.
    public static class Banner
    {
        public static void Show(string text, Color color, string subtitle = null, float hold = 1.1f, float y = 220f)
        {
            var ui = UIRoot.Instance;
            if (ui == null) return;
            var root = UiKit.Rect(ui.Screens, "Banner", new Vector2(320, y), new Vector2(620, 70));
            var group = root.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            // Textele scurte (AMBUSH!, MONSTRO) la 48 px; cele lungi (numele unei cărți) la 32 px, ca să încapă pe un rând.
            int size = text.Length <= 12 ? 3 : 2;
            var title = UiKit.Label(root, text, new Vector2(310, 40), new Vector2(620, 52), UiFont.Title, color, TextAlignmentOptions.Center, size);
            if (!string.IsNullOrEmpty(subtitle))
                UiKit.Label(root, subtitle, new Vector2(310, 8), new Vector2(620, 12), UiFont.Label, Palette.Parchment);

            float age = 0f;
            Ticker.Attach(root.gameObject, () =>
            {
                age += Time.unscaledDeltaTime;
                float pop = Easing.Evaluate(Ease.OutBack, Mathf.Clamp01(age / 0.28f));
                title.Rect.localScale = Vector3.one * Mathf.LerpUnclamped(1.8f, 1f, pop);
                group.alpha = Mathf.Clamp01(age / 0.12f) * Mathf.Clamp01((hold + 0.5f - age) / 0.5f);
                if (age > hold + 0.5f) Object.Destroy(root.gameObject);
            });
        }
    }
}
