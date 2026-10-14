using System;
using System.Collections.Generic;
using DarkestBasement.Core;
using TMPro;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;

namespace DarkestBasement.UI
{
    // Body = Micro 5 (textul principal, cărți, tooltip-uri); Label/LabelBold = Silkscreen (numere, etichete în majuscule);
    // Tiny = același Micro 5 (păstrat pentru codul vechi); Title = Jacquarda Bastarda (gotică, titluri);
    // Big = Pixelify Sans (doar texte mari de meniu, ≥ 20 px).
    public enum UiFont { Body, Label, Tiny, Title, Big, LabelBold }

    // Fabrica de elemente UI. Tot UI-ul e construit din cod cu funcțiile astea, ca să arate la fel peste tot.
    // Coordonatele sunt în pixeli de referință (640×360), cu originea în colțul stânga-jos al părintelui.
    public static class UiKit
    {
        static readonly Dictionary<UiFont, TMP_FontAsset> Fonts = new Dictionary<UiFont, TMP_FontAsset>();

        // Fonturi pixel cu licență OFL (vezi Resources/Fonts/*/OFL.txt și tools/art/fonts.md).
        // Mărimea e cea "nativă": pixelii literelor cad exact pe grilă doar la ea sau la multipli întregi.
        static readonly Dictionary<UiFont, (string path, int size)> FontFiles = new Dictionary<UiFont, (string, int)>
        {
            { UiFont.Body, ("Fonts/Micro5/Micro5-Regular", 11) },
            { UiFont.Tiny, ("Fonts/Micro5/Micro5-Regular", 11) },
            { UiFont.Label, ("Fonts/Silkscreen/Silkscreen-Regular", 8) },
            { UiFont.LabelBold, ("Fonts/Silkscreen/Silkscreen-Bold", 8) },
            { UiFont.Title, ("Fonts/JacquardaBastarda9/JacquardaBastarda9-Regular", 13) },
            { UiFont.Big, ("Fonts/PixelifySans/PixelifySans-Variable", 20) },
        };

        public static int NativeSize(UiFont font) => FontFiles[font].size;

        public static TMP_FontAsset Font(UiFont font)
        {
            if (Fonts.TryGetValue(font, out var asset) && asset != null) return asset;
            var (path, size) = FontFiles[font];
            // Două enum-uri pot folosi același fișier (Body și Tiny): refolosim asset-ul.
            foreach (var pair in FontFiles)
                if (pair.Key != font && pair.Value.path == path && pair.Value.size == size && Fonts.TryGetValue(pair.Key, out var shared) && shared != null)
                    return Fonts[font] = shared;
            var ttf = Resources.Load<Font>(path);
            if (ttf == null) { Debug.LogWarning("[UiKit] lipsește fontul " + path); return TMP_Settings.defaultFontAsset; }
            // RASTER_HINTED = glife desenate ca pixeli, nu ca SDF: pentru fonturi pixel arată tăios.
            asset = TMP_FontAsset.CreateFontAsset(ttf, size, 2, GlyphRenderMode.RASTER_HINTED, 512, 512);
            if (asset == null) { Debug.LogWarning("[UiKit] nu pot crea fontul " + path); return TMP_Settings.defaultFontAsset; }
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

        // Un dreptunghi care umple tot părintele (fundaluri, ecrane întregi).
        public static RectTransform Stretch(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            return rt;
        }

        // Imagine la mărimea ei nativă (1 pixel de artă = 1 unitate UI), dacă nu primește altă mărime.
        public static Image Image(Transform parent, Sprite sprite, Vector2 pos, Vector2? size = null, string name = "Image")
        {
            var rt = Rect(parent, name, pos, size ?? (sprite != null ? sprite.rect.size : new Vector2(16, 16)));
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.preserveAspect = false;
            // Sprite-urile cu margini 9-slice (din ui_slices.json) se întind fără să-și deformeze colțurile.
            // "tiled" (hârtia) repetă marginile rupte în loc să le întindă.
            if (sprite != null && sprite.border != Vector4.zero)
                img.type = IsTiled(sprite.name) ? UnityEngine.UI.Image.Type.Tiled : UnityEngine.UI.Image.Type.Sliced;
            return img;
        }

        public static Image Image(Transform parent, string spritePath, Vector2 pos, Vector2? size = null)
            => Image(parent, SpriteLibrary.Get(spritePath), pos, size, System.IO.Path.GetFileName(spritePath));

        // Dreptunghi plin de o culoare (fără sprite). Nu prinde click-uri, decât dacă ceri.
        public static Image Fill(Transform parent, Color color, Vector2 pos, Vector2 size, Vector2? pivot = null, bool raycast = false, string name = "Fill")
        {
            var rt = Rect(parent, name, pos, size, pivot);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = raycast;
            return img;
        }

        // Panou din sprite-urile UI (panel_paper = pergament, panel_dark = lemn închis, tooltip).
        public static Image Panel(Transform parent, string spriteName, Vector2 pos, Vector2 size, Vector2? pivot = null)
        {
            var img = Image(parent, SpriteLibrary.Get("Sprites/UI/" + spriteName), pos, size, spriteName);
            if (pivot.HasValue) img.rectTransform.pivot = pivot.Value;
            return img;
        }

        // Fundal semi-transparent pe tot ecranul, care oprește click-urile spre ce e dedesubt (ecran "modal").
        public static Image Dim(Transform parent, float alpha, string name = "Dim")
        {
            var rt = Rect(parent, name, new Vector2(UIRoot.RefWidth / 2f, UIRoot.RefHeight / 2f), new Vector2(UIRoot.RefWidth * 4, UIRoot.RefHeight * 4));
            var img = rt.gameObject.AddComponent<Image>();
            img.color = new Color(0.02f, 0.01f, 0.01f, alpha);
            img.raycastTarget = true;
            return img;
        }

        public static TextMeshProUGUI Text(Transform parent, string text, Vector2 pos, Vector2 size, UiFont font = UiFont.Body,
            Color? color = null, TextAlignmentOptions align = TextAlignmentOptions.Center, int sizeMultiplier = 1)
        {
            var rt = Rect(parent, "Text", pos, size);
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            t.font = Font(font);
            t.fontSize = NativeSize(font) * sizeMultiplier;
            t.color = color ?? Palette.Ink;
            t.alignment = align;
            t.textWrappingMode = TextWrappingModes.Normal;
            t.overflowMode = TextOverflowModes.Overflow;
            t.richText = true;
            t.raycastTarget = false;
            t.text = text;
            return t;
        }

        // Text cu o umbră de 1 pixel dedesubt: se citește și peste fundaluri întunecate și peste lumea 3D.
        public static ShadowText Label(Transform parent, string text, Vector2 pos, Vector2 size, UiFont font = UiFont.Label,
            Color? color = null, TextAlignmentOptions align = TextAlignmentOptions.Center, int sizeMultiplier = 1, Color? shadow = null)
        {
            var rt = Rect(parent, "Label", pos, size);
            var back = Text(rt, text, size / 2f + new Vector2(sizeMultiplier, -sizeMultiplier), size, font, shadow ?? Palette.Shadow, align, sizeMultiplier);
            var front = Text(rt, text, size / 2f, size, font, color ?? Palette.Parchment, align, sizeMultiplier);
            return new ShadowText(rt, front, back);
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
                selectedSprite = SpriteLibrary.Get("Sprites/UI/button_normal"),
            };
            b.onClick.AddListener(() => { Audio.AudioManager.Play("ui_click"); onClick?.Invoke(); });
            PointerEvents.Of(img.gameObject).OnEnter = () => { if (b.interactable) Audio.AudioManager.Play("ui_hover"); };
            Text(img.transform, label, size / 2f, size, UiFont.Label, Palette.Ink);
            return b;
        }

        // Buton cu text mare (×2) pentru meniuri.
        public static Button BigButton(Transform parent, string label, Vector2 pos, Vector2 size, Action onClick)
        {
            var b = Button(parent, "", pos, size, onClick);
            Text(b.transform, label, size / 2f + new Vector2(0, 1), size, UiFont.Label, Palette.Ink, TextAlignmentOptions.Center, 2);
            return b;
        }

        // ---- ui_slices.json: care panouri se repetă (tiled) și care se întind (sliced) ----

        [Serializable] sealed class SliceEntry { public string sprite; public string drawMode; }
        [Serializable] sealed class SliceFile { public List<SliceEntry> slices = new List<SliceEntry>(); }

        static HashSet<string> _tiled;

        static bool IsTiled(string spriteName)
        {
            if (_tiled == null)
            {
                _tiled = new HashSet<string>();
                var json = Resources.Load<TextAsset>("Sprites/UI/ui_slices");
                if (json != null)
                {
                    var file = JsonUtility.FromJson<SliceFile>(json.text);
                    foreach (var e in file.slices) if (e.drawMode == "tiled") _tiled.Add(e.sprite);
                }
            }
            return _tiled.Contains(spriteName);
        }
    }

    // Un text cu umbră = două texte suprapuse; schimbi textul și culoarea dintr-un singur loc.
    public sealed class ShadowText
    {
        public readonly RectTransform Rect;
        public readonly TextMeshProUGUI Front;
        public readonly TextMeshProUGUI Back;

        public ShadowText(RectTransform rect, TextMeshProUGUI front, TextMeshProUGUI back) { Rect = rect; Front = front; Back = back; }

        public string Text
        {
            get => Front.text;
            set { Front.text = value; Back.text = value; }
        }

        public Color Color
        {
            get => Front.color;
            set { Front.color = value; var b = Back.color; b.a = value.a; Back.color = b; }
        }

        public float Alpha
        {
            set { var f = Front.color; f.a = value; Front.color = f; var b = Back.color; b.a = value; Back.color = b; }
        }
    }

    // Culorile UI din docs/ART.md, ca să nu scriem hex-uri peste tot.
    public static class Palette
    {
        public static readonly Color Ink = Hex("#2a1b13");
        public static readonly Color Parchment = Hex("#e8dcc4");
        public static readonly Color ParchmentDark = Hex("#c8b89a");
        public static readonly Color Gold = Hex("#e0b04a");
        public static readonly Color GoldDark = Hex("#a8782a");
        public static readonly Color Blood = Hex("#d33c3c");
        public static readonly Color BloodDark = Hex("#9e1b1b");
        public static readonly Color Block = Hex("#6fa8dc");
        public static readonly Color Poison = Hex("#7cc04a");
        public static readonly Color Stun = Hex("#f2d14a");
        public static readonly Color Weak = Hex("#9a8fb0");
        public static readonly Color Mark = Hex("#ff8a3d");
        public static readonly Color White = Hex("#fff0e8");
        public static readonly Color Outline = Hex("#1b0f0f");
        public static readonly Color Shadow = new Color(0.106f, 0.059f, 0.059f, 0.9f);

        public static Color Hex(string hex) => ColorUtility.TryParseHtmlString(hex, out var c) ? c : Color.magenta;

        public static Color WithAlpha(Color c, float a) { c.a = a; return c; }
    }
}
