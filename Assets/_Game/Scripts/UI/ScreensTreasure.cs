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
    // Cărțile sunt CardView-urile echipei C; în spatele fiecăreia stă o aură în culoarea rarității,
    // care prinde și click-ul (merge și cât timp CardView e încă un stub).
    public sealed class TreasureScreen
    {
        public RectTransform Root { get; }
        readonly List<RectTransform> _slots = new List<RectTransform>();
        readonly Action<int> _onChoose;
        bool _chosen;

        public TreasureScreen(Transform parent, IReadOnlyList<CardDef> choices, Action<int> onChoose)
        {
            _onChoose = onChoose;
            Root = UiKit.Stretch(parent, "TreasureScreen");
            var dim = UiKit.Dim(Root, 0f);
            Tween.Value(0.35f, k => { if (dim != null) dim.color = new Color(0.02f, 0.01f, 0.01f, 0.55f * k); }, Ease.OutQuad, null, 0f, true, dim);

            UiKit.Label(Root, "TREASURE", new Vector2(320, 312), new Vector2(300, 30), UiFont.Title, Palette.Gold, TextAlignmentOptions.Center, 2);
            UiKit.Label(Root, "TAKE ONE CARD", new Vector2(320, 290), new Vector2(300, 10), UiFont.Label, Palette.Parchment);

            for (int i = 0; i < choices.Count; i++)
            {
                var def = choices[i];
                int index = i;
                var slot = UiKit.Rect(Root, "Slot" + i, new Vector2(320 + (i - (choices.Count - 1) / 2f) * 104f, 178), new Vector2(80, 110));
                var glow = UiKit.Image(slot, SpriteLibrary.Get("Sprites/UI/card_glow"), new Vector2(40, 55));
                glow.color = RarityColor(def.Rarity);
                var card = CardView.Create(slot, def, false, new Vector2(40, 55));
                card.OnClicked = _ => Choose(index);
                var events = PointerEvents.Of(glow.gameObject);
                events.OnClick = () => Choose(index);
                events.OnEnter = () => { AudioManager.Play("ui_hover"); card.SetHighlighted(true); };
                events.OnExit = () => card.SetHighlighted(false);
                Tooltip.Attach(glow.gameObject, () => def.Name + "\n" + CardText.Describe(def, false));

                string owner = HeroDatabase.Get(def.HeroId).Name.ToUpperInvariant();
                UiKit.Label(slot, owner, new Vector2(40, -6), new Vector2(100, 10), UiFont.Label, Palette.Parchment);
                UiKit.Label(slot, def.Rarity.ToString().ToUpperInvariant(), new Vector2(40, -17), new Vector2(100, 10), UiFont.Label, RarityColor(def.Rarity));
                if (IsStub(card)) UiKit.Label(slot, def.Name, new Vector2(40, 55), new Vector2(70, 40), UiFont.Body, Palette.Parchment);

                // Cărțile intră una câte una, de jos, cu un mic salt.
                var home = slot.anchoredPosition;
                slot.anchoredPosition = home + new Vector2(0, -220);
                Tween.Value(0.45f, k => { if (slot != null) slot.anchoredPosition = Vector2.LerpUnclamped(home + new Vector2(0, -220), home, k); },
                    Ease.OutBack, null, 0.12f * i, true, slot);
                float phase = i * 1.7f;
                Ticker.Attach(glow.gameObject, () => glow.color = Palette.WithAlpha(glow.color, 0.55f + 0.35f * Mathf.Sin(Time.unscaledTime * 3f + phase)));
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
                    Tween.Value(0.4f, k => { if (slot != null) slot.anchoredPosition = from + new Vector2(0, -260f * k); }, Ease.InBack, null, 0f, true, slot);
                }
            }
            var onChoose = _onChoose;
            Tween.Delay(0.75f, () => onChoose?.Invoke(index), true);
        }

        public void Close() { if (Root != null) UnityEngine.Object.Destroy(Root.gameObject); }
    }
}
