using System;
using System.Collections.Generic;
using DarkestBasement.Audio;
using DarkestBasement.Core;
using DarkestBasement.Model.Data;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DarkestBasement.UI
{
    // Comoara: 3 cărți pe piedestal, alegi una (GDD §5, cel puțin una Uncommon+).
    // Cărțile sunt CardView-urile echipei C, la scară ×2 (ca pe ecranul de recompensă): e singura alegere de pe ecran,
    // textul trebuie citit dintr-o privire. În spatele fiecăreia stă o aură în culoarea rarității,
    // care prinde și click-ul (merge și cât timp CardView e încă un stub).
    public sealed class TreasureScreen
    {
        public RectTransform Root { get; }
        readonly List<RectTransform> _slots = new List<RectTransform>();
        readonly List<CardView> _cards = new List<CardView>();
        public CardView ChoiceCard(int index) => index >= 0 && index < _cards.Count ? _cards[index] : null;
        readonly Action<int> _onChoose;
        bool _chosen;

        public TreasureScreen(Transform parent, IReadOnlyList<CardDef> choices, Action<int> onChoose)
        {
            _onChoose = onChoose;
            Root = UiKit.Stretch(parent, "TreasureScreen");
            var dim = UiKit.Dim(Root, 0f);
            Tween.Value(0.35f, k => { if (dim != null) dim.color = new Color(0.02f, 0.01f, 0.01f, 0.55f * k); }, Ease.OutQuad, null, 0f, true, dim);

            UiKit.Label(Root, "TREASURE", new Vector2(320, 312), new Vector2(400, 36), UiFont.Title, Palette.Gold, TextAlignmentOptions.Center, 2);
            UiKit.Label(Root, "TAKE ONE CARD", new Vector2(320, 290), new Vector2(300, 10), UiFont.Label, Palette.Parchment);

            for (int i = 0; i < choices.Count; i++)
            {
                var def = choices[i];
                int index = i;
                // Slotul are mărimea cărții ×2 (128×180); centrul cărții e la (64, 90) în slot.
                // y = 172: și ridicată la hover, cartea rămâne sub „TAKE ONE CARD” (y = 290).
                var slot = UiKit.Rect(Root, "Slot" + i, new Vector2(320 + (i - (choices.Count - 1) / 2f) * 150f, 172), new Vector2(128, 180));
                var glow = UiKit.Image(slot, SpriteLibrary.Get("Sprites/UI/card_glow"), new Vector2(64, 90));
                glow.rectTransform.localScale = Vector3.one * 2f;
                glow.color = RarityColor(def.Rarity);
                var card = CardView.Create(slot, def, false, new Vector2(64, 90));
                card.Rect.localScale = Vector3.one * 2f;
                card.OnClicked = _ => Choose(index);
                _cards.Add(card);
                PointerEvents.Of(glow.gameObject).OnClick = () => Choose(index);
                // Cartea (CardView) prinde hover-ul: ridicare, sunet, ramă aurie. Textul se citește direct pe ea.
                card.HoverChanged = (v, on) =>
                {
                    v.SetHighlighted(on);
                    if (on) AudioManager.Play("ui_hover");
                };

                string owner = HeroDatabase.Get(def.HeroId).Name.ToUpperInvariant();
                UiKit.Label(slot, owner, new Vector2(64, -8), new Vector2(120, 10), UiFont.Label, Palette.Parchment);
                UiKit.Label(slot, def.Rarity.ToString().ToUpperInvariant(), new Vector2(64, -19), new Vector2(120, 10), UiFont.Label, RarityColor(def.Rarity));
                if (IsStub(card)) UiKit.Label(slot, def.Name, new Vector2(64, 90), new Vector2(110, 40), UiFont.Body, Palette.Parchment);

                // Cărțile intră una câte una, de jos, cu un mic salt.
                var home = slot.anchoredPosition;
                slot.anchoredPosition = home + new Vector2(0, -220);
                Tween.Value(0.45f, k => { if (slot != null) slot.anchoredPosition = Vector2.LerpUnclamped(home + new Vector2(0, -220), home, k); },
                    Ease.OutBack, null, 0.12f * i, true, slot);
                float phase = i * 1.7f;
                Ticker.Attach(glow.gameObject, () => glow.color = Palette.WithAlpha(glow.color, 0.55f + 0.35f * Mathf.Sin(Core.GameTime.Unscaled * 3f + phase)));
                _slots.Add(slot);
            }
            AudioManager.Play("chest_open");
        }

        public static Color RarityColor(Rarity r)
        {
            switch (r)
            {
                case Rarity.Rare: return Palette.Gold;
                case Rarity.Uncommon: return Palette.Block;
                default: return new Color(0.75f, 0.72f, 0.68f);
            }
        }

        // Stub-ul de CardView nu desenează nimic; atunci scriem noi numele cărții, ca ecranul să rămână folosibil.
        public static bool IsStub(CardView card) => card.GetComponentInChildren<Graphic>(true) == null;

        // Click pe carte (sau QA): cartea aleasă zboară spre portretele eroilor, celelalte cad.
        public void Choose(int index)
        {
            if (_chosen || index < 0 || index >= _slots.Count) return;
            _chosen = true;
            AudioManager.Play("card_play");
            for (int i = 0; i < _slots.Count; i++)
            {
                var slot = _slots[i];
                var from = slot.anchoredPosition;
                if (i == index)
                {
                    Tween.Value(0.55f, k =>
                    {
                        if (slot == null) return;
                        slot.anchoredPosition = Vector2.Lerp(from, new Vector2(60, 330), k);
                        slot.localScale = Vector3.one * Mathf.Lerp(1.1f, 0.2f, k);
                    }, Ease.InCubic, null, 0.15f, true, slot);
                }
                else
                {
                    // Cărțile ×2 au 180 px: cad destul cât să iasă de tot din ecran.
                    Tween.Value(0.4f, k => { if (slot != null) slot.anchoredPosition = from + new Vector2(0, -330f * k); }, Ease.InBack, null, 0f, true, slot);
                }
            }
            var onChoose = _onChoose;
            Tween.Delay(0.75f, () => onChoose?.Invoke(index), true);
        }

        public void Close() { if (Root != null) UnityEngine.Object.Destroy(Root.gameObject); }
    }
}
