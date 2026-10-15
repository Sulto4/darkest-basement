using DarkestBasement.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace DarkestBasement.UI
{
    // Canvas-ul unic al jocului. Tot UI-ul e desenat la 640×360 și mărit cu un factor ÎNTREG (×2, ×3, ×4),
    // ca fiecare pixel din artă să devină un pătrat perfect (pixel-perfect). Ce rămâne pe margini e bandă neagră.
    public sealed class UIRoot : MonoBehaviour
    {
        public const int RefWidth = 640;
        public const int RefHeight = 360;
        public const float PixelsPerUnit = 32f;

        public static UIRoot Instance { get; private set; }

        public Canvas Canvas { get; private set; }
        // Straturi, de jos în sus: Backdrop (imaginea lumii 3D) → World (bare de HP peste unități) → Hud → Screens (meniuri, recompense)
        // → Fade (cortina neagră a tranzițiilor) → Overlay (tooltip, hint, cartonașul de etaj).
        public RectTransform Root { get; private set; }
        public RectTransform Backdrop { get; private set; }
        public RectTransform World { get; private set; }
        public RectTransform Hud { get; private set; }
        public RectTransform Screens { get; private set; }
        public RectTransform Fade { get; private set; }
        public RectTransform Overlay { get; private set; }

        public int Scale { get; private set; } = 1;

        CanvasScaler _scaler;
        Vector2Int _lastScreen;
        Image _curtain;
        TweenHandle _curtainTween;

        public static UIRoot Create()
        {
            if (Instance != null) return Instance;
            var go = new GameObject("[UI]");
            DontDestroyOnLoad(go);
            var root = go.AddComponent<UIRoot>();
            root.Build();
            return root;
        }

        void Build()
        {
            Instance = this;
            Canvas = gameObject.AddComponent<Canvas>();
            Canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            Canvas.pixelPerfect = true;
            Canvas.sortingOrder = 100;
            _scaler = gameObject.AddComponent<CanvasScaler>();
            _scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            // Sprite-urile au 32 px pe unitate. Cu aceeași valoare aici, un pixel de artă = o unitate UI,
            // iar marginile 9-slice ale panourilor au exact mărimea desenată.
            _scaler.referencePixelsPerUnit = PixelsPerUnit;
            gameObject.AddComponent<GraphicRaycaster>();

            // Fără EventSystem nu merge niciun click în UI.
            if (FindAnyObjectByType<EventSystem>() == null)
            {
                var es = new GameObject("[EventSystem]");
                DontDestroyOnLoad(es);
                es.AddComponent<EventSystem>();
                es.AddComponent<InputSystemUIInputModule>();
            }

            Root = NewLayer("Root", transform, true);
            Root.anchorMin = Root.anchorMax = new Vector2(0.5f, 0.5f);
            Root.sizeDelta = new Vector2(RefWidth, RefHeight);
            Backdrop = NewLayer("Backdrop", Root, false);
            World = NewLayer("World", Root, false);
            Hud = NewLayer("Hud", Root, false);
            Screens = NewLayer("Screens", Root, false);
            Fade = NewLayer("Fade", Root, false);
            Overlay = NewLayer("Overlay", Root, false);

            // Cortina: un dreptunghi negru peste tot jocul, mai mare decât ecranul ca să acopere și benzile laterale.
            var curtainRt = UiKit.Rect(Fade, "Curtain", new Vector2(RefWidth / 2f, RefHeight / 2f), new Vector2(RefWidth * 4, RefHeight * 4));
            _curtain = curtainRt.gameObject.AddComponent<Image>();
            _curtain.color = new Color(0f, 0f, 0f, 1f);
            _curtain.raycastTarget = false;
            UpdateScale();
        }

        static RectTransform NewLayer(string name, Transform parent, bool isRoot)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            if (!isRoot)
            {
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = rt.offsetMax = Vector2.zero;
            }
            return rt;
        }

        void Update()
        {
            var size = new Vector2Int(Screen.width, Screen.height);
            if (size != _lastScreen) UpdateScale();
        }

        // Cel mai mare factor întreg care încape în ecran: 1920×1080 → ×3, 2560×1440 → ×4.
        void UpdateScale()
        {
            _lastScreen = new Vector2Int(Screen.width, Screen.height);
            Scale = Mathf.Max(1, Mathf.Min(Screen.width / RefWidth, Screen.height / RefHeight));
            _scaler.scaleFactor = Scale;
        }

        // ---- Cortina (fade la negru) ----

        public float CurtainAlpha => _curtain != null ? _curtain.color.a : 0f;

        // Merge cu timp "unscaled": funcționează și când jocul e pe pauză sau în hit-stop.
        public TweenHandle FadeTo(float alpha, float duration)
        {
            _curtainTween?.Kill();
            float from = _curtain.color.a;
            _curtain.raycastTarget = alpha > 0.5f; // cât e întuneric, click-urile nu trec prin cortină
            _curtainTween = Tween.Value(duration, k =>
            {
                var c = _curtain.color; c.a = Mathf.Lerp(from, alpha, k); _curtain.color = c;
            }, Ease.InOutSine, null, 0f, true, _curtain);
            return _curtainTween;
        }

        public TweenHandle FadeOut(float duration = 0.35f) => FadeTo(1f, duration);
        public TweenHandle FadeIn(float duration = 0.45f) => FadeTo(0f, duration);

        // ---- Conversii ----

        // Poziția unui punct din lume în coordonatele lui Root: (0,0) = colțul stânga-jos, (640,360) = dreapta-sus.
        // Sunt exact coordonatele pe care le folosește UiKit.Rect într-un strat întins pe tot Root-ul (World, Hud...).
        public Vector2 WorldToUI(Camera cam, Vector3 worldPos)
        {
            Vector3 p = cam.WorldToScreenPoint(worldPos);
            if (p.z < 0f) return new Vector2(-10000f, -10000f); // punctul e în spatele camerei
            if (cam.targetTexture != null)
                return new Vector2(p.x * RefWidth / cam.pixelWidth, p.y * RefHeight / cam.pixelHeight);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(Root, p, null, out var local);
            return local + Root.rect.size * 0.5f;
        }

        // Din coordonatele lui Root (640×360) în pixeli de ecran: inversul lui ScreenToUI.
        // Canvas-ul e Screen Space Overlay, deci poziția „în lume” a unui punct din UI chiar e pixelul de pe ecran.
        public Vector2 UIToScreen(Vector2 ui)
        {
            Vector3 world = Root.TransformPoint(ui - Root.rect.size * 0.5f);
            return new Vector2(world.x, world.y);
        }

        // Din pixeli de ecran (mouse) în coordonatele lui Root (stânga-jos = 0,0).
        public Vector2 ScreenToUI(Vector2 screenPos)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(Root, screenPos, null, out var local);
            return local + Root.rect.size * 0.5f;
        }
    }
}
