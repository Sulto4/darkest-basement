using UnityEngine;

namespace DarkestBasement.Core
{
    // Timpul „nescalat” (nu ține cont de Time.timeScale): UI, numere de damage, hit-stop, screen shake, cortina.
    // În joc e exact Time.unscaledTime / Time.unscaledDeltaTime. Când QA-ul capturează cadre pentru video (-capture-fps),
    // Unity avansează timpul jocului în pași ficși, dar unscaledTime merge mai departe în timp real; sub randare software
    // (~4 cadre/s) tot ce e nescalat ar alerga în clip de ~7 ori prea repede. Atunci și ceasul ăsta merge în pași de captură.
    public static class GameTime
    {
        static bool _capturing;
        static float _base;
        static int _baseFrame;

        public static float Unscaled
        {
            get
            {
                if (Time.captureDeltaTime <= 0f) { _capturing = false; return Time.unscaledTime; }
                // Prima citire în captură: pornim din valoarea reală de acum, ca ceasul să nu sară înapoi.
                if (!_capturing) { _capturing = true; _base = Time.unscaledTime; _baseFrame = Time.frameCount; }
                return _base + (Time.frameCount - _baseFrame) * Time.captureDeltaTime;
            }
        }

        public static float UnscaledDelta => Time.captureDeltaTime > 0f ? Time.captureDeltaTime : Time.unscaledDeltaTime;
    }
}
