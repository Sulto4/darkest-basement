using System.Collections.Generic;
using DarkestBasement.Audio;
using DarkestBasement.Combat;
using DarkestBasement.Core;
using DarkestBasement.FX;
using DarkestBasement.Model.Combat;
using DarkestBasement.Model.Core;
using DarkestBasement.Model.Data;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace DarkestBasement.UI
{
    // Mâna eroului activ, jos la mijloc, ca un evantai.
    // Hover: cartea urcă, crește la 1.15 și se înclină spre mouse. Drag: urmărește mouse-ul cu SmoothDamp
    // (un arc amortizat) și se înclină după viteză. Drop pe o țintă validă = carte jucată.
    // O carte cu țintă, scoasă din mână, se oprește deasupra ei și arată spre mouse cu o săgeată punctată
    // (ca în Slay the Spire): altfel cartea ar acoperi chiar inamicul pe care îl ochești.
    // Merge și fără drag: click pe carte, apoi click pe țintă; tastele 1–9 aleg cartea.
    public sealed class CombatHand : MonoBehaviour
    {
        sealed class Motion
        {
            public Vector2 Pos, Vel;
            public float Rot, Scale = 1f, Delay;
            public bool Shown;
        }

        public static readonly Vector2 DrawPilePos = new Vector2(150, 30);
        public static readonly Vector2 DiscardPilePos = new Vector2(490, 30);
        // Deasupra acestei linii mouse-ul e „pe scenă”: o carte fără țintă lăsată aici se joacă.
        const float PlayZoneY = 110f;
        // Unde stă o carte cu țintă cât timp o ochești (sus din mână, dar sub barele de HP ale unităților din față).
        const float AimCardY = 84f;

        CombatController _c;
        RectTransform _rt;
        readonly List<CardView> _cards = new List<CardView>();
        readonly Dictionary<CardView, Motion> _motion = new Dictionary<CardView, Motion>();
        CardView _hovered, _selected, _dragged;
        bool _interactive;
        HeroUnit _hero;
        readonly HashSet<string> _validTargets = new HashSet<string>();
        CardView _targetsFor;
        // Săgeata de țintire: puncte pe o curbă Bézier, de la carte până la mouse.
        RectTransform _aimLayer;
        readonly List<Image> _aimDots = new List<Image>();
        Vector2 _dragFrom;   // de unde a plecat cartea trasă (în ea „se întoarce” cât timp ochești)

        public static CombatHand Create(Transform parent, CombatController c)
        {
            var rt = UiKit.Stretch(parent, "Hand");
            var hand = rt.gameObject.AddComponent<CombatHand>();
            hand._rt = rt;
            hand._c = c;
            hand._aimLayer = UiKit.Stretch(rt, "AimArrow");
            return hand;
        }

        public int Count => _cards.Count;
        public IReadOnlyList<CardView> Cards => _cards;     // de la stânga la dreapta (tasta 1 = prima)
        public CardView Selected => _selected;

        // Cartea care decide ce ținte se aprind: cea trasă > cea selectată > cea de sub mouse (dacă se poate juca).
        public CardView FocusCard
        {
            get
            {
                // Fără `??`: operatorul ăsta nu știe că un obiect Unity distrus e „null”.
                if (_dragged != null) return _dragged;
                if (_selected != null) return _selected;
                return _hovered != null && _hovered.Playable && _interactive ? _hovered : null;
            }
        }
        public bool IsValidTarget(string unitId) => _validTargets.Contains(unitId);
        public bool IsDragging => _dragged != null;

        // ---------- cărți care intră și ies ----------

        public void Add(CardInstance card, float delay)
        {
            var view = CardView.Create(_rt, card, DrawPilePos);
            view.ManagedByHand = true;
            view.HoverChanged = OnHover;
            view.OnClicked = OnClick;
            view.DragBegan = OnDragBegan;
            view.DragEnded = OnDragEnded;
            view.Alpha = 0f;
            _cards.Add(view);
            _motion[view] = new Motion { Pos = DrawPilePos, Rot = 25f, Scale = 0.35f, Delay = delay };
            view.Rect.localScale = Vector3.one * 0.35f;
            if (_interactive) RefreshPlayable();
        }

        // Cartea jucată zboară spre țintă, se micșorează și dispare.
        public void PlayOut(string uid, Vector3 worldTarget)
        {
            var card = Find(uid);
            if (card == null) return;
            Detach(card);
            // Cartea zboară spre erou, se micșorează și se stinge în ~0,2 s: a dispărut înainte de impact.
            Vector2 aim = CombatCamera.WorldToUI(worldTarget);
            Vector2 target = Vector2.Lerp(card.Rect.anchoredPosition, aim, 0.55f);
            Vector2 from = card.Rect.anchoredPosition;
            float fromRot = card.Rect.localEulerAngles.z;
            var rt = card.Rect;
            card.SetGlow(CardGlow.Selected);
            Tween.Value(0.08f, k =>
            {
                if (rt == null) return;
                rt.anchoredPosition = CombatUi.Snap(Vector2.LerpUnclamped(from, target, k));
                rt.localRotation = Quaternion.Euler(0f, 0f, Mathf.LerpAngle(fromRot, 0f, k));
                rt.localScale = Vector3.one * Mathf.Lerp(1.15f, 0.9f, k);
            }, Ease.OutCubic, () =>
            {
                if (rt == null) return;
                Vector2 start = rt.anchoredPosition;
                Tween.Value(0.12f, k =>
                {
                    if (rt == null) return;
                    rt.anchoredPosition = CombatUi.Snap(Vector2.Lerp(start, aim, k));
                    rt.localScale = Vector3.one * Mathf.Lerp(0.9f, 0.1f, k);
                    card.Alpha = 1f - k * k;
                }, Ease.InQuad, () => { if (rt != null) Destroy(rt.gameObject); }, 0f, true, rt);
            }, 0f, true, rt);
        }

        // Toată mâna pleacă în discard (sfârșit de tură, The D6, Dice Shard).
        public void DiscardAll()
        {
            int i = 0;
            foreach (var card in new List<CardView>(_cards))
            {
                Detach(card);
                var rt = card.Rect;
                Vector2 from = rt.anchoredPosition;
                float fromRot = rt.localEulerAngles.z;
                float delay = i++ * 0.035f;
                Tween.Value(0.25f, k =>
                {
                    if (rt == null) return;
                    Vector2 p = Vector2.LerpUnclamped(from, DiscardPilePos, k) + Vector2.up * Mathf.Sin(k * Mathf.PI) * 30f;
                    rt.anchoredPosition = CombatUi.Snap(p);
                    rt.localRotation = Quaternion.Euler(0f, 0f, fromRot - 120f * k);
                    rt.localScale = Vector3.one * Mathf.Lerp(1f, 0.25f, k);
                    card.Alpha = 1f - k * 0.6f;
                }, Ease.InQuad, () => { if (rt != null) Destroy(rt.gameObject); }, delay, true, rt);
            }
            ClearTargets();
        }

        void Detach(CardView card)
        {
            _cards.Remove(card);
            _motion.Remove(card);
            if (_hovered == card) _hovered = null;
            if (_selected == card) _selected = null;
            if (_dragged == card) _dragged = null;
            card.Interactable = false;
            card.BlocksRaycasts = false;
            card.SetHoverPad(false);
            CombatTooltip.Hide(card);
            card.transform.SetAsLastSibling();
        }

        CardView Find(string uid)
        {
            foreach (var c in _cards) if (c.Instance != null && c.Instance.Uid == uid) return c;
            return null;
        }

        // ---------- input ----------

        public void BeginInput(HeroUnit hero)
        {
            _hero = hero;
            _interactive = true;
            RefreshPlayable();
        }

        public void EndInput()
        {
            _interactive = false;
            _selected = null;
            foreach (var c in _cards) c.SetGlow(CardGlow.None);
            ClearTargets();
        }

        // Gri + motiv pentru cărțile care nu se pot juca acum (rang, energie, fără ținte).
        public void RefreshPlayable()
        {
            foreach (var c in _cards)
            {
                if (c.Instance == null || _hero == null) continue;
                bool ok = _c.Engine.CanPlay(_hero, c.Instance, out string reason);
                c.SetPlayable(ok, reason);
                c.SetGlow(!_interactive ? CardGlow.None : c == _selected ? CardGlow.Selected : ok ? CardGlow.Playable : CardGlow.None);
            }
        }

        void OnHover(CardView card, bool on)
        {
            if (on)
            {
                _hovered = card;
                if (_dragged == null) AudioManager.Play("ui_hover", 0.35f);
                if (_interactive && !card.Playable && !string.IsNullOrEmpty(card.Reason))
                    CombatTooltip.Show(card, "CAN'T PLAY", card.Reason, BesideCard(card));
                else if (_dragged == null) ShowKeywords(card);
            }
            else
            {
                if (_hovered == card) _hovered = null;
                CombatTooltip.Hide(card);
            }
            card.SetHoverPad(on);
        }

        // Cuvintele-cheie de pe carte (Bleed, Mark, Block...) explicate lângă ea (GDD §10: jucătorul înțelege mereu de ce).
        // Tooltip-ul stă în partea în care e mai mult loc, ca să nu acopere chiar cartea.
        void ShowKeywords(CardView card)
        {
            var def = card.Def;
            if (def == null) return;
            var seen = new List<Status>();
            var lines = new List<string>();
            foreach (var fx in def.Effects)
            {
                Status s;
                if (fx.Kind == EffectKind.ApplyStatus) s = fx.Status;
                else if (fx.Kind == EffectKind.Block) s = Status.Block;
                else if (fx.Kind == EffectKind.GainStrength) s = Status.Strength;
                else if (fx.Kind == EffectKind.Taunt) s = Status.Taunt;
                else continue;
                if (seen.Contains(s)) continue;
                seen.Add(s);
                int value = def.EffectValue(fx, card.Upgraded);
                lines.Add("<color=" + CombatUi.HexOf(Palette.BloodDark) + ">" + s.ToString().ToUpperInvariant() + "</color>  " + CombatUi.StatusText(s, value));
            }
            if (lines.Count == 0) return;
            CombatTooltip.Show(card, lines.Count == 1 ? "KEYWORD" : "KEYWORDS", string.Join("\n", lines), BesideCard(card), CombatUi.StatusIcon(seen[0]));
        }

        // Tooltip-urile unei cărți stau lângă ea, în partea cu mai mult loc: nu acoperă cartea ridicată
        // și nici barele de HP ale unităților de deasupra mâinii. Pornim de la locul cărții în evantai, nu de la
        // poziția de acum: în clipa hover-ului ea poate fi încă împinsă de vecina ridicată înainte.
        Vector2 BesideCard(CardView card)
        {
            float x = RestX(card);
            float side = x <= UIRoot.RefWidth / 2f ? 1f : -1f;
            return new Vector2(x + side * 108f, 34f);
        }

        // Locul unei cărți în evantai (fără împingerile de la hover) — aceeași formulă ca în Layout.
        float RestX(CardView card)
        {
            int n = _cards.Count, i = _cards.IndexOf(card);
            if (i < 0) return card.Rect.anchoredPosition.x;
            return 320f + (i - (n - 1) / 2f) * Spacing(n);
        }

        // Până la 4 cărți încap una lângă alta (66 > 64 px), fără să-și acopere textul; de la 5 se strâng.
        static float Spacing(int n) => n <= 4 ? 66f : Mathf.Max(30f, 230f / Mathf.Max(1, n - 1));

        void OnClick(CardView card)
        {
            if (!_interactive) return;
            if (!card.Playable) { card.Shake(); AudioManager.Play("ui_click", 0.4f, 0.7f); return; }
            if (_selected == card)
            {
                if (!NeedsTarget(card.Def)) PlayUntargeted(card);
                else Deselect();
                return;
            }
            Select(card);
        }

        void Select(CardView card)
        {
            if (_selected != null && _selected != card) _selected.SetGlow(_selected.Playable ? CardGlow.Playable : CardGlow.None);
            _selected = card;
            card.SetGlow(CardGlow.Selected);
            AudioManager.Play("ui_click");
        }

        void Deselect()
        {
            if (_selected == null) return;
            _selected.SetGlow(_selected.Playable ? CardGlow.Playable : CardGlow.None);
            _selected = null;
        }

        void OnDragBegan(CardView card)
        {
            if (!_interactive || !card.Playable) { card.Shake(); return; }
            Deselect();
            _dragged = card;
            _dragFrom = card.Rect.anchoredPosition;
            CombatTooltip.Hide(card);
            card.BlocksRaycasts = false;
            card.SetGlow(CardGlow.Selected);
            AudioManager.Play("card_draw", 0.6f);
        }

        void OnDragEnded(CardView card)
        {
            if (_dragged != card) return;
            _dragged = null;
            card.BlocksRaycasts = true;
            Vector2 mouse = CombatUi.MouseUI();
            bool played = false;
            if (NeedsTarget(card.Def))
            {
                var unit = _c.UnitAtUI(mouse);
                if (unit != null && _validTargets.Contains(unit.Id)) played = TryPlay(card, _c.ModelUnit(unit.Id));
            }
            else if (mouse.y > PlayZoneY) played = PlayUntargeted(card);
            if (!played)
            {
                card.SetGlow(card.Playable ? CardGlow.Playable : CardGlow.None);
                AudioManager.Play("card_draw", 0.4f, 0.8f);
            }
        }

        public static bool NeedsTarget(CardDef def) => def != null && (def.TargetType == TargetType.Enemy || def.TargetType == TargetType.Ally);

        bool PlayUntargeted(CardView card)
        {
            // Pentru „sine” ținta e eroul; pentru „toți” motorul lovește oricum toate țintele valide.
            Unit target = card.Def.TargetType == TargetType.Self ? (Unit)_hero : null;
            if (target == null)
            {
                var valid = _c.Engine.ValidTargets(_hero, card.Instance);
                if (valid.Count > 0) target = valid[0];
            }
            return TryPlay(card, target);
        }

        bool TryPlay(CardView card, Unit target)
        {
            if (card == null || target == null) return false;
            if (!_c.RequestPlay(card.Instance, target)) { card.Shake(); return false; }
            _selected = null;
            card.Interactable = false;
            return true;
        }

        void Update()
        {
            float dt = Core.GameTime.UnscaledDelta;
            Vector2 mouse = CombatUi.MouseUI();
            if (_interactive && !GameFlow.Paused) HandleInput(mouse);
            Layout(mouse, dt);
            UpdateTargets();
            UpdateAim(mouse);
        }

        void HandleInput(Vector2 mouse)
        {
            var kb = Keyboard.current;
            if (kb != null)
            {
                Key[] digits = { Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4, Key.Digit5, Key.Digit6, Key.Digit7, Key.Digit8, Key.Digit9 };
                for (int i = 0; i < digits.Length && i < _cards.Count; i++)
                    if (kb[digits[i]].wasPressedThisFrame) OnClick(_cards[i]);
            }
            var m = Mouse.current;
            if (m == null) return;
            if (m.rightButton.wasPressedThisFrame) Deselect();
            // Cartea selectată + click în lume = alegerea țintei.
            if (_selected != null && m.leftButton.wasPressedThisFrame && !CombatUi.MouseOverUI())
            {
                if (NeedsTarget(_selected.Def))
                {
                    var unit = _c.UnitAtUI(mouse);
                    if (unit != null && _validTargets.Contains(unit.Id)) TryPlay(_selected, _c.ModelUnit(unit.Id));
                    else Deselect();
                }
                else if (mouse.y > PlayZoneY) PlayUntargeted(_selected);
            }
        }

        // ---------- așezarea în evantai ----------

        void Layout(Vector2 mouse, float dt)
        {
            int n = _cards.Count;
            float mid = (n - 1) / 2f;
            float spacing = Spacing(n);
            var focus = _dragged != null ? null : _selected != null ? _selected : _hovered != null ? _hovered : null;
            int focusIndex = focus != null ? _cards.IndexOf(focus) : -1;
            float k = 1f - Mathf.Exp(-16f * dt);
            for (int i = 0; i < n; i++)
            {
                var card = _cards[i];
                var m = _motion[card];
                if (m.Delay > 0f) { m.Delay -= dt; card.Alpha = 0f; continue; }
                if (!m.Shown) { m.Shown = true; AudioManager.Play("card_draw", 0.7f); }
                card.Alpha = Mathf.MoveTowards(card.Alpha, 1f, dt * 8f);

                Vector2 target; float rot, scale;
                if (card == _dragged)
                {
                    if (NeedsTarget(card.Def) && mouse.y > PlayZoneY)
                    {
                        // Ochire: cartea se oprește deasupra mâinii (urmărește doar puțin mouse-ul pe orizontală),
                        // iar săgeata arată ținta. Ținta rămâne la vedere, cu conturul aprins.
                        target = new Vector2(Mathf.Lerp(_dragFrom.x, mouse.x, 0.25f), AimCardY);
                        scale = 1f;
                    }
                    else
                    {
                        target = mouse + new Vector2(0f, -20f);
                        scale = 1.1f;
                    }
                    // SmoothDamp = arc amortizat critic: ajunge repede la mouse, fără să oscileze.
                    m.Pos = Vector2.SmoothDamp(m.Pos, target, ref m.Vel, 0.05f, Mathf.Infinity, dt);
                    rot = Mathf.Clamp(-m.Vel.x * 0.03f, -20f, 20f);
                }
                else
                {
                    float off = i - mid;
                    // Arcul și înclinarea sunt normalizate: și cu 10 cărți marginile coboară cel mult 8 px.
                    float u = mid > 0f ? off / mid : 0f;
                    target = new Vector2(320f + off * spacing, 46f - u * u * Mathf.Min(8f, 2f * mid * mid));
                    rot = -u * Mathf.Min(10f, 3.5f * mid);
                    scale = 1f;
                    if (focusIndex >= 0 && card != focus) target.x += Mathf.Sign(i - focusIndex) * 14f / Mathf.Abs(i - focusIndex);
                    if (card == _selected) { target.y = 64f; rot = 0f; scale = 1.15f; }
                    else if (card == _hovered && _dragged == null)
                    {
                        target.y = 62f; scale = 1.15f;
                        // Înclinare spre mouse: partea de sus a cărții „se apleacă” după cursor.
                        rot = -Mathf.Clamp((mouse.x - m.Pos.x) / 30f, -1f, 1f) * 6f;
                    }
                    // Cartea de sub mouse răspunde mai repede decât restul (ridicarea se simte imediat).
                    m.Pos = Vector2.SmoothDamp(m.Pos, target, ref m.Vel, card == focus ? 0.04f : 0.07f, Mathf.Infinity, dt);
                }
                m.Rot = Mathf.LerpAngle(m.Rot, rot, k);
                m.Scale = Mathf.Lerp(m.Scale, scale, k);
                card.Rect.anchoredPosition = CombatUi.Snap(m.Pos);
                card.Rect.localRotation = Quaternion.Euler(0f, 0f, m.Rot);
                card.Rect.localScale = Vector3.one * m.Scale;
            }
            // Ordinea de desenare: de la stânga la dreapta; cartea „activă” deasupra tuturor.
            for (int i = 0; i < n; i++) _cards[i].transform.SetSiblingIndex(i);
            if (focus != null && focus.transform.parent == _rt) focus.transform.SetAsLastSibling();
            if (_dragged != null) _dragged.transform.SetAsLastSibling();
            _aimLayer.SetAsLastSibling();
        }

        // ---------- săgeata de țintire ----------

        // De la cartea trasă (sau selectată cu click) la mouse, cât timp mouse-ul e pe scenă și cartea cere o țintă.
        // Aurie peste o țintă validă, palidă în rest: vezi dinainte dacă drop-ul / click-ul va prinde.
        void UpdateAim(Vector2 mouse)
        {
            CardView source = null;
            if (_dragged != null && NeedsTarget(_dragged.Def)) source = _dragged;
            else if (_selected != null && _interactive && NeedsTarget(_selected.Def)) source = _selected;
            if (source == null || mouse.y <= PlayZoneY || GameFlow.Paused) { HideAim(); return; }

            var unit = _c.UnitAtUI(mouse);
            bool onTarget = unit != null && _validTargets.Contains(unit.Id);
            Color color = onTarget ? Palette.Gold : new Color(0.91f, 0.86f, 0.77f, 0.7f);
            float top = Height(source) * 0.5f;
            Vector2 a = source.Rect.anchoredPosition + new Vector2(0f, top);
            Vector2 b = mouse;
            // B(t) = (1−t)²·A + 2(1−t)t·C + t²·B, cu punctul de control C ridicat deasupra mijlocului.
            Vector2 c = (a + b) * 0.5f + new Vector2((a.x - b.x) * 0.15f, 18f + Mathf.Abs(a.x - b.x) * 0.12f);
            int steps = Mathf.Clamp(Mathf.RoundToInt(Vector2.Distance(a, b) / 7f), 4, 40);
            float phase = (Core.GameTime.Unscaled * 2.5f) % 1f;
            int dot = 0;
            for (int i = 0; i < steps; i++)
            {
                float t = (i + phase) / (steps + 1);
                Vector2 p = (1 - t) * (1 - t) * a + 2 * (1 - t) * t * c + t * t * b;
                // Puncte cu contur închis: se văd și peste praful auriu și torțele din fundal.
                var img = AimDot(dot++, PixelSprites.DotOutlined, new Vector2(4, 4));
                img.rectTransform.anchoredPosition = CombatUi.Snap(p);
                img.rectTransform.localRotation = Quaternion.identity;
                img.color = new Color(color.r, color.g, color.b, color.a * (0.6f + 0.4f * t));
            }
            // Vârful, orientat pe direcția de la sfârșitul curbei: B'(1) ∝ (B − C).
            var head = AimDot(dot++, PixelSprites.ChevronOutlined, new Vector2(11, 11));
            head.rectTransform.anchoredPosition = CombatUi.Snap(b);
            head.rectTransform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(b.y - c.y, b.x - c.x) * Mathf.Rad2Deg);
            head.color = color;
            for (int i = dot; i < _aimDots.Count; i++) _aimDots[i].enabled = false;
        }

        static float Height(CardView card) => CardView.Height * card.Rect.localScale.y;

        Image AimDot(int i, Sprite sprite, Vector2 size)
        {
            while (_aimDots.Count <= i) _aimDots.Add(CombatUi.Image(_aimLayer, sprite, Vector2.zero, size, "AimDot"));
            var img = _aimDots[i];
            if (img.sprite != sprite) { img.sprite = sprite; img.rectTransform.sizeDelta = size; }
            img.enabled = true;
            return img;
        }

        void HideAim()
        {
            foreach (var d in _aimDots) if (d.enabled) d.enabled = false;
        }

        public bool AimShown => _aimDots.Count > 0 && _aimDots[0].enabled;

        // ---------- ținte ----------

        void UpdateTargets()
        {
            var focus = FocusCard;
            if (focus == _targetsFor) return;
            _targetsFor = focus;
            _validTargets.Clear();
            if (focus != null && focus.Instance != null && _hero != null)
                foreach (var u in _c.Engine.ValidTargets(_hero, focus.Instance)) _validTargets.Add(u.Id);
            bool allies = focus != null && (focus.Def.TargetType == TargetType.Ally || focus.Def.TargetType == TargetType.AllAllies || focus.Def.TargetType == TargetType.Self);
            Color glow = allies ? new Color(0.55f, 1f, 0.45f) : Palette.Gold;
            foreach (var v in _c.Views.Values) v.SetTargetable(_validTargets.Contains(v.Id), glow);
        }

        void ClearTargets()
        {
            _targetsFor = null;
            _validTargets.Clear();
            foreach (var v in _c.Views.Values) v.SetTargetable(false, Color.white);
        }
    }
}
