using System.Collections.Generic;
using DarkestBasement.Combat;
using DarkestBasement.Core;
using DarkestBasement.Model.Combat;
using UnityEngine;
using UnityEngine.UI;

namespace DarkestBasement.UI
{
    // HUD-ul de sub fiecare unitate (ca în Darkest Dungeon): bara de HP cu „urma” albă a damage-ului,
    // stratul albastru de Block, cifrele HP și iconițele de status cu numărul de stack-uri (tooltip la hover).
    // Stă sub picioare, nu deasupra capului: așa nu se lovește de intenții și are aceeași înălțime și la boss-i.
    public sealed class CombatUnitHud : MonoBehaviour
    {
        static readonly Status[] Shown = { Status.Bleed, Status.Poison, Status.Stun, Status.Weak, Status.Mark, Status.Strength, Status.Taunt };

        UnitView _view;
        CombatViewState _state;
        RectTransform _rt;
        Image _frame, _lag, _fill, _blockFrame, _blockIcon;
        OutlinedText _hpText, _blockText;
        RectTransform _statusRow;
        readonly Dictionary<Status, StatusIcon> _icons = new Dictionary<Status, StatusIcon>();
        int _barWidth;
        bool _boss;
        static readonly Vector2 BossAnchor = new Vector2(540, 336);
        float _shownHp, _lagHp, _lagDelay, _shake, _alpha = 1f;
        bool _dead;
        CanvasGroup _group;

        sealed class StatusIcon
        {
            public RectTransform Root;
            public Image Icon;
            public OutlinedText Count;
            public float Pop;
        }

        public static CombatUnitHud Create(Transform parent, UnitView view, CombatViewState state)
        {
            var rt = UiKit.Rect(parent, "UnitHud " + view.Id, Vector2.zero, Vector2.zero);
            var hud = rt.gameObject.AddComponent<CombatUnitHud>();
            hud._rt = rt;
            hud._view = view;
            hud._state = state;
            hud.Build();
            return hud;
        }

        CombatViewState.UnitInfo Info => _state.Unit(_view.Id);

        void Build()
        {
            _group = gameObject.AddComponent<CanvasGroup>();
            _group.blocksRaycasts = true;
            // Boss-ii au bara lor sus-dreapta (ca în Darkest Dungeon): sub ei stau minionii, cu barele lor.
            _boss = _view.IsBoss;
            // Bara crește odată cu personajele (×2 pe ecran): 44 px la unitățile obișnuite, 150 la boss.
            _barWidth = _boss ? 150 : 44;
            if (_boss)
            {
                CombatUi.Panel(_rt, "panel_dark", new Vector2(0, -8), new Vector2(_barWidth + 26, 46));
                CombatUi.Outlined(_rt, _view.DisplayName.ToUpperInvariant(), new Vector2(0, 7), new Vector2(_barWidth, 10), UiFont.LabelBold, Palette.Gold);
            }
            // Coordonate față de punctul de sub picioarele unității (0,0) sau, la boss, față de colțul lui.
            _frame = CombatUi.Panel(_rt, "hpbar_frame", new Vector2(0, -6.5f), new Vector2(_barWidth, 7));
            _lag = CombatUi.Panel(_rt, "hpbar_fill_lag", new Vector2(0, -6.5f), new Vector2(_barWidth - 4, 3));
            _fill = CombatUi.Panel(_rt, "hpbar_fill", new Vector2(0, -6.5f), new Vector2(_barWidth - 4, 3));
            foreach (var img in new[] { _lag, _fill }) { img.rectTransform.pivot = new Vector2(0f, 0.5f); img.rectTransform.anchoredPosition = new Vector2(-_barWidth / 2f + 2, -6.5f); }
            _blockFrame = CombatUi.Panel(_rt, "hpbar_block", new Vector2(0, -6.5f), new Vector2(_barWidth, 7));
            _blockIcon = CombatUi.Image(_rt, CombatUi.StatusIcon(Status.Block), new Vector2(-_barWidth / 2f - 4, -6), null, "BlockIcon", true);
            _blockText = CombatUi.Outlined(_blockIcon.transform, "0", new Vector2(6, 5), new Vector2(16, 10), UiFont.LabelBold, Palette.White);
            CombatHoverTrigger.On(_blockIcon.gameObject,
                () => CombatTooltip.Show(this, "BLOCK " + (Info?.Block ?? 0), CombatUi.StatusText(Status.Block, Info?.Block ?? 0), Anchor(_blockIcon.rectTransform), CombatUi.StatusIcon(Status.Block)),
                () => CombatTooltip.Hide(this));
            _hpText = CombatUi.Outlined(_rt, "", new Vector2(0, -14), new Vector2(60, 10), UiFont.Label, Palette.Parchment);
            // Trei rânduri sub picioare: bara (−6), cifrele HP (−14), statusurile (−25), deasupra mâinii de cărți.
            _statusRow = UiKit.Rect(_rt, "Statuses", new Vector2(0, -25), Vector2.zero);

            var info = Info;
            _shownHp = _lagHp = info != null ? info.Hp : 0;
            Refresh();
        }

        Vector2 Anchor(RectTransform r)
        {
            // Poziția unui copil în coordonatele ecranului de 640×360.
            return CombatUi.Snap(_rt.anchoredPosition + r.anchoredPosition + new Vector2(0, 6));
        }

        // Recitește starea (HP, block, statusuri) și reface rândul de iconițe.
        public void Refresh()
        {
            var info = Info;
            if (info == null) return;
            _hpText.Text = info.Hp + "/" + info.MaxHp;
            bool block = info.Block > 0;
            _blockFrame.enabled = block;
            _blockIcon.enabled = block;
            _blockText.Rect.gameObject.SetActive(block);
            if (block) _blockText.Text = info.Block.ToString();

            int count = 0;
            foreach (var s in Shown) if (info.Get(s) > 0) count++;
            int index = 0;
            foreach (var s in Shown)
            {
                int stacks = info.Get(s);
                _icons.TryGetValue(s, out var icon);
                if (stacks <= 0)
                {
                    if (icon != null) icon.Root.gameObject.SetActive(false);
                    continue;
                }
                if (icon == null) icon = _icons[s] = MakeIcon(s);
                icon.Root.gameObject.SetActive(true);
                // Stun și Taunt la Nerve Ending nu au cifră utilă; restul arată stack-urile.
                icon.Count.Text = s == Status.Stun ? "" : stacks.ToString();
                icon.Root.anchoredPosition = new Vector2((index - (count - 1) / 2f) * 14f, 0f);
                index++;
            }
        }

        StatusIcon MakeIcon(Status s)
        {
            var root = UiKit.Rect(_statusRow, s.ToString(), Vector2.zero, new Vector2(12, 12));
            var img = CombatUi.Image(root, CombatUi.StatusIcon(s), new Vector2(6, 6), null, "Icon", true);
            var count = CombatUi.Outlined(root, "", new Vector2(11, 1), new Vector2(12, 10), UiFont.LabelBold, Palette.White);
            var icon = new StatusIcon { Root = root, Icon = img, Count = count };
            CombatHoverTrigger.On(img.gameObject, () =>
            {
                var info = Info; int n = info != null ? info.Get(s) : 0;
                bool permanent = s == Status.Taunt && info != null && info.DefId == "nerve_ending";
                CombatTooltip.Show(this, s.ToString().ToUpperInvariant() + (s == Status.Stun ? "" : " " + n), CombatUi.StatusText(s, n, permanent),
                    CombatUi.Snap(_rt.anchoredPosition + _statusRow.anchoredPosition + root.anchoredPosition + new Vector2(0, 8)), CombatUi.StatusIcon(s));
            }, () => CombatTooltip.Hide(this));
            return icon;
        }

        // O iconiță de status de pe ecran (Block sau din rândul de statusuri), pentru QA; null dacă nu e niciuna.
        public RectTransform VisibleStatusIcon()
        {
            if (_dead || _waiting) return null;
            foreach (var icon in _icons.Values) if (icon.Root.gameObject.activeInHierarchy) return icon.Icon.rectTransform;
            return _blockIcon.enabled ? _blockIcon.rectTransform : null;
        }

        public void OnDamaged(int amount, int blocked)
        {
            _shake = amount > 0 ? 1f : 0.5f;
            _lagDelay = 0.35f;
            Refresh();
            if (blocked > 0) PopStatus(Status.Block);
        }

        // Mică „săritură” a iconiței când statusul se schimbă: ochiul observă ce s-a modificat.
        public void PopStatus(Status s)
        {
            Refresh();
            if (s == Status.Block) { _blockIcon.rectTransform.localScale = Vector3.one * 1.6f; return; }
            if (_icons.TryGetValue(s, out var icon)) { icon.Pop = 1f; icon.Root.localScale = Vector3.one * 1.7f; }
        }

        bool _destroyWhenFaded;
        bool _waiting;      // ascuns până când unitatea și-a terminat intrarea în scenă
        float _popScale = 1f;

        // Inamicii cad din tavan la începutul luptei: HUD-ul lor apare abia după aterizare, cu un mic „pop”.
        public void HideUntilPop()
        {
            _waiting = true;
            _group.alpha = 0f;
            _group.blocksRaycasts = false;
        }

        public void PopIn()
        {
            if (!_waiting) return;
            _waiting = false;
            _group.alpha = 1f;
            _group.blocksRaycasts = true;
            _popScale = 1.4f;
        }
        public void FadeAndDestroy()
        {
            // Dacă s-a stins deja (și s-a dezactivat), LateUpdate nu mai rulează: îl distrugem pe loc.
            if (!gameObject.activeSelf || _alpha <= 0f) { Destroy(gameObject); return; }
            _dead = true; _destroyWhenFaded = true;
        }

        public void SetDead(bool dead)
        {
            _dead = dead;
            if (!dead) { _alpha = 1f; gameObject.SetActive(true); Refresh(); }
        }

        void LateUpdate()
        {
            if (_view == null) { Destroy(gameObject); return; }
            float dt = Time.unscaledDeltaTime;
            if (_waiting) return;
            if (_dead)
            {
                _alpha -= dt * 3f;
                _group.alpha = Mathf.Max(0f, _alpha);
                _group.blocksRaycasts = false;
                if (_alpha <= 0f)
                {
                    if (_destroyWhenFaded) Destroy(gameObject);
                    else gameObject.SetActive(false);
                    return;
                }
            }
            // HUD-ul stă la slot (Home), nu urmează lunge-ul: altfel ar sări prin ecran la fiecare atac.
            Vector2 feet = _boss ? BossAnchor : CombatCamera.WorldToUI(_view.Home);
            Vector2 shake = _shake > 0f ? new Vector2(Mathf.Round(Mathf.Sin(Time.unscaledTime * 70f) * 2f * _shake), 0f) : Vector2.zero;
            _shake = Mathf.Max(0f, _shake - dt * 4f);
            _rt.anchoredPosition = CombatUi.Snap(feet + shake);
            _popScale = Mathf.Lerp(_popScale, 1f, 1f - Mathf.Exp(-14f * dt));
            _rt.localScale = new Vector3(1f, _popScale > 1.01f ? _popScale : 1f, 1f);

            var info = Info;
            if (info == null) return;
            // Bara roșie urmează repede; urma crem rămâne o clipă și apoi „se scurge” (citești cât ai pierdut).
            _shownHp = Mathf.MoveTowards(_shownHp, info.Hp, Mathf.Max(30f, Mathf.Abs(_shownHp - info.Hp) * 10f) * dt);
            if (_lagDelay > 0f) _lagDelay -= dt;
            else _lagHp = Mathf.MoveTowards(_lagHp, info.Hp, Mathf.Max(12f, Mathf.Abs(_lagHp - info.Hp) * 3f) * dt);
            if (_lagHp < _shownHp) _lagHp = _shownHp;
            float inner = _barWidth - 4;
            float max = Mathf.Max(1, info.MaxHp);
            _fill.rectTransform.sizeDelta = new Vector2(Mathf.Round(inner * Mathf.Clamp01(_shownHp / max)), 3);
            _lag.rectTransform.sizeDelta = new Vector2(Mathf.Round(inner * Mathf.Clamp01(_lagHp / max)), 3);
            _fill.enabled = _fill.rectTransform.sizeDelta.x > 0f;
            _lag.enabled = _lag.rectTransform.sizeDelta.x > 0f;

            _blockIcon.rectTransform.localScale = Vector3.Lerp(_blockIcon.rectTransform.localScale, Vector3.one, 1f - Mathf.Exp(-12f * dt));
            foreach (var icon in _icons.Values)
                icon.Root.localScale = Vector3.Lerp(icon.Root.localScale, Vector3.one, 1f - Mathf.Exp(-12f * dt));
        }
    }
}
