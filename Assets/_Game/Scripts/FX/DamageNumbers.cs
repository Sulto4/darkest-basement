using System.Collections.Generic;
using DarkestBasement.Combat;
using DarkestBasement.Core;
using DarkestBasement.UI;
using UnityEngine;

namespace DarkestBasement.FX
{
    public enum NumberStyle { Damage, Crit, Heal, Poison, Bleed, Block, Blocked, Info }

    // Numerele care sar din ținte. Culoarea spune tipul (GDD §9): alb damage, galben crit,
    // verde otravă/vindecare, roșu bleed, albastru block. Animația folosește timp unscaled:
    // numărul „explodează” chiar în timpul hit-stop-ului, când restul lumii stă pe loc.
    public static class DamageNumbers
    {
        static RectTransform _layer;
        static readonly Dictionary<string, float> LastTime = new Dictionary<string, float>();
        static readonly Dictionary<string, int> StackCount = new Dictionary<string, int>();

        public static Color ColorOf(NumberStyle style)
        {
            switch (style)
            {
                case NumberStyle.Crit: return Palette.Hex("#f2d14a");
                case NumberStyle.Heal:
                case NumberStyle.Poison: return Palette.Hex("#7cc04a");
                case NumberStyle.Bleed: return Palette.Hex("#e04848");
                case NumberStyle.Block:
                case NumberStyle.Blocked: return Palette.Hex("#6fa8dc");
                case NumberStyle.Info: return Palette.Parchment;
                default: return Palette.White;
            }
        }

        // stackKey = de obicei id-ul unității: loviturile rapide succesive se așază una peste alta, nu suprapuse.
        public static void Spawn(Vector3 world, string text, NumberStyle style, string stackKey = null, Color? color = null)
        {
            if (UIRoot.Instance == null) return;
            EnsureLayer();
            Vector2 pos = CombatCamera.WorldToUI(world);
            if (pos.x < -100f) return;

            int stack = 0;
            if (stackKey != null)
            {
                float now = Core.GameTime.Unscaled;
                if (LastTime.TryGetValue(stackKey, out var last) && now - last < 0.45f) stack = StackCount[stackKey] + 1;
                LastTime[stackKey] = now; StackCount[stackKey] = stack;
            }

            bool crit = style == NumberStyle.Crit;
            // Mărimi întregi ale fontului pixel: damage ×3, crit ×4, restul ×2 (personajele sunt ×2 pe ecran).
            int mult = crit ? 4 : style == NumberStyle.Damage ? 3 : 2;
            var label = CombatUi.Outlined(_layer, text, pos, new Vector2(160, 40), UiFont.LabelBold, color ?? ColorOf(style), mult);
            var rt = label.Rect;
            // Mici decalaje: numerele succesive „urcă” în trepte și nu stau exact pe aceeași coloană.
            Vector2 start = pos + new Vector2((stack % 2 == 0 ? 1 : -1) * Mathf.Min(stack, 3) * 8f, stack * 16f);
            if (style == NumberStyle.Blocked) start += new Vector2(-16f, -6f);
            rt.anchoredPosition = CombatUi.Snap(start);
            rt.localScale = Vector3.zero;

            float rise = crit ? 22f : 16f;
            float popScale = crit ? 1.25f : 1f;
            // 1) pop cu depășire (OutBack), 2) plutește, 3) cade puțin și dispare.
            Tween.Value(0.18f, k =>
            {
                if (rt == null) return;
                rt.localScale = Vector3.one * Mathf.LerpUnclamped(0.2f, popScale, k);
                rt.anchoredPosition = CombatUi.Snap(start + new Vector2(0f, rise * k));
                if (crit) rt.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(k * 30f) * 8f * (1f - k));
            }, Ease.OutBack, unscaled: true, owner: rt, onComplete: () =>
            {
                if (rt == null) return;
                Vector2 top = start + new Vector2(0f, rise);
                Tween.Value(crit ? 0.6f : 0.45f, k =>
                {
                    if (rt == null) return;
                    rt.anchoredPosition = CombatUi.Snap(top + new Vector2(0f, 4f * k));
                    if (crit) rt.localScale = Vector3.one * Mathf.Lerp(popScale, 1f, k);
                }, Ease.OutQuad, unscaled: true, owner: rt, onComplete: () =>
                {
                    if (rt == null) return;
                    Vector2 hold = top + new Vector2(0f, 4f);
                    Tween.Value(0.3f, k =>
                    {
                        if (rt == null) return;
                        rt.anchoredPosition = CombatUi.Snap(hold + new Vector2(0f, -6f * k * k));
                        label.Alpha = 1f - k;
                    }, Ease.InQuad, unscaled: true, owner: rt, onComplete: () => { if (rt != null) Object.Destroy(rt.gameObject); });
                });
            });
        }

        static void EnsureLayer()
        {
            if (_layer != null) return;
            _layer = UiKit.Rect(UIRoot.Instance.World, "DamageNumbers", Vector2.zero, Vector2.zero, Vector2.zero);
            _layer.anchorMin = Vector2.zero; _layer.anchorMax = Vector2.one; _layer.offsetMin = _layer.offsetMax = Vector2.zero;
        }

        // Ținem stratul numerelor deasupra barelor de HP create după el.
        public static void BringToFront()
        {
            if (_layer != null) _layer.SetAsLastSibling();
        }
    }
}
