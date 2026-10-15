using System;
using System.Collections.Generic;
using DarkestBasement.Audio;
using DarkestBasement.Core;
using DarkestBasement.Model.Core;
using DarkestBasement.Model.Data;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DarkestBasement.UI
{
    // Dormitorul lui Isaac: alegi UNA din trei (GDD §5) — dormi (vindecare 35%), arunci o carte, sau o îmbunătățești.
    // Pentru ultimele două vezi pachetul fiecărui erou: tab-uri cu portretele, iar dedesubt cărțile lui.
    public sealed class BedroomScreen
    {
        public RectTransform Root { get; }
        readonly RunState _state;
        readonly Action<BedroomAction, string, string> _onChoose;
        RectTransform _page;
        BedroomAction _action;
        string _heroId;
        bool _done;
        // Ce e pe ecran acum (QA-ul apasă exact ce ar apăsa jucătorul).
        readonly Dictionary<BedroomAction, RectTransform> _options = new Dictionary<BedroomAction, RectTransform>();
        readonly Dictionary<string, RectTransform> _tabs = new Dictionary<string, RectTransform>();
        readonly List<CardView> _pickerCards = new List<CardView>();

        public bool PickerOpen { get; private set; }
        // Cartea „după upgrade” afișată cât timp mouse-ul e pe o carte din pachet (null în rest).
        public CardView UpgradePreview { get; private set; }
        RectTransform _preview;
        public string PickerHero => _heroId;
        public RectTransform OptionPanel(BedroomAction action) => _options.TryGetValue(action, out var rt) ? rt : null;
        public RectTransform HeroTab(string heroId) => heroId != null && _tabs.TryGetValue(heroId, out var rt) ? rt : null;
        // Cartea de pe poziția deckIndex din pachetul eroului afișat (null dacă nu se poate alege).
        public CardView PickerCard(int deckIndex) => deckIndex >= 0 && deckIndex < _pickerCards.Count ? _pickerCards[deckIndex] : null;

        public BedroomScreen(Transform parent, RunState state, Action<BedroomAction, string, string> onChoose)
        {
            _state = state;
            _onChoose = onChoose;
            Root = UiKit.Stretch(parent, "BedroomScreen");
            var dim = UiKit.Dim(Root, 0f);
            Tween.Value(0.35f, k => { if (dim != null) dim.color = new Color(0.01f, 0.01f, 0.03f, 0.6f * k); }, Ease.OutQuad, null, 0f, true, dim);
            ShowOptions();
        }

        void NewPage()
        {
            if (_page != null) UnityEngine.Object.Destroy(_page.gameObject);
            Tooltip.Hide();
            HidePreview();
            _options.Clear();
            _tabs.Clear();
            _pickerCards.Clear();
            PickerOpen = false;
            _page = UiKit.Stretch(Root, "Page");
        }

        // Pagina 1: cele trei opțiuni.
        public void ShowOptions()
        {
            NewPage();
            UiKit.Label(_page, "BEDROOM", new Vector2(320, 312), new Vector2(400, 36), UiFont.Title, Palette.Hex("#a9c1ff"), TextAlignmentOptions.Center, 2);
            UiKit.Label(_page, "REST A WHILE. CHOOSE ONE.", new Vector2(320, 290), new Vector2(300, 10), UiFont.Label, Palette.Parchment);

            int healTotal = 0;
            foreach (var h in _state.Party) if (h.IsAlive) healTotal += Math.Min(h.Def.MaxHp - h.Hp, (int)Math.Ceiling(h.Def.MaxHp * 0.35));
            Option(BedroomAction.Heal, 170, "SLEEP", "Heal every living hero for 35% of max HP.", "+" + healTotal + " HP TOTAL", Palette.Poison, ChooseHeal);
            Option(BedroomAction.RemoveCard, 320, "PURGE", "Remove one card from a hero's deck.", "THIN THE DECK", Palette.Blood, () => OpenPicker(BedroomAction.RemoveCard, FirstHero()));
            Option(BedroomAction.UpgradeCard, 470, "STUDY", "Upgrade one card: bigger numbers or lower cost.", "CARD +", Palette.Gold, () => OpenPicker(BedroomAction.UpgradeCard, FirstHero()));
        }

        void Option(BedroomAction action, float x, string title, string text, string footer, Color accent, Action onClick)
        {
            var panel = UiKit.Panel(_page, "panel_paper", new Vector2(x, 176), new Vector2(136, 150));
            var rt = panel.rectTransform;
            _options[action] = rt;
            UiKit.Text(rt, title, new Vector2(68, 122), new Vector2(124, 24), UiFont.Title, Palette.Ink, TextAlignmentOptions.Center, 1);
            UiKit.Fill(rt, accent, new Vector2(68, 104), new Vector2(60, 2));
            UiKit.Text(rt, text, new Vector2(68, 70), new Vector2(108, 50), UiFont.Body, Palette.Ink);
            UiKit.Text(rt, footer, new Vector2(68, 26), new Vector2(120, 10), UiFont.Label, Palette.Ink);
            var events = PointerEvents.Of(panel.gameObject);
            Vector2 home = rt.anchoredPosition;
            events.OnEnter = () => { AudioManager.Play("ui_hover"); rt.anchoredPosition = home + new Vector2(0, 4); panel.color = new Color(1f, 0.97f, 0.88f); };
            events.OnExit = () => { rt.anchoredPosition = home; panel.color = Color.white; };
            events.OnClick = () => { AudioManager.Play("ui_click"); onClick(); };
        }

        string FirstHero()
        {
            foreach (var h in _state.Party) if (h.IsAlive) return h.Def.Id;
            return null;
        }

        // Pagina 2: tab-uri cu eroii vii, iar dedesubt pachetul eroului selectat.
        public void OpenPicker(BedroomAction action, string heroId)
        {
            if (_done || heroId == null) return;
            _action = action;
            _heroId = heroId;
            NewPage();
            PickerOpen = true;
            bool upgrade = action == BedroomAction.UpgradeCard;
            UiKit.Label(_page, upgrade ? "UPGRADE A CARD" : "REMOVE A CARD", new Vector2(320, 318), new Vector2(560, 36), UiFont.Title,
                upgrade ? Palette.Gold : Palette.Blood, TextAlignmentOptions.Center, 2);

            var living = new List<PartyHero>();
            foreach (var h in _state.Party) if (h.IsAlive) living.Add(h);
            for (int i = 0; i < living.Count; i++)
            {
                var hero = living[i];
                float x = 320 + (i - (living.Count - 1) / 2f) * 64f;
                bool selected = hero.Def.Id == heroId;
                var frame = UiKit.Image(_page, SpriteLibrary.Get(selected ? "Sprites/UI/portrait_frame_active" : "Sprites/UI/portrait_frame"), new Vector2(x, 292));
                var portrait = UiKit.Image(_page, SpriteLibrary.Get("Sprites/Heroes/" + hero.Def.Id + "/portrait"), new Vector2(x, 294));
                portrait.raycastTarget = false;
                if (!selected) portrait.color = new Color(0.6f, 0.55f, 0.55f);
                UiKit.Label(_page, hero.Def.Name.ToUpperInvariant() + " " + hero.Deck.Count, new Vector2(x, 268), new Vector2(64, 10), UiFont.Label,
                    selected ? Palette.Gold : Palette.ParchmentDark);
                string id = hero.Def.Id;
                _tabs[id] = frame.rectTransform;
                var ev = PointerEvents.Of(frame.gameObject);
                ev.OnClick = () => { AudioManager.Play("ui_click"); OpenPicker(_action, id); };
                ev.OnEnter = () => AudioManager.Play("ui_hover");
            }

            var owner = FindHero(heroId);
            var deck = owner.Deck;
            // Două rânduri; dacă pachetul e mare, cărțile se strâng (se suprapun puțin) ca să încapă.
            int perRow = Math.Max(6, (deck.Count + 1) / 2);
            float spacing = Math.Min(72f, 600f / perRow);
            for (int i = 0; i < deck.Count; i++)
            {
                var card = deck[i];
                int col = i % perRow, row = i / perRow;
                int inRow = Math.Min(perRow, deck.Count - row * perRow);
                var pos = new Vector2(Mathf.Round(320 + (col - (inRow - 1) / 2f) * spacing), 200 - row * 100);
                var slot = UiKit.Rect(_page, "Card" + i, pos, new Vector2(70, 96));
                bool allowed = upgrade ? !card.Upgraded : deck.Count > 1;
                var glow = UiKit.Image(slot, SpriteLibrary.Get("Sprites/UI/card_glow_selected"), new Vector2(35, 48));
                glow.color = new Color(1f, 1f, 1f, 0f);
                var view = CardView.Create(slot, card.Def, card.Upgraded, new Vector2(35, 48));
                _pickerCards.Add(allowed ? view : null);
                if (TreasureScreen.IsStub(view))
                    UiKit.Label(slot, card.Def.Name + (card.Upgraded ? "+" : ""), new Vector2(35, 48), new Vector2(64, 40), UiFont.Body, Palette.Parchment);
                if (!allowed)
                {
                    var group = slot.gameObject.AddComponent<CanvasGroup>();
                    group.alpha = 0.4f;
                    if (upgrade) UiKit.Label(slot, "UPGRADED", new Vector2(35, 4), new Vector2(70, 10), UiFont.Label, Palette.Gold);
                    continue;
                }
                string uid = card.Uid;
                var c = card;
                view.OnClicked = _ => Choose(_action, _heroId, uid);
                PointerEvents.Of(glow.gameObject).OnClick = () => Choose(_action, _heroId, uid);
                // La upgrade arătăm chiar cartea de după, mare, lângă cea de sub mouse (cifrele noi sunt verzi);
                // la ștergere, un tooltip scurt. Hover-ul îl prinde CardView-ul (imaginea lui acoperă aura).
                string tip = "Remove " + c.Def.Name + (c.Upgraded ? "+" : "") + "\n" + CardText.Describe(c.Def, c.Upgraded);
                Vector2 slotPos = pos;
                view.HoverChanged = (v, on) =>
                {
                    glow.color = on ? Color.white : new Color(1f, 1f, 1f, 0f);
                    v.SetHighlighted(on);
                    if (on)
                    {
                        AudioManager.Play("ui_hover");
                        if (upgrade) ShowPreview(c, slotPos);
                        else Tooltip.Show(tip, v);
                    }
                    else if (upgrade) HidePreview();
                    else Tooltip.Hide(v);
                };
            }

            UiKit.Button(_page, "BACK", new Vector2(60, 24), new Vector2(80, 18), ShowOptions);
        }

        // Cartea îmbunătățită la scară ×2 (întreagă, deci pixeli curați), în partea cu mai mult loc.
        // Nu prinde mouse-ul: altfel, apărând peste vecini, ar „fura” hover-ul.
        void ShowPreview(CardInstance card, Vector2 slotPos)
        {
            HidePreview();
            float side = slotPos.x <= UIRoot.RefWidth / 2f ? 1f : -1f;
            // y = 172: cartea (180 px înălțime) se termină sub numele eroilor din tab-uri (y ≈ 268).
            var root = UiKit.Rect(_page, "UpgradePreview", new Vector2(slotPos.x + side * 105f, 172f), new Vector2(CardView.Width * 2, CardView.Height * 2));
            var group = root.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            var view = CardView.Create(root, card.Def, true, new Vector2(CardView.Width, CardView.Height));
            view.Interactable = false;
            view.SetHighlighted(true);
            view.Rect.localScale = Vector3.one * 2f;
            UiKit.Label(root, "AFTER UPGRADE", new Vector2(CardView.Width, -8), new Vector2(120, 10), UiFont.Label, Palette.Gold);
            _preview = root;
            UpgradePreview = view;
        }

        void HidePreview()
        {
            if (_preview != null) UnityEngine.Object.Destroy(_preview.gameObject);
            _preview = null;
            UpgradePreview = null;
        }

        PartyHero FindHero(string id)
        {
            foreach (var h in _state.Party) if (h.Def.Id == id) return h;
            return null;
        }

        public void ChooseHeal() => Choose(BedroomAction.Heal, null, null);

        // Alegerea finală (click sau QA). Ecranul se închide, GameFlow aplică acțiunea în model.
        public void Choose(BedroomAction action, string heroId, string cardUid)
        {
            if (_done) return;
            _done = true;
            Tooltip.Hide();
            HidePreview();
            AudioManager.Play(action == BedroomAction.Heal ? "heal" : action == BedroomAction.UpgradeCard ? "buff" : "card_play");
            _onChoose?.Invoke(action, heroId, cardUid);
        }

        public void Close() { Tooltip.Hide(); if (Root != null) UnityEngine.Object.Destroy(Root.gameObject); }
    }
}
