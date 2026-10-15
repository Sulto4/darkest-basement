using DarkestBasement.Combat;
using UnityEngine;

namespace DarkestBasement.FX
{
    // Screen shake bazat pe „trauma” (tehnica din GDC-ul lui Squirrel Eiserloh).
    // - Fiecare lovitură adaugă trauma (0..1); trauma scade liniar în timp.
    // - Mișcarea = trauma²: loviturile mici abia se simt, cele mari se simt mult (curbă convexă).
    // - Direcția vine din zgomot Perlin, nu din Random: zgomotul e continuu, deci camera „tremură”, nu „sare”.
    public static class ScreenShake
    {
        public static float MaxOffset = 0.15f;  // unități de lume la trauma 1 (~10 px la 64 px/unitate în luptă)
        public static float MaxAngle = 2.2f;    // grade de rotație la trauma 1
        public static float Frequency = 18f;    // cât de repede se plimbă zgomotul
        public static float Decay = 1.5f;       // trauma pierdută pe secundă

        public static float Trauma { get; private set; }

        static Transform _applied;
        static bool _dirty;

        public static void Add(float trauma)
        {
            if (trauma <= 0f) return;
            FxHost.Ensure();
            Trauma = Mathf.Clamp01(Trauma + trauma);
        }

        public static void Clear()
        {
            Trauma = 0f;
            Tick();
        }

        internal static void Tick()
        {
            Transform root = CombatCamera.ShakeRoot;
            if (root != _applied)
            {
                if (_applied != null) { _applied.localPosition = Vector3.zero; _applied.localRotation = Quaternion.identity; }
                _applied = root;
            }
            if (root == null) return;

            if (Trauma <= 0f)
            {
                // Scriem zero o singură dată, ca să nu ne luptăm cu alt cod care mută rădăcina.
                if (_dirty) { root.localPosition = Vector3.zero; root.localRotation = Quaternion.identity; _dirty = false; }
                return;
            }

            float shake = Trauma * Trauma;
            // Timp real: shake-ul continuă și în timpul hit-stop-ului (arată foarte bine împreună).
            float t = Core.GameTime.Unscaled * Frequency;
            root.localPosition = new Vector3(MaxOffset * shake * Noise(0, t), MaxOffset * shake * Noise(1, t), 0f);
            root.localRotation = Quaternion.Euler(0f, 0f, MaxAngle * shake * Noise(2, t));
            _dirty = true;
            Trauma = Mathf.Max(0f, Trauma - Decay * Core.GameTime.UnscaledDelta);
        }

        // PerlinNoise dă 0..1; îl mutăm în -1..1. Fiecare canal citește alt „rând” din zgomot.
        static float Noise(int channel, float t) => Mathf.PerlinNoise(13.7f + channel * 41.3f, t) * 2f - 1f;
    }
}
