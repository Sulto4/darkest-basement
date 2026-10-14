using System;
using System.Collections.Generic;
using DarkestBasement.Core;
using TMPro;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;

namespace DarkestBasement.UI
{
    public enum UiFont { Body, Label, Tiny, Title }

    // Fabrica de elemente UI. Tot UI-ul e construit din cod cu funcțiile astea, ca să arate la fel peste tot.
    // Coordonatele sunt în pixeli de referință (640×360), cu originea în colțul stânga-jos al părintelui.
    public static class UiKit
    {
        static readonly Dictionary<UiFont, TMP_FontAsset> Fonts = new Dictionary<UiFont, TMP_FontAsset>();

        // Fonturi pixel cu licență OFL (vezi Resources/Fonts/*/OFL.txt).
        static readonly Dictionary<UiFont, (string path, int size)> FontFiles = new Dictionary<UiFont, (string, int)>
        {
            { UiFont.Body, ("Fonts/PixelifySans/PixelifySans-Variable", 16) },
            { UiFont.Label, ("Fonts/Silkscreen/Silkscreen-Regular", 8) },
            { UiFont.Tiny, ("Fonts/Micro5/Micro5-Regular", 10) },
            { UiFont.Title, ("Fonts/JacquardaBastarda9/JacquardaBastarda9-Regular", 18) },
        };

        // Mărimea "nativă" a fiecărui font: textul arată curat doar la multipli ai ei.
        public static int NativeSize(UiFont font) => FontFiles[font].size;

        public static TMP_FontAsset Font(UiFont font)
        {
            if (Fonts.TryGetValue(font, out var asset) && asset != null) return asset;
            var (path, size) = FontFiles[font];
            var ttf = Resources.Load<Font>(path);
            if (ttf == null) { Debug.LogWarning("[UiKit] lipsește fontul " + path); return TMP_Settings.defaultFontAsset; }
            // RASTER_HINTED = glife desenate ca pixeli, nu ca SDF: pentru fonturi pixel arată tăios.
            asset = TMP_FontAsset.CreateFontAsset(ttf, size, 2, GlyphRenderMode.RASTER_HINTED, 512, 512);
            asset.atlasTexture.filterMode = FilterMode.Point;
            Fonts[font] = asset;
            return asset;
        }

        public static RectTransform Rect(Transform parent, string name, Vector2 pos, Vector2 size, Vector2? pivot = null)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = Vector2.zero;
            rt.pivot = pivot ?? new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return rt;
        }

        // Imagine la mărimea ei nativă (1 pixel de artă = 1 unitate UI), dacă nu primește altă mărime.
        public static Image Image(Transform parent, Sprite sprite, Vector2 pos, Vector2? size = null, string name = "Image")
        {
            var rt = Rect(parent, name, pos, size ?? (sprite != null ? sprite.rect.size : new Vector2(16, 16)));
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.preserveAspect = false;
            // Sprite-urile cu margini 9-slice (definite în ui_slices.json) se întind fără să-și deformeze colțurile.
            if (sprite != null && sprite.border != Vector4.zero) img.type = UnityEngine.UI.Image.Type.Sliced;
            return img;
        }

        public static Image Image(Transform parent, string spritePath, Vector2 pos, Vector2? size = null)
            => Image(parent, SpriteLibrary.Get(spritePath), pos, size, System.IO.Path.GetFileName(spritePath));

        public static TextMeshProUGUI Text(Transform parent, string text, Vector2 pos, Vector2 size, UiFont font = UiFont.Body,
            Color? color = null, TextAlignmentOptions align = TextAlignmentOptions.Center, int sizeMultiplier = 1)
        {
            var rt = Rect(parent, "Text", pos, size);
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            t.font = Font(font);
            t.fontSize = NativeSize(font) * sizeMultiplier;
            t.color = color ?? Palette.Ink;
            t.alignment = align;
            t.enableWordWrapping = true;
            t.overflowMode = TextOverflowModes.Overflow;
            t.raycastTarget = false;
            t.text = text;
            return t;
        }

        public static Button Button(Transform parent, string label, Vector2 pos, Vector2 size, Action onClick)
        {
            var img = Image(parent, SpriteLibrary.Get("Sprites/UI/button_normal"), pos, size, "Button");
            var b = img.gameObject.AddComponent<Button>();
            b.transition = Selectable.Transition.SpriteSwap;
            b.spriteState = new SpriteState
            {
                highlightedSprite = SpriteLibrary.Get("Sprites/UI/button_hover"),
                pressedSprite = SpriteLibrary.Get("Sprites/UI/button_pressed"),
                selectedSprite = SpriteLibrary.Get("Sprites/UI/button_hover"),
            };
            b.onClick.AddListener(() => { Audio.AudioManager.Play("ui_click"); onClick?.Invoke(); });
            Text(img.transform, label, size / 2f, size, UiFont.Label, Palette.Ink);
            return b;
        }
    }

    // Culorile UI din docs/ART.md, ca să nu scriem hex-uri peste tot.
    public static class Palette
    {
        public static readonly Color Ink = Hex("#2a1b13");
        public static readonly Color Parchment = Hex("#e8dcc4");
        public static readonly Color Gold = Hex("#e0b04a");
        public static readonly Color Blood = Hex("#d33c3c");
        public static readonly Color Block = Hex("#6fa8dc");
        public static readonly Color Poison = Hex("#7cc04a");
        public static readonly Color Stun = Hex("#f2d14a");
        public static readonly Color Weak = Hex("#9a8fb0");
        public static readonly Color Mark = Hex("#ff8a3d");
        public static readonly Color White = Hex("#fff0e8");

        public static Color Hex(string hex) => ColorUtility.TryParseHtmlString(hex, out var c) ? c : Color.magenta;
    }
}
