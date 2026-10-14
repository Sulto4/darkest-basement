using System;
using System.Collections.Generic;
using DarkestBasement.Audio;
using DarkestBasement.Core;
using DarkestBasement.Model.Combat;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace DarkestBasement.UI
{
    // Unelte comune pentru UI-ul de luptă: text pixel, panouri 9-slice, poziția mouse-ului, statusuri.
    // Coordonatele sunt pixeli de referință (640×360), originea jos-stânga, ca în UiKit.
    public static class CombatUi
    {
        public const int W = UIRoot.RefWidth;
        public const int H = UIRoot.RefHeight;

        public static readonly Color Ink = Palette.Ink;
        public static readonly Color Paper = Palette.Parchment;
        public static readonly Color Shadow = Palette.Hex("#1b0f0f");
        public static readonly Color Gold = Palette.Gold;
        public static readonly Color Upgrade = Palette.Hex("#7cc04a");

        // ---------- text ----------

        public static TextMeshProUGUI Text(Transform parent, string text, Vector2 pos, Vector2 size, UiFont font, Color color,
            TextAlignmentOptions align = TextAlignmentOptions.Center, int sizeMultiplier = 1)
        {
            var t = UiKit.Text(parent, text, pos, size, font, color, align, sizeMultiplier);
            t.richText = true;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.overflowMode = TextOverflowModes.Overflow;
            t.margin = Vector4.zero;
            return t;
        }

        // Text cu contur de 1 px (4 copii închise, decalate sus/jos/stânga/dreapta, sub textul principal).
        // Fonturile bitmap nu au contur „din shader”, așa că îl facem ca pe vremuri.
        public static OutlinedText Outlined(Transform parent, string text, Vector2 pos, Vector2 size, UiFont font, Color color,
            int sizeMultiplier = 1, TextAlignmentOptions align = TextAlignmentOptions.Center)
        {
            var root = UiKit.Rect(parent, "OutlinedText", pos, size);
            var o = new OutlinedText { Rect = root };
            Vector2[] offsets = { new Vector2(-1, 0), new Vector2(1, 0), new Vector2(0, -1), new Vector2(0, 1), Vector2.zero };
            o.Parts = new TextMeshProUGUI[offsets.Length];
            for (int i = 0; i < offsets.Length; i++)
            {
                bool main = i == offsets.Length - 1;
                var t = Text(root, text, size / 2f + offsets[i] * sizeMultiplier, size, font, main ? color : Shadow, align, sizeMultiplier);
                // Copiile de contur nu primesc rich text colorat (ar colora și conturul).
                if (!main) t.richText = false;
                o.Parts[i] = t;
            }
            o.Text = text;   // conturul primește textul fără etichete de culoare
            return o;
        }

        // ---------- imagini și panouri ----------

        public static Image Image(Transform parent, Sprite sprite, Vector2 pos, Vector2? size = null, string name = "Image", bool raycast = false)
        {
            var img = UiKit.Image(parent, sprite, pos, size, name);
            img.raycastTarget = raycast;
            if (sprite != null && sprite.border != Vector4.zero) img.type = UnityEngine.UI.Image.Type.Sliced;
            FixPixelScale(img);
            return img;
        }

        public static Image Image(Transform parent, string spritePath, Vector2 pos, Vector2? size = null, bool raycast = false)
            => Image(parent, SpriteLibrary.Get(spritePath), pos, size, System.IO.Path.GetFileName(spritePath), raycast);

        // Panou 9-slice (marginile vin din ui_slices.json; colțurile nu se deformează la întindere).
        public static Image Panel(Transform parent, string uiSprite, Vector2 pos, Vector2 size, bool raycast = false)
        {
            var sprite = Sliced(uiSprite, out bool tiled);
            var img = UiKit.Image(parent, sprite, pos, size, uiSprite);
            img.type = tiled ? UnityEngine.UI.Image.Type.Tiled : UnityEngine.UI.Image.Type.Sliced;
            img.raycastTarget = raycast;
            FixPixelScale(img);
            return img;
        }

        // UI-ul Unity împarte marginile 9-slice la (PPU sprite / PPU referință al canvas-ului).
        // Sprite-urile noastre au PPU 32, canvas-ul are implicit 100 → marginile ar ieși de 3× mai groase.
        // Multiplicatorul readuce raportul la 1 pixel de artă = 1 unitate UI.
        public static void FixPixelScale(Image img)
        {
            if (img == null || img.sprite == null) return;
            float reference = img.canvas != null ? img.canvas.referencePixelsPerUnit : 100f;
            img.pixelsPerUnitMultiplier = reference / img.sprite.pixelsPerUnit;
        }

        [Serializable] sealed class SliceEntry { public string sprite; public int left, right, top, bottom; public string drawMode; }
        [Serializable] sealed class SliceFile { public SliceEntry[] slices; }

        static Dictionary<string, SliceEntry> _slices;
        static readonly Dictionary<string, Sprite> SlicedCache = new Dictionary<string, Sprite>();

        public static Sprite Sliced(string uiSprite) => Sliced(uiSprite, out _);

        public static Sprite Sliced(string uiSprite, out bool tiled)
        {
            LoadSlices();
            tiled = _slices.TryGetValue(uiSprite, out var e) && e.drawMode == "tiled";
            var src = SpriteLibrary.Get("Sprites/UI/" + uiSprite);
            if (src == null || e == null || src.border != Vector4.zero || src.texture == null) return src;
            if (SlicedCache.TryGetValue(uiSprite, out var s)) return s;
            // Recreăm sprite-ul cu margini (stânga, jos, dreapta, sus), independent de setările de import.
            s = Sprite.Create(src.texture, src.rect, new Vector2(0.5f, 0.5f), src.pixelsPerUnit, 0, SpriteMeshType.FullRect,
                new Vector4(e.left, e.bottom, e.right, e.top));
            s.name = uiSprite + "_sliced";
            SlicedCache[uiSprite] = s;
            return s;
        }

        static void LoadSlices()
        {
            if (_slices != null) return;
            _slices = new Dictionary<string, SliceEntry>();
            var text = Resources.Load<TextAsset>("Sprites/UI/ui_slices");
            if (text == null) return;
            var file = JsonUtility.FromJson<SliceFile>(text.text);
            if (file?.slices == null) return;
            foreach (var e in file.slices) if (!string.IsNullOrEmpty(e.sprite)) _slices[e.sprite] = e;
        }

        // Buton din pergament (button_normal / hover / pressed), cu click sonor.
        public static Button Button(Transform parent, string label, Vector2 pos, Vector2 size, Action onClick, UiFont font = UiFont.Label)
        {
            var img = Panel(parent, "button_normal", pos, size, true);
            img.name = "Button " + label;
            var b = img.gameObject.AddComponent<Button>();
            b.transition = Selectable.Transition.SpriteSwap;
            b.spriteState = new SpriteState
            {
                highlightedSprite = Sliced("button_hover"),
                pressedSprite = Sliced("button_pressed"),
                selectedSprite = Sliced("button_normal"),
                disabledSprite = Sliced("button_pressed"),
            };
            var nav = b.navigation; nav.mode = Navigation.Mode.None; b.navigation = nav;
            b.onClick.AddListener(() => { AudioManager.Play("ui_click"); onClick?.Invoke(); });
            CombatHoverTrigger.On(img.gameObject, () => { if (b.interactable) AudioManager.Play("ui_hover", 0.5f); }, null);
            Text(img.transform, label, size / 2f, size, font, Ink);
            return b;
        }

        // ---------- mouse ----------

        // Poziția mouse-ului în pixeli de referință (0..640, 0..360).
        public static Vector2 MouseUI()
        {
            var mouse = Mouse.current;
            if (mouse == null || UIRoot.Instance == null) return new Vector2(-999f, -999f);
            Vector2 screen = mouse.position.ReadValue();
            RectTransformUtility.ScreenPointToLocalPointInRectangle(UIRoot.Instance.Root, screen, null, out var local);
            return local + new Vector2(W / 2f, H / 2f);
        }

        public static bool MouseOverUI()
        {
            var es = EventSystem.current;
            return es != null && es.IsPointerOverGameObject();
        }

        public static Vector2 Snap(Vector2 v) => new Vector2(Mathf.Round(v.x), Mathf.Round(v.y));

        // ---------- statusuri și intenții ----------

        public static Sprite StatusIcon(Status s) => SpriteLibrary.Get("Sprites/UI/status_" + s.ToString().ToLowerInvariant());

        public static Color StatusColor(Status s)
        {
            switch (s)
            {
                case Status.Block: return Palette.Block;
                case Status.Bleed: return Palette.Blood;
                case Status.Poison: return Palette.Poison;
                case Status.Stun: return Palette.Stun;
                case Status.Weak: return Palette.Weak;
                case Status.Mark: return Palette.Mark;
                case Status.Strength: return Palette.Hex("#e05050");
                default: return Palette.Hex("#c0c0c0");
            }
        }

        // Explicațiile din tooltip (regula 1 din GDD §10: jucătorul înțelege mereu de ce).
        public static string StatusText(Status s, int stacks, bool permanent = false)
        {
            switch (s)
            {
                case Status.Block: return "Absorbs the next " + stacks + " damage. Lost at the start of this unit's turn.";
                case Status.Bleed: return "Loses " + stacks + " HP at the start of its turn, then Bleed drops by 1.";
                case Status.Poison: return "Loses " + stacks + " HP at the start of its turn, then Poison drops by 1.";
                case Status.Stun: return "Skips its next turn.";
                case Status.Weak: return "Deals 25% less damage. " + Turns(stacks);
                case Status.Mark: return "Takes 50% more damage from every hit. " + Turns(stacks);
                case Status.Strength: return "Deals +" + stacks + " damage with every hit, until the fight ends.";
                default: return permanent ? "Enemies must attack this unit. Permanent." : "Enemies must attack this unit. " + Turns(stacks);
            }
        }

        static string Turns(int n) => n == 1 ? "1 turn left." : n + " turns left.";

        public static Sprite IntentIcon(IntentKind k)
        {
            string name;
            switch (k)
            {
                case IntentKind.Attack: name = "intent_attack"; break;
                case IntentKind.AttackAll: name = "intent_attack_aoe"; break;
                case IntentKind.Block: name = "intent_block"; break;
                case IntentKind.Debuff: name = "intent_debuff"; break;
                case IntentKind.Buff: name = "intent_buff"; break;
                case IntentKind.Summon: name = "intent_summon"; break;
                default: name = "intent_unknown"; break;
            }
            return SpriteLibrary.Get("Sprites/UI/" + name);
        }

        public static string HexOf(Color c) => "#" + ColorUtility.ToHtmlStringRGB(c);
    }

    // Textul cu contur: părțile se schimbă împreună.
    public sealed class OutlinedText
    {
        public RectTransform Rect;
        public TextMeshProUGUI[] Parts;
        public TextMeshProUGUI Main => Parts[Parts.Length - 1];

        public string Text
        {
            get => Main.text;
            set
            {
                // Conturul primește textul fără etichete de culoare, altfel s-ar colora și el.
                string plain = StripColor(value);
                for (int i = 0; i < Parts.Length; i++) Parts[i].text = i == Parts.Length - 1 ? value : plain;
            }
        }

        public Color Color { get => Main.color; set => Main.color = value; }

        public float Alpha
        {
            set { foreach (var p in Parts) { var c = p.color; c.a = value; p.color = c; } }
        }

        static string StripColor(string s)
        {
            if (string.IsNullOrEmpty(s) || s.IndexOf('<') < 0) return s;
            var sb = new System.Text.StringBuilder(s.Length);
            bool inTag = false;
            foreach (char ch in s)
            {
                if (ch == '<') { inTag = true; continue; }
                if (ch == '>') { inTag = false; continue; }
                if (!inTag) sb.Append(ch);
            }
            return sb.ToString();
        }
    }
}
