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

        public static UIRoot Instance { get; private set; }

        public Canvas Canvas { get; private set; }
        // Straturi, de jos în sus: World (bare de HP peste unități) → Hud → Screens (meniuri, recompense) → Overlay (tooltip, fade).
        public RectTransform Root { get; private set; }
        public RectTransform World { get; private set; }
        public RectTransform Hud { get; private set; }
        public RectTransform Screens { get; private set; }
        public RectTransform Overlay { get; private set; }

        public int Scale { get; private set; } = 1;

        CanvasScaler _scaler;
        Vector2Int _lastScreen;

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
            World = NewLayer("World", Root, false);
            Hud = NewLayer("Hud", Root, false);
            Screens = NewLayer("Screens", Root, false);
            Overlay = NewLayer("Overlay", Root, false);
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

        // Transformă o poziție din lume (de ex. capul unui inamic) în coordonate de UI, ca să pui o bară de HP deasupra.
        public Vector2 WorldToUI(Camera cam, Vector3 worldPos)
        {
            Vector3 screen = cam.WorldToScreenPoint(worldPos);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(Root, screen, null, out var local);
            return local;
        }
    }
}
