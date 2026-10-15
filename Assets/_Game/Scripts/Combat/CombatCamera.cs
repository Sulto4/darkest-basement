using System.Collections;
using DarkestBasement.Core;
using DarkestBasement.Exploration;
using DarkestBasement.UI;
using UnityEngine;

namespace DarkestBasement.Combat
{
    // Adaptor între luptă și cameră. În joc, camera este CameraRig (echipa W).
    // În sandbox, dacă rig-ul lipsește, lupta folosește camera ei de rezervă (CombatSandboxCamera).
    // Restul codului de luptă vorbește numai cu clasa asta, deci nu-i pasă care dintre ele există.
    public static class CombatCamera
    {
        static CombatSandboxCamera _fallback;

        static CameraRig Rig => CameraRig.Instance != null && CameraRig.Instance.Camera != null ? CameraRig.Instance : null;

        public static Camera Camera
        {
            get
            {
                var rig = Rig;
                if (rig != null) return rig.Camera;
                if (_fallback != null) return _fallback.Camera;
                return UnityEngine.Camera.main;
            }
        }

        public static Transform ShakeRoot
        {
            get
            {
                var rig = Rig;
                if (rig != null) return rig.ShakeRoot;
                return _fallback != null ? _fallback.ShakeRoot : null;
            }
        }

        internal static void SetFallback(CombatSandboxCamera cam) { _fallback = cam; }

        // ---------- încadrarea luptei ----------
        // Vederea laterală a lui W (CameraRig.SidePose) arată 10 unități în lățime pe planul scenei:
        // 640 px / 10 = 64 px pe unitate, adică fiecare pixel de artă devine exact 2×2 pixeli. Scară ÎNTREAGĂ = pixel art curat.
        // Camera de rezervă a sandbox-ului folosește exact aceeași poză, ca lupta să arate la fel cu sau fără W.
        public static float Fov => CameraRig.SideFov;
        public static Pose CombatPose(CombatStageInfo stage) => CameraRig.SidePose(stage);

        // Centrul scenei curente: punch-in-urile sunt trase spre el, ca eroul de pe rangul 4 să nu iasă din cadru.
        public static Vector3? StageFocus;

        // Cu rig-ul lui W nu facem nimic: GoToSideView a adus deja camera aici. Camera de rezervă sare direct.
        public static IEnumerator EnterCombatFraming(CombatStageInfo stage, float duration)
        {
            if (Rig == null && _fallback != null) _fallback.SetPose(CombatPose(stage), Fov);
            yield break;
        }

        // Zoom cinematic scurt pe un moment important (crit, Brimstone, atac de boss). Revine singur.
        public static void PunchIn(Vector3 worldFocus, float amount = 0.12f, float duration = 0.35f)
        {
            // Punch-in discret: cel mult ~7% zoom, cu focusul tras spre centrul scenei.
            // Altfel, la un atac pe un erou din spate, camera s-ar roti și l-ar scoate pe cel de pe rangul 4 din cadru.
            amount = Mathf.Min(amount, 0.07f);
            if (StageFocus.HasValue) worldFocus = Vector3.Lerp(StageFocus.Value, worldFocus, 0.35f);
            var rig = Rig;
            if (rig != null) rig.PunchIn(worldFocus, amount, duration);
            else if (_fallback != null) _fallback.PunchIn(worldFocus, amount, duration);
        }

        // Poziția din lume → pixel în UI-ul de 640×360 (origine jos-stânga, ca la UiKit).
        // Lumea se randează într-un RenderTexture exact cât UIRoot.Root, deci viewport-ul camerei (0..1)
        // corespunde direct suprafeței UI-ului. Așa nu depindem de rezoluția ferestrei.
        public static Vector2 WorldToUI(Vector3 world)
        {
            var cam = Camera;
            if (cam == null) return new Vector2(-999f, -999f);
            Vector3 vp = cam.WorldToViewportPoint(world);
            if (vp.z <= 0f) return new Vector2(-999f, -999f);
            return new Vector2(vp.x * UIRoot.RefWidth, vp.y * UIRoot.RefHeight);
        }

        // Rotația pentru sprite-urile „billboard”: privesc mereu spre cameră, dar stau drepte (doar rotație în jurul lui Y).
        public static Quaternion Billboard
        {
            get
            {
                var cam = Camera;
                if (cam == null) return Quaternion.identity;
                Vector3 f = cam.transform.forward; f.y = 0f;
                if (f.sqrMagnitude < 1e-4f) f = Vector3.forward;
                return Quaternion.LookRotation(f.normalized, Vector3.up);
            }
        }

        // Rotația completă a camerei (pentru particule și raze: stau mereu cu fața spre ecran).
        public static Quaternion Facing
        {
            get
            {
                var cam = Camera;
                return cam != null ? Quaternion.LookRotation(cam.transform.forward, Vector3.up) : Quaternion.identity;
            }
        }
    }
}
