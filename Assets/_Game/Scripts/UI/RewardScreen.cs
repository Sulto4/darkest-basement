using System;
using System.Collections;
using System.Collections.Generic;
using DarkestBasement.Audio;
using DarkestBasement.Core;
using DarkestBasement.FX;
using DarkestBasement.Model.Data;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace DarkestBasement.UI
{
    // Recompensa de după luptă (GDD §5): un pachet cade, se scutură, se deschide, cartea se întoarce.
    // Take = cartea zboară spre portretul eroului ei; Skip = cartea cade și dispare. onDone(true) = a luat-o.
    // Totul merge pe timp real (unscaled): ecranul funcționează și dacă jocul e încă încetinit.
    public static class RewardScreen
    {
        static readonly Vector2 CardPos = new Vector2(320, 192);
        static readonly Vector2 PortraitPos = new Vector2(320, 30);

        public static bool IsOpen { get; private set; }

        // QA: cine decide singur (de ex. AutoPlayer.ShouldTake). Fără el, QA-ul ia cartea după 2,5 s.
        public static Func<CardDef, bool> AutoDecide;

        static Coroutine _routine;
        static RectTransform _root;

        public static void Show(CardDef card, bool rare, Action<bool> onDone)
        {
            if (card == null) { onDone?.Invoke(false); return; }
            Cancel();
            UIRoot.Create();
            _routine = FxHost.Run(Routine(card, rare, onDone));
        }

        // Închide ecranul fără răspuns (de ex. Abandon run din pauză). onDone nu mai e chemat.
        public static void Cancel()
        {
            if (_routine != null && FxHost.Instance != null) FxHost.Instance.StopCoroutine(_routine);
            _routine = null;
            if (_root != null) UnityEngine.Object.Destroy(_root.gameObject);
            _root = null;
            IsOpen = false;
        }

        static IEnumerator Routine(CardDef card, bool rare, Action<bool> onDone)
        {
            IsOpen = true;
            var root = UiKit.Stretch(UIRoot.Instance.Screens, "RewardScreen");
            _root = root;
            var dim = UiKit.Dim(root, 0f);
            yield return Animate(0.3f, k => dim.color = new Color(0.02f, 0.01f, 0.01f, 0.72f * k));

            var title = CombatUi.Outlined(root, "SPOILS", new Vector2(320, 322), new Vector2(400, 40), UiFont.Title, Palette.Parchment, 2);

            // 1) Pachetul cade de sus și sare (OutBounce).
            var pack = CombatUi.Image(root, "Sprites/UI/card_back", new Vector2(320, 460));
            var packRt = pack.rectTransform;
            packRt.localScale = Vector3.one * 2f;
            AudioManager.Play("card_draw");
            yield return Animate(0.55f, k => packRt.anchoredPosition = CombatUi.Snap(Vector2.LerpUnclamped(new Vector2(320, 460), CardPos, Easing.Evaluate(Ease.OutBounce, k))));
            AudioManager.Play("card_play", 0.8f, 0.8f);
            Sparkles(root, CardPos + new Vector2(0, -88), 8, new Color(0.7f, 0.6f, 0.5f), 40f);

            // 2) Se scutură tot mai tare: anticiparea face deschiderea să conteze.
            yield return Animate(rare ? 0.75f : 0.5f, k =>
            {
                float amp = Mathf.Lerp(2f, rare ? 10f : 6f, k);
                packRt.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(k * (rare ? 70f : 50f)) * amp);
                packRt.anchoredPosition = CombatUi.Snap(CardPos + new Vector2(Mathf.Sin(k * 90f) * amp * 0.4f, 0f));
            });
            packRt.localRotation = Quaternion.identity;

            // 3) Se întoarce: spatele se strânge pe orizontală până la 0, apoi fața crește din 0.
            AudioManager.Play("card_flip_reward");
            if (rare) AudioManager.Play("card_reward_rare");
            // Flash-ul lumii (ScreenFlash) stă sub ecranul de recompensă: aici avem flash-ul nostru, peste pachet.
            var flash = UiKit.Fill(root, rare ? Palette.Gold : Color.white, new Vector2(320, 180), new Vector2(UIRoot.RefWidth, UIRoot.RefHeight));
            // Spațiu de culoare liniar: alfa ridicat la 2,2 (vezi ScreenFlash).
            float peak = Mathf.Pow(rare ? 0.45f : 0.3f, QualitySettings.activeColorSpace == ColorSpace.Linear ? 2.2f : 1f);
            FxHost.Run(Animate(0.3f, k => { if (flash != null) { var c = flash.color; c.a = peak * (1f - k); flash.color = c; } }));
            yield return Animate(0.12f, k => packRt.localScale = new Vector3(2f * (1f - k), 2f, 1f));
            UnityEngine.Object.Destroy(pack.gameObject);

            var glow = CombatUi.Image(root, "Sprites/UI/card_glow", CardPos, new Vector2(72, 98) * 2f);
            glow.color = rare ? Palette.Gold : RarityTint(card.Rarity);
            var view = CardView.Create(root, card, false, CardPos);
            view.Interactable = false;
            var cardRt = view.Rect;
            yield return Animate(0.16f, k => cardRt.localScale = new Vector3(2f * Easing.Evaluate(Ease.OutBack, k), 2f, 1f));
            Sparkles(root, CardPos, rare ? 26 : 12, rare ? Palette.Gold : Palette.Parchment, rare ? 120f : 80f);
            title.Text = rare ? "RARE CARD!" : "NEW CARD";
            title.Color = rare ? Palette.Gold : Palette.Parchment;
            QA.QaHooks.Moment("reward_reveal");

            // Al cui e: portretul eroului, jos.
            string heroName = HeroDatabase.Get(card.HeroId).Name.ToUpperInvariant();
            var frame = CombatUi.Image(root, "Sprites/UI/portrait_frame_active", PortraitPos);
            var portrait = CombatUi.Image(root, SpriteLibrary.TryGet("Sprites/Heroes/" + card.HeroId + "/portrait"), PortraitPos + new Vector2(0, 2), new Vector2(24, 24), "Portrait");
            var owner = CombatUi.Outlined(root, "FOR " + heroName, PortraitPos + new Vector2(0, 26), new Vector2(120, 10), UiFont.LabelBold, Palette.Parchment);

            // 4) Take / Skip (sau Enter / Esc).
            bool? choice = null;
            var take = CombatUi.Button(root, "TAKE", new Vector2(250, 82), new Vector2(72, 22), () => choice = true, UiFont.LabelBold);
            var skip = CombatUi.Button(root, "SKIP", new Vector2(390, 82), new Vector2(72, 22), () => choice = false, UiFont.LabelBold);
            float time = 0f;
            while (choice == null)
            {
                time += Time.unscaledDeltaTime;
                // Strălucirea respiră; cartea plutește 1 px.
                var c = glow.color; c.a = 0.55f + 0.35f * Mathf.Sin(time * 3f); glow.color = c;
                cardRt.anchoredPosition = CombatUi.Snap(CardPos + new Vector2(0, Mathf.Sin(time * 2f) * 1.5f));
                var kb = Keyboard.current;
                // Escape aparține meniului de pauză (W); aici doar Enter/Space = Take, cât timp jocul nu e pe pauză.
                if (kb != null && !GameFlow.Paused && (kb.enterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame)) { AudioManager.Play("ui_click"); choice = true; }
                if (QA.QaHooks.Active && time > 2.5f) choice = AutoDecide != null ? AutoDecide(card) : true;
                yield return null;
            }
            take.interactable = false;
            skip.interactable = false;
            UnityEngine.Object.Destroy(glow.gameObject);

            if (choice.Value)
            {
                // Cartea zboară în portretul eroului, care clipește (GDD §5).
                Vector2 from = cardRt.anchoredPosition;
                yield return Animate(0.42f, k =>
                {
                    Vector2 p = Vector2.LerpUnclamped(from, PortraitPos, Easing.Evaluate(Ease.InCubic, k)) + Vector2.up * Mathf.Sin(k * Mathf.PI) * 40f;
                    cardRt.anchoredPosition = CombatUi.Snap(p);
                    cardRt.localScale = Vector3.one * Mathf.Lerp(2f, 0.15f, Easing.Evaluate(Ease.InQuad, k));
                    cardRt.localRotation = Quaternion.Euler(0f, 0f, -360f * Easing.Evaluate(Ease.InQuad, k));
                });
                UnityEngine.Object.Destroy(view.gameObject);
                AudioManager.Play("buff");
                Sparkles(root, PortraitPos, 16, Palette.Gold, 70f);
                owner.Text = "ADDED TO " + heroName + "'S DECK";
                owner.Color = Palette.Gold;
                var frameRt = frame.rectTransform;
                yield return Animate(0.35f, k =>
                {
                    float s = 1f + Mathf.Sin(k * Mathf.PI) * 0.6f;
                    frameRt.localScale = Vector3.one * s;
                    portrait.rectTransform.localScale = Vector3.one * s;
                    portrait.color = Color.Lerp(Color.white, new Color(1f, 0.9f, 0.5f), Mathf.Sin(k * Mathf.PI * 3f) * 0.5f + 0.5f);
                });
                portrait.color = Color.white;
                yield return Wait(0.35f);
            }
            else
            {
                // Skip: cartea cade și se stinge.
                Vector2 from = cardRt.anchoredPosition;
                yield return Animate(0.35f, k =>
                {
                    cardRt.anchoredPosition = CombatUi.Snap(from + new Vector2(0, -160f * k * k));
                    cardRt.localRotation = Quaternion.Euler(0f, 0f, 25f * k);
                    view.Alpha = 1f - k;
                });
                UnityEngine.Object.Destroy(view.gameObject);
            }

            var group = root.gameObject.AddComponent<CanvasGroup>();
            yield return Animate(0.25f, k => group.alpha = 1f - k);
            UnityEngine.Object.Destroy(root.gameObject);
            _root = null;
            _routine = null;
            IsOpen = false;
            try { onDone?.Invoke(choice.Value); }
            catch (Exception e) { Debug.LogException(e); }
        }

        static Color RarityTint(Rarity r) => r == Rarity.Uncommon ? Palette.Block : new Color(0.85f, 0.82f, 0.78f);

        // Rulează f(k), k de la 0 la 1, pe durata dată (timp real).
        static IEnumerator Animate(float duration, Action<float> f)
        {
            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                f(Mathf.Clamp01(t / duration));
                yield return null;
            }
            f(1f);
        }

        static IEnumerator Wait(float seconds)
        {
            float t = 0f;
            while (t < seconds) { t += Time.unscaledDeltaTime; yield return null; }
        }

        // Scântei UI (plus-uri de 3×3) care zboară din centru și cad.
        static void Sparkles(RectTransform parent, Vector2 at, int count, Color color, float speed)
        {
            var parts = new List<(RectTransform rt, Vector2 vel, Image img)>();
            for (int i = 0; i < count; i++)
            {
                var img = CombatUi.Image(parent, PixelSprites.Plus, at, new Vector2(3, 3), "Sparkle");
                img.color = color;
                float a = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
                var vel = new Vector2(Mathf.Cos(a), Mathf.Sin(a) * 0.8f + 0.4f) * UnityEngine.Random.Range(0.4f, 1f) * speed;
                parts.Add((img.rectTransform, vel, img));
            }
            FxHost.Run(SparkleRoutine(parts));
        }

        static IEnumerator SparkleRoutine(List<(RectTransform rt, Vector2 vel, Image img)> parts)
        {
            float t = 0f;
            var pos = new List<Vector2>(); var vel = new List<Vector2>();
            foreach (var p in parts) { pos.Add(p.rt.anchoredPosition); vel.Add(p.vel); }
            while (t < 0.9f)
            {
                float dt = Time.unscaledDeltaTime;
                t += dt;
                for (int i = 0; i < parts.Count; i++)
                {
                    if (parts[i].rt == null) continue;
                    vel[i] += Vector2.down * 160f * dt;
                    pos[i] += vel[i] * dt;
                    parts[i].rt.anchoredPosition = CombatUi.Snap(pos[i]);
                    var c = parts[i].img.color; c.a = 1f - t / 0.9f; parts[i].img.color = c;
                }
                yield return null;
            }
            foreach (var p in parts) if (p.rt != null) UnityEngine.Object.Destroy(p.rt.gameObject);
        }
    }
}
