using System;
using System.Collections.Generic;
using System.Text;
using DarkestBasement.Audio;
using DarkestBasement.Core;
using DarkestBasement.Model.Core;
using DarkestBasement.Model.Data;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DarkestBasement.UI
{
    public enum CardGlow { None, Playable, Selected, Target }

    // O carte, 64×90 pixeli de artă, construită din bucăți (vezi „card” în Sprites/UI/ui_slices.json):
    // ramă pe culoarea eroului, ilustrația (56×36), bulina de cost, numele, textul regulilor, bijuteria de raritate.
    // Folosită în mâna din luptă (CombatHand) și de ecranele echipei W (Treasure, Bedroom) prin Create/OnClicked/SetHighlighted.
    public sealed class CardView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler,
        IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        public const int Width = 64, Height = 90;

        // ---- API public (contract cu echipa W) ----
        public CardDef Def;
        public bool Upgraded;
        public Action<CardView> OnClicked;

        // ---- extensii pentru mâna din luptă ----
        public CardInstance Instance { get; private set; }
        public Action<CardView, bool> HoverChanged;
        public Action<CardView> DragBegan, DragEnded;
        public Action<CardView, Vector2> Dragged;
        public bool ManagedByHand;        // poziția/rotația le controlează mâna, nu hover-ul implicit
        public bool Interactable = true;

        public RectTransform Rect { get; private set; }
        public RectTransform Visual { get; private set; }   // partea care se înclină și se mărește
        public bool Playable { get; private set; } = true;
        public string Reason { get; private set; } = "";
        public bool IsHovered { get; private set; }
        public bool IsDragging { get; private set; }

        CanvasGroup _group;
        Image _glow, _frame, _art, _orb, _gem, _plate, _hoverPad;
        Image[] _rankPips;
        TextMeshProUGUI _name, _text;
        OutlinedText _cost;
        CardGlow _glowKind;
        TweenHandle _hoverTween;
        Vector2 _restPos;
        bool _highlighted;

        public static CardView Create(Transform parent, CardDef def, bool upgraded, Vector2 pos)
        {
            var rt = UiKit.Rect(parent, "Card " + (def != null ? def.Id : "?"), pos, new Vector2(Width, Height));
            var v = rt.gameObject.AddComponent<CardView>();
            v.Rect = rt;
            v.Def = def;
            v.Upgraded = upgraded;
            v._restPos = pos;
            v.Build();
            return v;
        }

        public static CardView Create(Transform parent, CardInstance card, Vector2 pos)
        {
            var v = Create(parent, card.Def, card.Upgraded, pos);
            v.Instance = card;
            return v;
        }

        // ---------- construcție ----------

        void Build()
        {
            _group = gameObject.AddComponent<CanvasGroup>();
            // Imaginea invizibilă de 64×90 prinde mouse-ul pe toată cartea (copiii nu prind nimic).
            var hit = gameObject.AddComponent<Image>();
            hit.color = new Color(0f, 0f, 0f, 0f);
            hit.raycastTarget = true;

            // Când cartea urcă la hover, sub ea rămâne o „pernă” invizibilă: mouse-ul nu iese din carte doar pentru că ea s-a ridicat.
            _hoverPad = UiKit.Rect(transform, "HoverPad", new Vector2(Width / 2f, -14f), new Vector2(Width, 28)).gameObject.AddComponent<Image>();
            _hoverPad.color = new Color(0f, 0f, 0f, 0f);
            _hoverPad.enabled = false;
            Visual = UiKit.Rect(transform, "Visual", new Vector2(Width / 2f, Height / 2f), new Vector2(Width, Height));
            // Coordonatele de mai jos sunt în pixeli față de colțul stânga-jos al cărții.
            _glow = CombatUi.Image(Visual, "Sprites/UI/card_glow", new Vector2(32, 45));
            _glow.enabled = false;
            _frame = CombatUi.Image(Visual, "Sprites/UI/card_frame_" + (Def != null ? Def.HeroId : "isaac"), new Vector2(32, 45));

            // Ilustrația: fereastra de 56×36 care începe la (4,4) de sus → centru (32, 90−4−18).
            var art = Def != null ? SpriteLibrary.TryGet("Sprites/Cards/" + Def.Id) : null;
            if (art != null) _art = CombatUi.Image(Visual, art, new Vector2(32, 68), new Vector2(56, 36), "Art");
            else
            {
                // Fără ilustrație încă: portretul eroului pe fondul ferestrei (nu un pătrat magenta pe o carte).
                var portrait = Def != null ? SpriteLibrary.TryGet("Sprites/Heroes/" + Def.HeroId + "/portrait") : null;
                if (portrait != null) _art = CombatUi.Image(Visual, portrait, new Vector2(32, 68), new Vector2(24, 24), "Art");
            }

            BuildName();
            BuildRankPips();
            BuildText();

            // Raritatea: bijuteria din soclul de jos (8×8 la (28,82) de sus → centru (32, 4)).
            string gem = Def == null || Def.Rarity == Rarity.Starter || Def.Rarity == Rarity.Common ? "gem_common" : Def.Rarity == Rarity.Uncommon ? "gem_uncommon" : "gem_rare";
            _gem = CombatUi.Image(Visual, "Sprites/UI/" + gem, new Vector2(32, 4));

            // Costul: bulina (lacrima) de 16×16 iese peste colțul stânga-sus (la −5,−5).
            _orb = CombatUi.Image(Visual, "Sprites/UI/cost_orb", new Vector2(3, 87));
            int cost = Def != null ? Def.EnergyCost(Upgraded) : 0;
            bool cheaper = Def != null && cost < Def.Cost;
            _cost = CombatUi.Outlined(Visual, cost.ToString(), new Vector2(3, 86), new Vector2(16, 12), UiFont.LabelBold, cheaper ? CombatUi.Upgrade : Palette.White);
        }

        // Rangurile ca în Darkest Dungeon: 4 buline stânga = din ce poziții se joacă (4-3-2-1, frontul e în dreapta),
        // 4 buline dreapta = pe ce ranguri lovește (1-2-3-4), doar la cărțile care țintesc inamici.
        void BuildRankPips()
        {
            if (Def == null) return;
            // Dacă plăcuța numelui s-a înălțat (nume pe două rânduri), bulinele urcă și ele.
            float y = _plate != null ? 59f : 54f;
            var list = new List<Image>();
            for (int i = 0; i < 4; i++)
            {
                int rank = 4 - i;
                var pip = CombatUi.Image(Visual, Def.CanUseRank(rank) ? "Sprites/UI/rank_pip_on" : "Sprites/UI/rank_pip_off", new Vector2(8 + i * 6, y));
                list.Add(pip);
            }
            if (Def.TargetType == TargetType.Enemy || Def.TargetType == TargetType.AllEnemies)
            {
                for (int i = 0; i < 4; i++)
                {
                    int rank = i + 1;
                    bool on = Def.CanTargetRank(rank);
                    var pip = CombatUi.Image(Visual, on ? "Sprites/UI/rank_pip_on" : "Sprites/UI/rank_pip_off", new Vector2(38 + i * 6, y));
                    if (on) pip.color = new Color(1f, 0.45f, 0.4f);
                    list.Add(pip);
                }
            }
            _rankPips = list.ToArray();
        }

        // Numele pe plăcuța de 56×9 (de la y=39 la 48). Încercăm, în ordine:
        // 1) Micro 5 la mărimea nativă; 2) spații mai înguste între cuvinte; 3) două rânduri pe o plăcuță mai înaltă.
        // Niciodată mărimi de font fracționare: literele pixel s-ar înmuia.
        void BuildName()
        {
            string name = Def != null ? Def.Name + (Upgraded ? "+" : "") : "";
            Color color = Upgraded ? CombatUi.Upgrade : Palette.Parchment;
            _name = CombatUi.Text(Visual, name, new Vector2(32, 44), new Vector2(58, 9), UiFont.Body, color);
            const float max = 56f;
            float width = _name.GetPreferredValues(name).x;
            if (width <= max) return;

            int spaces = 0; foreach (char ch in name) if (ch == ' ') spaces++;
            float px = 100f / _name.fontSize;   // 1 pixel în unitățile TMP (em/100)
            if (spaces > 0 && width - spaces * 2f <= max)
            {
                _name.wordSpacing = -2f * px;
                return;
            }

            // Două rânduri: împărțim la spațiul cel mai apropiat de mijloc și urcăm plăcuța cu 6 px peste ilustrație.
            int split = -1, best = int.MaxValue;
            for (int i = 0; i < name.Length; i++)
                if (name[i] == ' ' && Mathf.Abs(i - name.Length / 2) < best) { best = Mathf.Abs(i - name.Length / 2); split = i; }
            if (split < 0) return;
            _plate = CopyPlate();
            _name.text = name.Substring(0, split) + "\n" + name.Substring(split + 1);
            _name.rectTransform.anchoredPosition = new Vector2(32, 47);
            _name.rectTransform.sizeDelta = new Vector2(60, 15);
            SetLinePitch(_name, 7);
        }

        // Copie a benzii de nume din ramă (rândurile 42..50 de sus), pusă cu 6 px mai sus: plăcuța pare mai înaltă.
        static readonly Dictionary<Sprite, Sprite> Bands = new Dictionary<Sprite, Sprite>();

        Image CopyPlate()
        {
            var frame = _frame.sprite;
            if (frame == null || frame.texture == null) return null;
            // Un sprite per ramă (nu per carte): cărțile se creează des, sprite-urile nu trebuie să se adune.
            if (!Bands.TryGetValue(frame, out var band))
            {
                Rect r = frame.textureRect;
                band = Sprite.Create(frame.texture, new Rect(r.x + 4, r.y + 39, 56, 9), new Vector2(0.5f, 0.5f), frame.pixelsPerUnit, 0, SpriteMeshType.FullRect);
                Bands[frame] = band;
            }
            var img = CombatUi.Image(Visual, band, new Vector2(32, 49.5f), new Vector2(56, 9), "NamePlateExtension");
            img.transform.SetSiblingIndex(_name.transform.GetSiblingIndex());
            return img;
        }

        void BuildText()
        {
            string body = Def != null ? Describe(Def, Upgraded) : "";
            // Textul regulilor: zona 56×31 de la y=6; primul rând începe la 2 px sub marginea de sus.
            _text = CombatUi.Text(Visual, body, new Vector2(32, 21), new Vector2(54, 29), UiFont.Body, Palette.Ink, TextAlignmentOptions.Top);
            _text.textWrappingMode = TextWrappingModes.Normal;
            SetLinePitch(_text, 7);
            float height = _text.GetPreferredValues(body, 54f, 0f).y;
            if (height > 30f)
            {
                // Ultima soluție pentru textele foarte lungi: prescurtăm cuvintele frecvente.
                body = body.Replace(" damage", " dmg").Replace("Discard your hand", "Discard hand");
                _text.text = body;
            }
        }

        // Distanța dintre rânduri, în pixeli întregi (TMP adaugă lineSpacing × fontSize/100 peste înălțimea fontului).
        static void SetLinePitch(TextMeshProUGUI t, int pixels)
        {
            var face = t.font != null ? t.font.faceInfo : default;
            if (face.pointSize <= 0) return;
            float natural = face.lineHeight * t.fontSize / face.pointSize * face.scale;
            t.lineSpacing = (pixels - natural) / (t.fontSize * 0.01f);
        }

        // Textul generat de model (CardText.Describe), cu cuvintele-cheie colorate și cifrele îmbunătățite în verde.
        public static string Describe(CardDef def, bool upgraded)
        {
            string text = CardText.Describe(def, upgraded);
            if (upgraded)
            {
                string[] baseWords = CardText.Describe(def, false).Split(' ');
                string[] words = text.Split(' ');
                if (baseWords.Length == words.Length)
                {
                    var sb = new StringBuilder();
                    for (int i = 0; i < words.Length; i++)
                    {
                        if (i > 0) sb.Append(' ');
                        sb.Append(words[i] != baseWords[i] ? "<color=#3f8a2a>" + words[i] + "</color>" : words[i]);
                    }
                    text = sb.ToString();
                }
            }
            return Keywords(text);
        }

        static readonly (string word, string color)[] KeywordColors =
        {
            ("Bleed", "#a82424"), ("Poison", "#3f7a1f"), ("Block", "#2f6aa8"), ("Weak", "#6a5a8a"), ("Mark", "#b85a14"),
            ("Strength", "#a82424"), ("Stun", "#8a6a00"), ("Taunt", "#5a5a5a"), ("crit", "#8a6a00"), ("energy", "#2f6aa8"),
        };

        static string Keywords(string text)
        {
            foreach (var (word, color) in KeywordColors) text = text.Replace(word, "<color=" + color + ">" + word + "</color>");
            return text;
        }

        // ---------- stări vizuale ----------

        // Folosit de ecranele W (Treasure, Bedroom) pentru cartea aleasă / sub mouse.
        public void SetHighlighted(bool on)
        {
            _highlighted = on;
            SetGlow(on ? CardGlow.Selected : (_glowKind == CardGlow.Selected ? CardGlow.None : _glowKind));
        }

        public void SetGlow(CardGlow glow)
        {
            _glowKind = glow;
            if (_highlighted && glow == CardGlow.None) glow = CardGlow.Selected;
            string sprite = glow == CardGlow.Selected ? "card_glow_selected" : glow == CardGlow.Playable ? "card_glow_playable" : "card_glow";
            _glow.sprite = SpriteLibrary.Get("Sprites/UI/" + sprite);
            _glow.color = glow == CardGlow.Target ? new Color(1f, 0.45f, 0.35f) : Color.white;
            _glow.enabled = glow != CardGlow.None;
        }

        // Cărțile care nu se pot juca devin gri; motivul apare în tooltip („Needs rank 3–4”).
        public void SetPlayable(bool playable, string reason)
        {
            Playable = playable;
            Reason = reason ?? "";
            Color tint = playable ? Color.white : new Color(0.5f, 0.48f, 0.46f);
            _frame.color = tint;
            if (_art != null) _art.color = tint;
            _orb.color = tint;
            _gem.color = tint;
            if (_plate != null) _plate.color = tint;
            _text.alpha = playable ? 1f : 0.6f;
            _name.alpha = playable ? 1f : 0.7f;
        }

        public float Alpha { get => _group.alpha; set => _group.alpha = value; }

        public void SetHoverPad(bool on) { _hoverPad.enabled = on; }

        public bool BlocksRaycasts { set => _group.blocksRaycasts = value; }

        // Scuturare scurtă („nu se poate”).
        public void Shake()
        {
            var v = Visual;
            Tween.Value(0.25f, k => { if (v != null) v.anchoredPosition = new Vector2(Width / 2f + Mathf.Round(Mathf.Sin(k * 40f) * 3f * (1f - k)), Height / 2f); },
                Ease.Linear, () => { if (v != null) v.anchoredPosition = new Vector2(Width / 2f, Height / 2f); }, 0f, true, v);
        }

        // ---------- mouse ----------

        public void OnPointerEnter(PointerEventData e)
        {
            if (!Interactable) return;
            IsHovered = true;
            HoverChanged?.Invoke(this, true);
            if (!ManagedByHand) DefaultHover(true);
        }

        public void OnPointerExit(PointerEventData e)
        {
            IsHovered = false;
            HoverChanged?.Invoke(this, false);
            if (!ManagedByHand) DefaultHover(false);
        }

        public void OnPointerClick(PointerEventData e)
        {
            if (!Interactable || IsDragging || e.button != PointerEventData.InputButton.Left) return;
            OnClicked?.Invoke(this);
        }

        public void OnBeginDrag(PointerEventData e)
        {
            if (!Interactable || DragBegan == null || e.button != PointerEventData.InputButton.Left) return;
            IsDragging = true;
            DragBegan(this);
        }

        public void OnDrag(PointerEventData e)
        {
            if (IsDragging) Dragged?.Invoke(this, e.position);
        }

        public void OnEndDrag(PointerEventData e)
        {
            if (!IsDragging) return;
            IsDragging = false;
            DragEnded?.Invoke(this);
        }

        // Hover-ul implicit (în afara luptei): cartea urcă puțin și crește.
        void DefaultHover(bool on)
        {
            if (on) AudioManager.Play("ui_hover", 0.5f);
            _hoverTween?.Kill();
            var v = Visual;
            Vector3 fromScale = v.localScale, toScale = Vector3.one * (on ? 1.08f : 1f);
            Vector2 fromPos = v.anchoredPosition, toPos = new Vector2(Width / 2f, Height / 2f + (on ? 4f : 0f));
            _hoverTween = Tween.Value(0.12f, k =>
            {
                if (v == null) return;
                v.localScale = Vector3.LerpUnclamped(fromScale, toScale, k);
                v.anchoredPosition = CombatUi.Snap(Vector2.LerpUnclamped(fromPos, toPos, k));
            }, Ease.OutBack, null, 0f, true, v);
        }

        void OnDisable()
        {
            if (IsHovered) { IsHovered = false; HoverChanged?.Invoke(this, false); }
        }
    }
}
