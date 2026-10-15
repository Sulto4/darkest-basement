using UnityEngine;

namespace DarkestBasement.FX
{
    // Hit-stop: lumea îngheață 60–150 ms în momentul impactului. Creierul citește pauza ca „greutate”.
    // Înghețăm prin Time.timeScale = 0; UI-ul și numerele de damage folosesc timp unscaled, deci se mișcă în continuare.
    public static class HitStop
    {
        static float _base = 1f;
        static float _until;

        // Viteza „normală”: 1, sau 2 când jucătorul apasă F în luptă. După un hit-stop revenim la ea.
        public static float BaseTimeScale
        {
            get => _base;
            set
            {
                _base = Mathf.Clamp(value, 0.05f, 4f);
                // Pauza (meniul lui W) are ultimul cuvânt: nu repornim timpul sub ea.
                if (!IsFrozen && !Core.GameFlow.Paused) Time.timeScale = _base;
            }
        }

        public static bool IsFrozen => _until > 0f && Core.GameTime.Unscaled < _until;

        public static void Freeze(float seconds)
        {
            if (seconds <= 0f) return;
            FxHost.Ensure();
            // La viteză ×2 și pauzele sunt mai scurte, altfel lupta rapidă ar părea sacadată.
            float real = seconds / Mathf.Max(1f, _base);
            _until = Mathf.Max(_until, Core.GameTime.Unscaled + real);
            Time.timeScale = 0f;
        }

        // Ieșire forțată (la sfârșitul luptei), ca jocul să nu rămână înghețat.
        public static void Reset()
        {
            _until = 0f;
            _base = 1f;
            Time.timeScale = 1f;
        }

        internal static void Tick()
        {
            if (_until > 0f && Core.GameTime.Unscaled >= _until)
            {
                _until = 0f;
                if (!Core.GameFlow.Paused) Time.timeScale = _base;
            }
        }
    }
}
