using DarkestBasement.Exploration;
using DarkestBasement.UI;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace DarkestBasement.Core
{
    // Lumea 3D se randează într-o imagine mică de 640×360 (RenderTexture, filtru Point),
    // apoi imaginea e afișată mărită cu un factor ÎNTREG sub UI. Așa pereții 3D, sprite-urile și UI-ul
    // au pixeli de aceeași mărime și totul arată ca un singur desen pixel art.
    public sealed class WorldView : MonoBehaviour
    {
        public const int Width = UIRoot.RefWidth;
        public const int Height = UIRoot.RefHeight;

        public static WorldView Instance { get; private set; }

        public RenderTexture Target { get; private set; }
        public RawImage Display { get; private set; }
        public Volume Volume { get; private set; }
        public VolumeProfile Profile { get; private set; }
        public CameraRig Rig { get; private set; }

        Vignette _vignette;
        float _vignetteBoost;

        public static WorldView Create(UIRoot ui)
        {
            if (Instance != null) return Instance;
            var go = new GameObject("[World]");
            DontDestroyOnLoad(go);
            var view = go.AddComponent<WorldView>();
            view.Build(ui);
            return view;
        }

        void Build(UIRoot ui)
        {
            Instance = this;

            // Imaginea lumii: 640×360, fără anti-aliasing (MSAA ar înmuia marginile pixelilor), cu buffer de adâncime pentru 3D.
            Target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Default)
            {
                name = "WorldRT",
                filterMode = FilterMode.Point,
                antiAliasing = 1,
                useMipMap = false,
                autoGenerateMips = false,
            };
            Target.Create();

            Rig = CameraRig.Create(transform);
            var cam = Rig.Camera;
            cam.targetTexture = Target;
            cam.depth = -10;
            cam.allowMSAA = false;
            cam.allowHDR = true;
            var camData = cam.GetUniversalAdditionalCameraData();
            camData.renderPostProcessing = true;
            camData.antialiasing = AntialiasingMode.None;
            camData.renderShadows = false;
            camData.volumeLayerMask = ~0;

            // A doua cameră doar curăță ecranul cu negru (benzile din jurul imaginii, dacă rezoluția nu e multiplu exact).
            var screenGo = new GameObject("ScreenCamera");
            screenGo.transform.SetParent(transform, false);
            var screenCam = screenGo.AddComponent<Camera>();
            screenCam.cullingMask = 0;
            screenCam.clearFlags = CameraClearFlags.SolidColor;
            screenCam.backgroundColor = Color.black;
            screenCam.depth = 10;
            screenCam.allowMSAA = false;
            screenCam.allowHDR = false;
            var screenData = screenCam.GetUniversalAdditionalCameraData();
            screenData.renderPostProcessing = false;
            screenData.renderShadows = false;

            // Imaginea lumii stă în stratul Backdrop al UI-ului: sub barele de HP, sub HUD și sub meniuri.
            var rt = UiKit.Rect(ui.Backdrop, "WorldImage", Vector2.zero, new Vector2(Width, Height), Vector2.zero);
            Display = rt.gameObject.AddComponent<RawImage>();
            Display.texture = Target;
            Display.raycastTarget = false;

            // Volum global cu efectele construite în PostFx.
            var volumeGo = new GameObject("PostFx");
            volumeGo.transform.SetParent(transform, false);
            Volume = volumeGo.AddComponent<Volume>();
            Volume.isGlobal = true;
            Volume.priority = 10;
            Profile = PostFx.CreateProfile();
            Volume.sharedProfile = Profile;
            Profile.TryGet(out _vignette);
        }

        // Puls scurt de vignetă (lovituri, capcane): colțurile se strâng un pic și revin.
        public void PulseVignette(float amount, float duration = 0.4f)
        {
            _vignetteBoost = Mathf.Max(_vignetteBoost, amount);
            Tween.Value(duration, k => _vignetteBoost = Mathf.Lerp(amount, 0f, k), Ease.OutQuad, null, 0f, true, this);
        }

        // Vignetă suplimentară permanentă (de ex. meniul principal mai întunecat).
        public float ExtraVignette { get; set; }

        void LateUpdate()
        {
            if (_vignette != null)
                _vignette.intensity.value = Mathf.Clamp01(PostFx.BaseVignette + ExtraVignette + _vignetteBoost);
        }

        void OnDestroy()
        {
            if (Target != null) Target.Release();
        }
    }
}
