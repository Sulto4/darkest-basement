using System.Collections.Generic;
using DarkestBasement.Core;
using DarkestBasement.UI;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace DarkestBasement.Combat
{
    // Camera de rezervă a sandbox-ului de luptă, folosită doar când CameraRig (echipa W) nu există încă.
    // Face același lucru ca rig-ul: randează lumea într-un RenderTexture de 640×360 cu filtru Point,
    // afișat sub UI la scară întreagă, ca 3D-ul să arate pixelat la fel ca sprite-urile.
    public sealed class CombatSandboxCamera : MonoBehaviour
    {
        public Camera Camera { get; private set; }
        public Transform ShakeRoot { get; private set; }


        RenderTexture _rt;
        RawImage _view;
        Vector3 _punchOffset;
        float _punchStart, _punchDuration, _punchAmount;
        Vector3 _punchFocus;
        readonly List<SpriteRenderer> _torches = new List<SpriteRenderer>();
        readonly List<Sprite[]> _torchFrames = new List<Sprite[]>();

        public static CombatSandboxCamera Create(CombatStageInfo stage)
        {
            var go = new GameObject("[Sandbox Camera]");
            var rig = go.AddComponent<CombatSandboxCamera>();
            rig.Build(stage);
            CombatCamera.SetFallback(rig);
            return rig;
        }

        void Build(CombatStageInfo stage)
        {
            var pose = CombatCamera.CombatPose(stage);
            transform.SetPositionAndRotation(pose.position, pose.rotation);

            ShakeRoot = new GameObject("ShakeRoot").transform;
            ShakeRoot.SetParent(transform, false);
            var camGo = new GameObject("Camera");
            camGo.transform.SetParent(ShakeRoot, false);
            Camera = camGo.AddComponent<Camera>();
            Camera.fieldOfView = CombatCamera.Fov;
            Camera.nearClipPlane = 0.1f;
            Camera.farClipPlane = 80f;
            Camera.clearFlags = CameraClearFlags.SolidColor;
            Camera.backgroundColor = Palette.Hex("#120b0c");
            Camera.allowMSAA = false;
            Camera.allowHDR = true;

            _rt = new RenderTexture(UIRoot.RefWidth, UIRoot.RefHeight, 24, RenderTextureFormat.ARGB32) { filterMode = FilterMode.Point, name = "SandboxWorld" };
            Camera.targetTexture = _rt;
            var data = Camera.GetUniversalAdditionalCameraData();
            if (data != null) { data.renderPostProcessing = true; data.antialiasing = AntialiasingMode.None; }

            // Imaginea lumii stă sub toate straturile de UI (primul copil al rădăcinii).
            var ui = UIRoot.Create();
            var rt = UiKit.Rect(ui.Root, "WorldView", new Vector2(UIRoot.RefWidth / 2f, UIRoot.RefHeight / 2f), new Vector2(UIRoot.RefWidth, UIRoot.RefHeight));
            rt.SetAsFirstSibling();
            _view = rt.gameObject.AddComponent<RawImage>();
            _view.texture = _rt;
            _view.raycastTarget = false;

            BuildPostProcessing(go: gameObject);
        }

        // Post-procesarea din GDD §9 (Bloom, Vignette închisă, Film Grain, ton sepia-roșcat).
        // În joc o face echipa W; aici e doar ca sandbox-ul să arate cât mai aproape de joc.
        static void BuildPostProcessing(GameObject go)
        {
            var volume = go.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 1f;
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            var bloom = profile.Add<Bloom>(true);
            if (bloom != null) { bloom.threshold.Override(1f); bloom.intensity.Override(1.3f); bloom.scatter.Override(0.65f); }
            var vignette = profile.Add<Vignette>(true);
            if (vignette != null) { vignette.intensity.Override(0.38f); vignette.smoothness.Override(0.45f); vignette.color.Override(Color.black); }
            var grain = profile.Add<FilmGrain>(true);
            if (grain != null) grain.intensity.Override(0.22f);
            var color = profile.Add<ColorAdjustments>(true);
            if (color != null) { color.contrast.Override(8f); color.saturation.Override(-6f); color.colorFilter.Override(new Color(1f, 0.93f, 0.86f)); color.postExposure.Override(0f); color.hueShift.Override(0f); }
            volume.sharedProfile = profile;
        }

        public void SetPose(Pose pose, float fov)
        {
            transform.SetPositionAndRotation(pose.position, pose.rotation);
            if (Camera != null) Camera.fieldOfView = fov;
        }

        public void AddTorch(SpriteRenderer sr, Sprite[] frames)
        {
            _torches.Add(sr);
            _torchFrames.Add(frames);
        }

        // Punch-in: camera se apropie scurt de punctul de interes și revine (ca în Darkest Dungeon).
        public void PunchIn(Vector3 worldFocus, float amount, float duration)
        {
            _punchFocus = worldFocus;
            _punchAmount = Mathf.Clamp(amount, 0f, 0.4f);
            _punchDuration = Mathf.Max(0.05f, duration);
            _punchStart = Core.GameTime.Unscaled;
        }

        void LateUpdate()
        {
            if (Camera == null) return;
            float k = _punchDuration > 0f ? (Core.GameTime.Unscaled - _punchStart) / _punchDuration : 1f;
            Vector3 offset = Vector3.zero;
            if (k < 1f)
            {
                // Intră repede (primele 30%), iese lin: curba „lovitură, apoi respiră”.
                float w = k < 0.3f ? Easing.Evaluate(Ease.OutCubic, k / 0.3f) : 1f - Easing.Evaluate(Ease.InOutSine, (k - 0.3f) / 0.7f);
                Vector3 toFocus = _punchFocus - transform.position;
                offset = transform.InverseTransformDirection(toFocus) * _punchAmount * w;
            }
            Camera.transform.localPosition = offset;

            // Torțe: cadre animate + pâlpâire Perlin (doar estetică, nu afectează lupta).
            for (int i = 0; i < _torches.Count; i++)
            {
                var sr = _torches[i];
                if (sr == null) continue;
                var frames = _torchFrames[i];
                sr.sprite = frames[(int)(Time.time * 9f + i * 2) % frames.Length];
                float f = 0.85f + 0.15f * Mathf.PerlinNoise(i * 3.1f, Time.time * 4f);
                sr.color = new Color(f, f, f, 1f);
            }
        }

        void OnDestroy()
        {
            if (_view != null) Destroy(_view.gameObject);
            if (_rt != null) { if (Camera != null) Camera.targetTexture = null; _rt.Release(); Destroy(_rt); }
        }
    }
}
