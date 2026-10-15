using System.Collections.Generic;
using DarkestBasement.Audio;
using DarkestBasement.Combat;
using DarkestBasement.Core;
using DarkestBasement.FX;
using DarkestBasement.Model.Combat;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace DarkestBasement.UI
{
    // UI-ul luptei, la 640×360: bara de inițiativă (sus), HUD sub fiecare unitate, intențiile inamicilor,
    // mâna (jos, la mijloc), panoul eroului cu lacrimile-energie (stânga jos), pachetele, End Turn (dreapta jos),
    // butoanele Move lângă eroul activ, hint-ul primei lupte și bannerele mari (ROUND, VICTORY).
    public sealed class CombatHud : MonoBehaviour
    {
        CombatController _c;
        RectTransform _world, _hud, _overlay;
        readonly Dictionary<string, CombatUnitHud> _units = new Dictionary<string, CombatUnitHud>();
        readonly Dictionary<string, CombatIntentView> _intents = new Dictionary<string, CombatIntentView>();

        public CombatHand Hand { get; private set; }
        public CombatTurnOrderBar TurnOrder { get; private set; }
        public string HandHero { get; private set; }

        // Panoul eroului (stânga jos).
        Image _panel, _portraitFrame, _portrait;
        OutlinedText _heroName, _tearsLabel;
        readonly List<Image> _pips = new List<Image>();
        // Pachetele.
        Image _drawPile, _discardPile;
        OutlinedText _drawCount, _discardCount;
        // Butoane.
        Button _endTurn, _giveUp, _moveBack, _moveFront;
        OutlinedText _lastStand;
        RectTransform _moveRoot;
        // Texte mari.
        OutlinedText _hint, _speed;
        RectTransform _bannerRoot;
        TweenHandle _bannerTween;
        bool _input;
        UnitView _hoveredUnit;

        public static CombatHud Create(CombatController c)
        {
            var ui = UIRoot.Create();
            var go = new GameObject("CombatHud");
            go.transform.SetParent(ui.transform, false);
            var hud = go.AddComponent<CombatHud>();
            hud._c = c;
            hud.Build(ui);
            return hud;
        }

        void Build(UIRoot ui)
        {
            _world = UiKit.Stretch(ui.World, "CombatWorld");
            _hud = UiKit.Stretch(ui.Hud, "CombatHud");
            _overlay = UiKit.Stretch(ui.Overlay, "CombatOverlay");
            DamageNumbers.BringToFront();

            TurnOrder = CombatTurnOrderBar.Create(_hud, _c);
            BuildHeroPanel();
            BuildPiles();
            BuildButtons();
            Hand = CombatHand.Create(_hud, _c);

            _hint = CombatUi.Outlined(_overlay, "", new Vector2(320, 303), new Vector2(300, 12), UiFont.Body, Palette.Parchment);
            _hint.Rect.gameObject.SetActive(false);
            _speed = CombatUi.Outlined(_overlay, ">> x2", new Vector2(608, 300), new Vector2(50, 10), UiFont.LabelBold, CombatUi.Gold);
            _speed.Rect.gameObject.SetActive(CombatController.Fast);
            SetPanelVisible(false);
        }

        // ---------- panoul eroului și pachetele ----------

        void BuildHeroPanel()
        {
            _panel = CombatUi.Panel(_hud, "panel_dark", new Vector2(64, 30), new Vector2(112, 48));
            _portraitFrame = CombatUi.Image(_hud, "Sprites/UI/portrait_frame_active", new Vector2(26, 32));
            _portrait = CombatUi.Image(_hud, (Sprite)null, new Vector2(26, 34), new Vector2(24, 24), "HeroPortrait");
            _heroName = CombatUi.Outlined(_hud, "", new Vector2(83, 44), new Vector2(70, 10), UiFont.LabelBold, Palette.Parchment);
            _tearsLabel = CombatUi.Outlined(_hud, "", new Vector2(83, 31), new Vector2(70, 10), UiFont.Label, Palette.Parchment);
            CombatHoverTrigger.On(_panel.gameObject, () => CombatTooltip.Show(this, "TEARS", "Your energy. Cards cost tears; you get 2 at the start of each turn. Moving costs 1.", new Vector2(64, 60), SpriteLibrary.Get("Sprites/UI/energy_full")), () => CombatTooltip.Hide(this));
            _panel.raycastTarget = true;
        }

        void BuildPiles()
        {
            _drawPile = CombatUi.Image(_hud, MiniCard(false), CombatHand.DrawPilePos + new Vector2(0, 4), null, "DrawPile", true);
            _drawCount = CombatUi.Outlined(_hud, "", CombatHand.DrawPilePos + new Vector2(0, -20), new Vector2(60, 10), UiFont.Label, Palette.Parchment);
            CombatHoverTrigger.On(_drawPile.gameObject, () => CombatTooltip.Show(this, "DRAW PILE", "Cards you will draw. When it runs out, the discard pile is shuffled back in.", CombatHand.DrawPilePos + new Vector2(0, 22)), () => CombatTooltip.Hide(this));
            _discardPile = CombatUi.Image(_hud, MiniCard(true), CombatHand.DiscardPilePos + new Vector2(0, 4), null, "DiscardPile", true);
            _discardCount = CombatUi.Outlined(_hud, "", CombatHand.DiscardPilePos + new Vector2(0, -20), new Vector2(70, 10), UiFont.Label, Palette.Parchment);
            CombatHoverTrigger.On(_discardPile.gameObject, () => CombatTooltip.Show(this, "DISCARD PILE", "Played cards and the rest of your hand land here at the end of the turn.", CombatHand.DiscardPilePos + new Vector2(0, 22)), () => CombatTooltip.Hide(this));
        }

        // Un pachet mic de cărți (20×27 px), desenat din cod ca să rămână pixel-perfect (card_back micșorat ar ieși neregulat).
        static Sprite MiniCard(bool discard)
        {
            Color32 border = discard ? new Color32(0x8a, 0x7a, 0x60, 255) : new Color32(0xe0, 0xb0, 0x4a, 255);
            Color32 fill = discard ? new Color32(0x3a, 0x30, 0x28, 255) : new Color32(0x4a, 0x2e, 0x17, 255);
            Color32 outline = new Color32(0x1b, 0x0f, 0x0f, 255);
            return PixelSprites.Make(discard ? "minicard_discard" : "minicard_draw", 20, 27, (x, y) =>
            {
                // Trei cărți suprapuse, fiecare decalată cu 1 px: arată ca un teanc.
                for (int layer = 0; layer < 3; layer++)
                {
                    int ox = 2 - layer, oy = layer;
                    int lx = x - ox, ly = y - oy;
                    if (lx < 0 || ly < 0 || lx >= 18 || ly >= 25) continue;
                    if (layer < 2) { if (lx == 17 || ly == 0) return outline; continue; }
                    if (lx == 0 || ly == 0 || lx == 17 || ly == 24) return outline;
                    if (lx == 1 || ly == 1 || lx == 16 || ly == 23) return border;
                    // O lacrimă mică în mijlocul spatelui cărții.
                    int dx = lx - 9, dy = ly - 11;
                    if (!discard && dx * dx + dy * dy <= 6 || (!discard && dx == 0 && dy >= 2 && dy <= 4)) return new Color32(0x7f, 0xb2, 0xe5, 255);
                    return fill;
                }
                return new Color32(0, 0, 0, 0);
            });
        }

        void BuildButtons()
        {
            _endTurn = CombatUi.Button(_hud, "END TURN", new Vector2(596, 22), new Vector2(76, 22), () => _c.RequestEndTurn(), UiFont.LabelBold);
            CombatHoverTrigger.On(_endTurn.gameObject, () => { if (_endTurn.interactable) AudioManager.Play("ui_hover", 0.5f); CombatTooltip.Show(this, "END TURN", "Space. The rest of your hand goes to the discard pile.", new Vector2(596, 36)); }, () => CombatTooltip.Hide(this));
            _moveRoot = UiKit.Rect(_hud, "Move", Vector2.zero, Vector2.zero);
            _moveBack = CombatUi.Button(_moveRoot, "<", new Vector2(-22, 0), new Vector2(14, 14), () => _c.RequestMove(+1), UiFont.LabelBold);
            _moveFront = CombatUi.Button(_moveRoot, ">", new Vector2(22, 0), new Vector2(14, 14), () => _c.RequestMove(-1), UiFont.LabelBold);
            CombatHoverTrigger.On(_moveBack.gameObject, () => MoveTooltip(+1), () => CombatTooltip.Hide(this));
            CombatHoverTrigger.On(_moveFront.gameObject, () => MoveTooltip(-1), () => CombatTooltip.Hide(this));
            _moveRoot.gameObject.SetActive(false);
            _endTurn.interactable = false;
            _endTurn.gameObject.SetActive(false);
            _giveUp = CombatUi.Button(_hud, "GIVE UP", new Vector2(596, 46), new Vector2(76, 16), () => _c.RequestGiveUp(), UiFont.Label);
            CombatHoverTrigger.On(_giveUp.gameObject, () => CombatTooltip.Show(this, "GIVE UP", "Nobody left can hurt the enemy. End this fight as a defeat.", new Vector2(596, 56)), () => CombatTooltip.Hide(this));
            _giveUp.gameObject.SetActive(false);
            // Last Stand: o insignă deasupra panoului eroului, cât timp a rămas un singur erou.
            _lastStand = CombatUi.Outlined(_hud, "LAST STAND", new Vector2(64, 62), new Vector2(110, 10), UiFont.LabelBold, Palette.Blood);
            _lastStand.Rect.gameObject.SetActive(false);
            var standHit = UiKit.Fill(_lastStand.Rect, new Color(0f, 0f, 0f, 0f), new Vector2(55, 5), new Vector2(80, 10), null, true);
            CombatHoverTrigger.On(standHit.gameObject, () => CombatTooltip.Show(this, "LAST STAND", "The last hero standing can play cards from any rank.", new Vector2(64, 70)), () => CombatTooltip.Hide(this));
        }

        bool _lastStandOn;
        // Insigna stă deasupra panoului eroului: o arătăm doar cât timp se vede și panoul.
        public void SetLastStand(bool on) { _lastStandOn = on; _lastStand.Rect.gameObject.SetActive(on && HandHero != null); }

        void MoveTooltip(int direction)
        {
            var hero = _c.InputHero;
            if (hero == null) return;
            string other = null;
            foreach (var u in _c.Engine.State.Living(true)) if (u.Rank == hero.Rank + direction) other = u.Name;
            string body = other != null ? "Swap places with " + other + ". Costs 1 tear." : "Nobody to swap with.";
            if (other != null && hero.Energy < 1) body += "\nNot enough tears.";
            CombatTooltip.Show(this, direction < 0 ? "MOVE FORWARD" : "MOVE BACK", body, _moveRoot.anchoredPosition + new Vector2(direction < 0 ? 22 : -22, 10));
            // Aliatul cu care s-ar schimba locul se aprinde.
            foreach (var u in _c.Engine.State.Living(true)) if (u.Rank == hero.Rank + direction) _c.View(u.Id)?.SetHovered(true);
        }

        // ---------- unități ----------

        public void AddUnit(UnitView v, bool hidden = false)
        {
            if (_units.ContainsKey(v.Id)) return;
            _units[v.Id] = CombatUnitHud.Create(_world, v, _c.State);
            if (hidden) _units[v.Id].HideUntilPop();
            if (!v.IsHero) _intents[v.Id] = CombatIntentView.Create(_world, _c, v);
            DamageNumbers.BringToFront();
        }

        // O unitate moartă definitiv: HUD-ul se stinge și se distruge, intenția (cu săgețile ei) dispare imediat.
        public void RemoveUnit(string id)
        {
            if (_units.TryGetValue(id, out var h)) { _units.Remove(id); if (h != null) h.FadeAndDestroy(); }
            if (_intents.TryGetValue(id, out var i)) { _intents.Remove(id); if (i != null) Destroy(i.gameObject); }
        }

        // Verificarea Unity (`!= null`) transformă un obiect distrus în null adevărat: apelurile cu `?.` rămân sigure.
        public CombatUnitHud UnitHud(string id) => id != null && _units.TryGetValue(id, out var h) && h != null ? h : null;
        public CombatIntentView Intent(string id) => id != null && _intents.TryGetValue(id, out var i) && i != null ? i : null;

        public void ClearIntents() { foreach (var i in _intents.Values) if (i != null) i.Clear(); }

        public void RefreshAll()
        {
            foreach (var h in _units.Values) if (h != null && h.isActiveAndEnabled) h.Refresh();
            RefreshPiles(false);
            RefreshEnergy(0);
        }

        // ---------- eroul activ ----------

        public void ShowHero(string heroId)
        {
            if (HandHero == heroId) return;
            HandHero = heroId;
            var v = _c.View(heroId);
            var info = _c.State.Unit(heroId);
            _portrait.sprite = v != null ? v.Frames.Portrait : null;
            _heroName.Text = info != null ? info.Name.ToUpperInvariant() : "";
            SetPanelVisible(true);
            // Panoul „sare” puțin când se schimbă eroul.
            var rt = _panel.rectTransform;
            Tween.Value(0.2f, k => { if (rt != null) rt.localScale = new Vector3(1f, Mathf.LerpUnclamped(0.7f, 1f, k), 1f); }, Ease.OutBack, null, 0f, true, rt);
            RefreshEnergy(0);
            RefreshPiles(false);
        }

        public void ClearHero()
        {
            if (HandHero == null) return;
            HandHero = null;
            SetPanelVisible(false);
        }

        void SetPanelVisible(bool on)
        {
            foreach (var g in new Graphic[] { _panel, _portraitFrame, _portrait, _drawPile, _discardPile }) g.gameObject.SetActive(on);
            foreach (var t in new[] { _heroName, _tearsLabel, _drawCount, _discardCount }) t.Rect.gameObject.SetActive(on);
            foreach (var p in _pips) p.gameObject.SetActive(on);
            if (_lastStand != null) _lastStand.Rect.gameObject.SetActive(on && _lastStandOn);
        }

        public void RefreshEnergy(int delta)
        {
            var cards = _c.State.HeroCardsOf(HandHero);
            if (cards == null) return;
            int energy = cards.Energy;
            int slots = Mathf.Max(2, energy);
            while (_pips.Count < slots) _pips.Add(CombatUi.Image(_hud, "Sprites/UI/energy_full", Vector2.zero));
            for (int i = 0; i < _pips.Count; i++)
            {
                var pip = _pips[i];
                bool shown = i < slots;
                pip.gameObject.SetActive(shown);
                if (!shown) continue;
                pip.sprite = SpriteLibrary.Get(i < energy ? "Sprites/UI/energy_full" : "Sprites/UI/energy_empty");
                pip.rectTransform.anchoredPosition = new Vector2(83 + (i - (slots - 1) / 2f) * 14f, 17);
                // Lacrima câștigată „sare”, cea cheltuită se strânge.
                if (delta != 0) pip.rectTransform.localScale = Vector3.one * (delta > 0 ? 1.5f : 0.6f);
            }
            _tearsLabel.Text = "TEARS " + energy + "/2";
            if (_input) RefreshButtons();
        }

        public void RefreshPiles(bool shuffled)
        {
            var cards = _c.State.HeroCardsOf(HandHero);
            if (cards == null) return;
            _drawCount.Text = "DRAW " + cards.Draw;
            _discardCount.Text = "DISCARD " + (cards.Discard + cards.InFlight);
            if (shuffled)
            {
                // Amestec: pachetul de discard „zboară” în cel de draw.
                var rt = _drawPile.rectTransform;
                rt.localScale = Vector3.one * 1.4f;
                var d = _discardPile.rectTransform;
                Vector2 home = d.anchoredPosition;
                Tween.Value(0.3f, k => { if (d != null) d.anchoredPosition = CombatUi.Snap(Vector2.Lerp(home, _drawPile.rectTransform.anchoredPosition, Mathf.Sin(k * Mathf.PI) * 0.5f)); }, Ease.InOutQuad, () => { if (d != null) d.anchoredPosition = home; }, 0f, true, d);
            }
        }

        // ---------- input ----------

        public void BeginInput(HeroUnit hero, bool hopeless)
        {
            _input = true;
            ShowHero(hero.Id);
            Hand.BeginInput(hero);
            _moveRoot.gameObject.SetActive(true);
            // End Turn apare doar în tura jucătorului, cu un mic „pop”: e semnalul că acum e rândul tău.
            _endTurn.gameObject.SetActive(true);
            var rt = (RectTransform)_endTurn.transform;
            Tween.Value(0.18f, k => { if (rt != null) rt.localScale = Vector3.one * Mathf.LerpUnclamped(0.6f, 1f, k); }, Ease.OutBack, null, 0f, true, rt);
            _giveUp.gameObject.SetActive(hopeless);
            RefreshButtons();
        }

        public void EndInput()
        {
            _input = false;
            Hand.EndInput();
            _moveRoot.gameObject.SetActive(false);
            _endTurn.interactable = false;
            _endTurn.gameObject.SetActive(false);
            _giveUp.gameObject.SetActive(false);
            CombatTooltip.HideAll();
        }

        void RefreshButtons()
        {
            _endTurn.interactable = _input;
            _moveBack.interactable = _input && _c.CanMove(+1);
            _moveFront.interactable = _input && _c.CanMove(-1);
            _moveBack.gameObject.SetActive(HasNeighbor(+1));
            _moveFront.gameObject.SetActive(HasNeighbor(-1));
        }

        bool HasNeighbor(int direction)
        {
            var hero = _c.InputHero;
            if (hero == null) return false;
            foreach (var u in _c.Engine.State.Living(true)) if (u.Rank == hero.Rank + direction) return true;
            return false;
        }

        // ---------- texte ----------

        public void ShowHint(string text)
        {
            _hint.Text = text;
            _hint.Rect.gameObject.SetActive(true);
        }

        public void HideHint() { if (_hint != null) _hint.Rect.gameObject.SetActive(false); }

        public void ShowSpeed(bool fast)
        {
            _speed.Rect.gameObject.SetActive(fast);
            AudioManager.Play("ui_click");
        }

        // big = VICTORY / DEFEAT / LAST STAND (font gotic ×3 pe o bandă închisă); altfel o etichetă scurtă (ROUND 2).
        // Tot bannerul (banda + textul) stă într-un singur container care se animă și apoi se distruge.
        public void Banner(string text, Color color, bool big)
        {
            _bannerTween?.Kill();
            if (_bannerRoot != null) Destroy(_bannerRoot.gameObject);
            var root = UiKit.Rect(_overlay, "Banner " + text, big ? new Vector2(320, 210) : new Vector2(320, 287), new Vector2(UIRoot.RefWidth, big ? 48 : 20));
            _bannerRoot = root;
            if (big) UiKit.Fill(root, new Color(0.04f, 0.02f, 0.02f, 0.82f), new Vector2(UIRoot.RefWidth / 2f, 24), new Vector2(UIRoot.RefWidth * 2, 48));
            var label = big
                // Title = Silkscreen Bold 16 px: ×2 (32 px) încape și „MOM IS FURIOUS” pe lățimea ecranului.
                ? CombatUi.Outlined(root, text, new Vector2(UIRoot.RefWidth / 2f, 24), new Vector2(560, 44), UiFont.Title, color, 2)
                : CombatUi.Outlined(root, text, new Vector2(UIRoot.RefWidth / 2f, 10), new Vector2(240, 20), UiFont.LabelBold, color, 2);
            var group = root.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            float hold = big ? 1.4f : 0.5f;
            _bannerTween = Tween.Value(hold + 0.45f, k =>
            {
                if (root == null) return;
                float t = k * (hold + 0.45f);
                float scale = t < 0.15f ? Easing.Evaluate(Ease.OutBack, t / 0.15f) : 1f;
                root.localScale = new Vector3(1f, Mathf.Max(0.01f, scale), 1f);
                group.alpha = t < hold + 0.15f ? 1f : 1f - (t - hold - 0.15f) / 0.3f;
            }, Ease.Linear, () => { if (root != null) Destroy(root.gameObject); }, 0f, true, root);
        }

        // ---------- fiecare frame ----------

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            var kb = Keyboard.current;
            if (_input && kb != null && kb.spaceKey.wasPressedThisFrame && !GameFlow.Paused) _c.RequestEndTurn();

            foreach (var p in _pips) p.rectTransform.localScale = Vector3.Lerp(p.rectTransform.localScale, Vector3.one, 1f - Mathf.Exp(-10f * dt));
            _drawPile.rectTransform.localScale = Vector3.Lerp(_drawPile.rectTransform.localScale, Vector3.one, 1f - Mathf.Exp(-10f * dt));
            if (_hint.Rect.gameObject.activeSelf) _hint.Alpha = 0.65f + 0.35f * Mathf.Sin(Time.unscaledTime * 3f);

            // Butoanele Move stau de o parte și de alta a eroului activ.
            if (_input && _c.InputHero != null)
            {
                var v = _c.View(_c.InputHero.Id);
                if (v != null) _moveRoot.anchoredPosition = CombatUi.Snap(CombatCamera.WorldToUI(v.Home) + new Vector2(0, 8));
            }
            UpdateHover();
        }

        // Mouse-ul peste o unitate: conturul ei se aprinde; la inamici apar săgețile spre țintele intenției.
        void UpdateHover()
        {
            Vector2 mouse = CombatUi.MouseUI();
            UnitView over = CombatUi.MouseOverUI() && !Hand.IsDragging ? null : _c.UnitAtUI(mouse);
            var focus = Hand.FocusCard;
            foreach (var v in _c.Views.Values)
            {
                bool hovered = v == over && (focus == null || Hand.IsValidTarget(v.Id));
                v.SetHovered(hovered);
            }
            if (over == _hoveredUnit) return;
            if (_hoveredUnit != null) { Intent(_hoveredUnit.Id)?.SetArrows(false); CombatTooltip.Hide(_hoveredUnit); }
            _hoveredUnit = over;
            if (over == null || over.Dead || Hand.IsDragging) return;
            var info = _c.State.Unit(over.Id);
            if (info == null) return;
            var intent = Intent(over.Id);
            string body = "HP " + info.Hp + "/" + info.MaxHp;
            if (intent != null && intent.HasIntent) { intent.SetArrows(true); body += "\n" + intent.Describe(); }
            CombatTooltip.Show(over, info.Name.ToUpperInvariant(), body, CombatUi.Snap(CombatCamera.WorldToUI(over.Home + Vector3.up * (over.TopHeight + 0.6f))), over.Frames.Portrait);
        }

        public void Close()
        {
            if (this == null) return;
            CombatTooltip.HideAll();
            if (_world != null) Destroy(_world.gameObject);
            if (_hud != null) Destroy(_hud.gameObject);
            if (_overlay != null) Destroy(_overlay.gameObject);
            Destroy(gameObject);
        }
    }
}
