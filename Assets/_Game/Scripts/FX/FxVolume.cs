using DarkestBasement.Core;
using DarkestBasement.UI;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace DarkestBasement.FX
{
    // Pulsuri de post-procesare pentru momentele mari (Brimstone = aberație cromatică, GDD §9).
    // Avem un Volume global numai al nostru, cu prioritate mare, care suprascrie doar ChromaticAberration.
    // Greutatea lui (weight) crește și scade; restul post-procesării (W) rămâne neatinsă.
    public static class FxVolume
    {
        static Volume _volume;
        static ChromaticAberration _ca;
        static float _start, _duration, _peak;

        public static void Chromatic(float intensity, float duration)
        {
            if (!Ensure()) return;
            FxHost.Ensure();
            _peak = Mathf.Max(_peak * Remaining(), Mathf.Clamp01(intensity));
            _start = Time.unscaledTime;
            _duration = Mathf.Max(0.05f, duration);
        }

        static float Remaining() => _duration <= 0f ? 0f : Mathf.Clamp01(1f - (Time.unscaledTime - _start) / _duration);

        internal static void Tick()
        {
            if (_volume == null) return;
            float k = Remaining();
            // Curbă „atac rapid, coadă lungă”: pornește la maxim și se stinge pătratic.
            _volume.weight = _peak * k * k;
            if (k <= 0f) _peak = 0f;
        }

        static bool Ensure()
        {
            if (_volume != null) return true;
            var go = new GameObject("[FX Volume]");
            Object.DontDestroyOnLoad(go);
            _volume = go.AddComponent<Volume>();
            _volume.isGlobal = true;
            _volume.priority = 100f;
            _volume.weight = 0f;
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            _ca = profile.Add<ChromaticAberration>(true);
            if (_ca == null) { Object.Destroy(go); _volume = null; return false; }
            _ca.intensity.Override(1f);
            _volume.sharedProfile = profile;
            return true;
        }
    }

    // Flash pe tot ecranul (alb la crit-uri mari, roșu la lovituri de boss). Foarte scurt și discret.
    public static class ScreenFlash
    {
        static Image _image;

        public static void Flash(Color color, float alpha, float duration)
        {
            if (UIRoot.Instance == null) return;
            if (_image == null)
            {
                // Flash-ul acoperă doar lumea (stratul Backdrop), nu și HUD-ul: cifrele și cărțile rămân lizibile.
                _image = UiKit.Image(UIRoot.Instance.Backdrop, PixelSprites.Pixel, new Vector2(CombatUi.W / 2f, CombatUi.H / 2f), new Vector2(CombatUi.W, CombatUi.H), "ScreenFlash");
                _image.raycastTarget = false;
            }
            _image.transform.SetAsLastSibling();
            var img = _image;
            Tween.KillAll(img);
            // Proiectul e în spațiu de culoare liniar: UI-ul amestecă alfa în valori liniare, deci 8% alb peste negru
            // apare pe ecran ca ~31% gri (ceață). Ridicăm alfa la puterea 2,2 ca „alpha” să însemne ce vede ochiul.
            if (QualitySettings.activeColorSpace == ColorSpace.Linear) alpha = Mathf.Pow(Mathf.Clamp01(alpha), 2.2f);
            color.a = alpha;
            img.color = color;
            img.enabled = true;
            Tween.Value(duration, k => { if (img != null) { var c = img.color; c.a = alpha * (1f - k); img.color = c; } }, Ease.OutQuad,
                () => { if (img != null) img.enabled = false; }, 0f, true, img);
        }
    }
}
