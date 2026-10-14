using System.Collections.Generic;
using DarkestBasement.Core;
using DarkestBasement.Model.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DarkestBasement.UI
{
    // HUD-ul din explorare: portretele eroilor cu HP (stânga-sus) și etajul curent (dreapta-sus).
    // În luptă îl ascundem: echipa C își desenează propriul HUD peste unități.
    public sealed class PartyHud
    {
        sealed class Slot
        {
            public string HeroId;
            public RectTransform Root;
            public Image Frame, Portrait, Fill;
            public ShadowText Name, Hp;
            public float ShownRatio = -1f;
        }

        public RectTransform Root { get; }
        readonly List<Slot> _slots = new List<Slot>();
        readonly ShadowText _floorTitle, _floorSub;
        readonly CanvasGroup _group;
        const float BarWidth = 40f;

        public PartyHud(Transform parent)
        {
            Root = UiKit.Stretch(parent, "PartyHud");
            _group = Root.gameObject.AddComponent<CanvasGroup>();
            _group.blocksRaycasts = false;
            _floorTitle = UiKit.Label(Root, "", new Vector2(560, 344), new Vector2(150, 20), UiFont.Title, Palette.Gold, TextAlignmentOptions.Right, 1);
            _floorSub = UiKit.Label(Root, "", new Vector2(560, 330), new Vector2(150, 10), UiFont.Label, Palette.Parchment, TextAlignmentOptions.Right);
        }

        public void SetVisible(bool visible, float duration = 0.25f)
        {
            float from = _group.alpha, to = visible ? 1f : 0f;
            Tween.Value(duration, k => _group.alpha = Mathf.Lerp(from, to, k), Ease.OutQuad, null, 0f, true, Root);
        }

        public void Refresh(RunState state, FloorTheme theme)
        {
            _floorTitle.Text = theme.Title;
            _floorTitle.Color = theme.Accent;
            _floorSub.Text = "FLOOR " + theme.Index + "/3   [M] MAP";

            // Ordinea de pe ecran = rangul (1 = primul, ca în formație).
            var heroes = new List<PartyHero>(state.Party);
            heroes.Sort((a, b) => (a.IsAlive ? a.Rank : 10 + a.Rank).CompareTo(b.IsAlive ? b.Rank : 10 + b.Rank));
            while (_slots.Count < heroes.Count) _slots.Add(NewSlot(_slots.Count));

            for (int i = 0; i < heroes.Count; i++)
            {
                var hero = heroes[i];
                var s = _slots[i];
                if (s.HeroId != hero.Def.Id)
                {
                    s.HeroId = hero.Def.Id;
                    s.Portrait.sprite = SpriteLibrary.Get("Sprites/Heroes/" + hero.Def.Id + "/portrait");
                    s.ShownRatio = -1f;
                }
                s.Name.Text = hero.Def.Name.ToUpperInvariant();
                bool alive = hero.IsAlive;
                s.Portrait.color = alive ? Color.white : new Color(0.3f, 0.25f, 0.25f);
                s.Hp.Text = alive ? hero.Hp + "/" + hero.Def.MaxHp : "DEAD";
                s.Hp.Color = alive ? (hero.Hp * 3 <= hero.Def.MaxHp ? Palette.Blood : Palette.Parchment) : Palette.BloodDark;
                float ratio = alive ? (float)hero.Hp / hero.Def.MaxHp : 0f;
                if (!Mathf.Approximately(ratio, s.ShownRatio))
                {
                    float from = s.ShownRatio < 0f ? ratio : s.ShownRatio;
                    s.ShownRatio = ratio;
                    var fill = s.Fill;
                    Tween.Value(0.45f, k => fill.rectTransform.sizeDelta = new Vector2(Mathf.Round(Mathf.Lerp(from, ratio, k) * (BarWidth - 4f)), 4f),
                        Ease.OutCubic, null, 0f, true, fill);
                }
            }
        }

        // Portretul clipește (de ex. când eroul primește o carte nouă sau e vindecat).
        public void Flash(string heroId, Color color)
        {
            foreach (var s in _slots)
            {
                if (s.HeroId != heroId) continue;
                var frame = s.Frame;
                frame.sprite = SpriteLibrary.Get("Sprites/UI/portrait_frame_active");
                Tween.Value(0.9f, k => frame.color = Color.Lerp(color, Color.white, k), Ease.OutQuad,
                    () => frame.sprite = SpriteLibrary.Get("Sprites/UI/portrait_frame"), 0f, true, frame);
                var rt = s.Root;
                rt.localScale = Vector3.one * 1.15f;
                Tween.Scale(rt, Vector3.one, 0.35f, Ease.OutBack, null, 0f, true);
            }
        }

        Slot NewSlot(int index)
        {
            var s = new Slot();
            float x = 6 + index * 76;
            s.Root = UiKit.Rect(Root, "Hero" + index, new Vector2(x, 354), new Vector2(72, 34), new Vector2(0f, 1f));
            s.Frame = UiKit.Image(s.Root, SpriteLibrary.Get("Sprites/UI/portrait_frame"), new Vector2(14, 18));
            s.Frame.raycastTarget = false;
            s.Portrait = UiKit.Image(s.Root, (Sprite)null, new Vector2(14, 20), new Vector2(24, 24));
            s.Portrait.raycastTarget = false;
            s.Name = UiKit.Label(s.Root, "", new Vector2(52, 28), new Vector2(42, 10), UiFont.Label, Palette.Parchment, TextAlignmentOptions.Left);
            var bar = UiKit.Image(s.Root, SpriteLibrary.Get("Sprites/UI/hpbar_frame"), new Vector2(31, 16), new Vector2(BarWidth, 8), "HpFrame");
            bar.rectTransform.pivot = new Vector2(0f, 0.5f);
            bar.raycastTarget = false;
            s.Fill = UiKit.Image(bar.transform, SpriteLibrary.Get("Sprites/UI/hpbar_fill"), new Vector2(2, 2), new Vector2(BarWidth - 4f, 4), "HpFill");
            s.Fill.rectTransform.pivot = Vector2.zero;
            s.Fill.raycastTarget = false;
            s.Hp = UiKit.Label(s.Root, "", new Vector2(52, 5), new Vector2(42, 10), UiFont.Label, Palette.Parchment, TextAlignmentOptions.Left);
            return s;
        }

        public void Destroy() { if (Root != null) Object.Destroy(Root.gameObject); }
    }
}
