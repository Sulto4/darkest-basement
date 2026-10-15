using System;
using DarkestBasement.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DarkestBasement.UI
{
    // Meniul principal: logo-ul desenat de mână, iar dedesubt panoul cu NEW RUN, seed-ul (editabil) și QUIT.
    // În spate (în lumea 3D) echipa stă într-un hol luminat de torțe — îl construiește GameFlow.
    public sealed class MainMenu
    {
        public RectTransform Root { get; }
        readonly TMP_InputField _seed;
        readonly Action<int> _onStart;
        bool _done;

        public MainMenu(Transform parent, int suggestedSeed, Action<int> onStart, Action onQuit)
        {
            _onStart = onStart;
            Root = UiKit.Stretch(parent, "MainMenu");
            var group = Root.gameObject.AddComponent<CanvasGroup>();

            // Logo-ul plutește ușor, ca și cum ar atârna în întuneric.
            var logoSprite = SpriteLibrary.TryGet("Sprites/UI/title_logo");
            RectTransform logo;
            if (logoSprite != null)
            {
                var img = UiKit.Image(Root, logoSprite, new Vector2(320, 292));
                img.raycastTarget = false;
                logo = img.rectTransform;
            }
            else logo = UiKit.Label(Root, "DARKEST BASEMENT", new Vector2(320, 292), new Vector2(600, 60), UiFont.Title, Palette.Parchment, TextAlignmentOptions.Center, 3).Rect;
            UiKit.Label(Root, "A TURN-BASED DESCENT INTO THE BASEMENT", new Vector2(320, 252), new Vector2(400, 10), UiFont.Label, Palette.WithAlpha(Palette.ParchmentDark, 0.85f));

            var panel = UiKit.Panel(Root, "panel_dark", new Vector2(118, 100), new Vector2(184, 112)).rectTransform;
            UiKit.BigButton(panel, "NEW RUN", new Vector2(92, 84), new Vector2(156, 26), Start);

            UiKit.Label(panel, "SEED", new Vector2(36, 52), new Vector2(50, 10), UiFont.Label, Palette.Gold);
            _seed = SeedField(panel, new Vector2(108, 52), new Vector2(100, 18), suggestedSeed);

            UiKit.Button(panel, "QUIT", new Vector2(92, 20), new Vector2(156, 18), onQuit);

            UiKit.Label(Root, "HOLD W / RIGHT / LMB  WALK     M  MAP     F  FAST COMBAT     ESC  PAUSE", new Vector2(320, 12), new Vector2(600, 10),
                UiFont.Label, Palette.WithAlpha(Palette.ParchmentDark, 0.6f));
            UiKit.Label(Root, "UNOFFICIAL FAN GAME", new Vector2(590, 352), new Vector2(110, 10), UiFont.Label, Palette.WithAlpha(Palette.ParchmentDark, 0.35f));

            float age = 0f;
            Vector2 logoHome = logo.anchoredPosition;
            group.alpha = 0f;
            Ticker.Attach(Root.gameObject, () =>
            {
                age += Time.unscaledDeltaTime;
                group.alpha = Mathf.Clamp01((age - 0.3f) / 0.8f);
                logo.anchoredPosition = logoHome + new Vector2(0f, Mathf.Round(Mathf.Sin(age * 1.1f) * 2f));
            });
        }

        // Câmp de text pentru seed: doar cifre. Gol = seed aleatoriu.
        static TMP_InputField SeedField(Transform parent, Vector2 pos, Vector2 size, int value)
        {
            var bg = UiKit.Image(parent, SpriteLibrary.Get("Sprites/UI/button_pressed"), pos, size, "SeedField");
            var go = bg.gameObject;
            go.SetActive(false); // componentele se leagă întâi, apoi pornim obiectul (altfel TMP_InputField pornește fără text)

            var area = UiKit.Stretch(go.transform, "TextArea");
            area.offsetMin = new Vector2(5, 2);
            area.offsetMax = new Vector2(-5, -2);
            area.gameObject.AddComponent<RectMask2D>();
            var text = UiKit.Text(area, "", Vector2.zero, size, UiFont.Label, Palette.Ink, TextAlignmentOptions.Center);
            Fill(text.rectTransform);
            var placeholder = UiKit.Text(area, "RANDOM", Vector2.zero, size, UiFont.Label, Palette.WithAlpha(Palette.Ink, 0.45f), TextAlignmentOptions.Center);
            Fill(placeholder.rectTransform);
            text.textWrappingMode = TextWrappingModes.NoWrap;

            var field = go.AddComponent<TMP_InputField>();
            field.textViewport = area;
            field.textComponent = text;
            field.placeholder = placeholder;
            field.targetGraphic = bg;
            field.contentType = TMP_InputField.ContentType.IntegerNumber;
            field.characterLimit = 9;
            field.caretColor = Palette.Ink;
            field.customCaretColor = true;
            field.selectionColor = Palette.WithAlpha(Palette.Gold, 0.5f);
            go.SetActive(true);
            field.text = value.ToString();
            return field;
        }

        static void Fill(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        public int CurrentSeed
        {
            get
            {
                string s = _seed != null ? _seed.text.Trim() : "";
                if (string.IsNullOrEmpty(s)) return RandomSeed();
                if (int.TryParse(s, out int v)) return v;
                int hash = 17;
                foreach (char c in s) hash = unchecked(hash * 31 + c); // același text dă mereu același seed
                return hash & 0x7fffffff;
            }
        }

        public static int RandomSeed() => new System.Random(Environment.TickCount ^ DateTime.Now.Millisecond * 7919).Next(1, 99999999);

        // NEW RUN (sau QA-ul): pornește cu seed-ul din câmp.
        public void Start() => StartWith(CurrentSeed);

        public void StartWith(int seed)
        {
            if (_done) return;
            _done = true;
            _onStart?.Invoke(seed);
        }

        public void Close()
        {
            if (Root == null) return;
            var group = Root.GetComponent<CanvasGroup>();
            var go = Root.gameObject;
            Tween.Value(0.3f, k => { if (group != null) group.alpha = 1f - k; }, Ease.OutQuad, () => UnityEngine.Object.Destroy(go), 0f, true, go);
        }
    }
}
