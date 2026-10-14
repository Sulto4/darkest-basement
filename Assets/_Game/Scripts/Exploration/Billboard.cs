using DarkestBasement.Core;
using UnityEngine;

namespace DarkestBasement.Exploration
{
    // Un sprite pixel art în lumea 3D: un quad care se rotește mereu spre cameră (doar pe axa Y, ca să stea drept).
    // Mărimea vine din sprite: 32 px de artă = 1 unitate. Pivotul e cel din import (picioarele, pentru personaje).
    // Culoarea e lumina locală (LocalLight) × Tint × Brightness, așa că sprite-ul se întunecă între torțe.
    public sealed class Billboard : MonoBehaviour
    {
        public Color Tint = Color.white;
        public float Brightness = 1f;      // > 1 = strălucește (bloom), pentru flăcări
        public bool UseLocalLight = true;
        public bool FaceCamera = true;
        public bool FlipX;
        public float Scale = 1f;

        public Sprite Sprite { get; private set; }
        public MeshRenderer Renderer { get; private set; }
        public Material Material { get; private set; }
        public Transform Quad { get; private set; }

        Sprite[] _frames;
        float _fps, _time;
        bool _loop;
        Color _flashColor;
        float _flash;

        public static Billboard Create(Transform parent, string name, Sprite sprite, Vector3 localPos, MaterialKind kind = MaterialKind.UnlitCutout)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            var b = go.AddComponent<Billboard>();
            b.Init(kind);
            b.SetSprite(sprite);
            return b;
        }

        void Init(MaterialKind kind)
        {
            var quad = new GameObject("Quad");
            quad.transform.SetParent(transform, false);
            Quad = quad.transform;
            quad.AddComponent<MeshFilter>().sharedMesh = MeshKit.BillboardQuad;
            Renderer = quad.AddComponent<MeshRenderer>();
            Renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            Renderer.receiveShadows = false;
            Material = Materials.Create(kind);
            Renderer.sharedMaterial = Material;
        }

        public void SetSprite(Sprite sprite)
        {
            if (sprite == null || sprite == Sprite) return;
            Sprite = sprite;
            var tex = sprite.texture;
            Material.SetTexture(Materials.BaseMap, tex);
            // Zona sprite-ului în textură (contează doar dacă sprite-ul e dintr-un atlas).
            Rect r = sprite.textureRect;
            Material.SetTextureScale(Materials.BaseMap, new Vector2(r.width / tex.width, r.height / tex.height));
            Material.SetTextureOffset(Materials.BaseMap, new Vector2(r.x / tex.width, r.y / tex.height));
            ApplySize();
        }

        void ApplySize()
        {
            if (Sprite == null) return;
            float ppu = Sprite.pixelsPerUnit;
            float w = Sprite.rect.width / ppu * Scale, h = Sprite.rect.height / ppu * Scale;
            Vector2 pivot = new Vector2(Sprite.pivot.x / Sprite.rect.width, Sprite.pivot.y / Sprite.rect.height);
            Quad.localScale = new Vector3(FlipX ? -w : w, h, 1f);
            Quad.localPosition = new Vector3((0.5f - pivot.x) * w * (FlipX ? -1f : 1f), -pivot.y * h, 0f);
        }

        public void SetScale(float scale) { Scale = scale; ApplySize(); }
        public void SetFlip(bool flip) { FlipX = flip; ApplySize(); }

        // Animație în buclă (sau o singură dată) dintr-un șir de cadre.
        public void Play(Sprite[] frames, float fps, bool loop = true, float startTime = 0f)
        {
            _frames = frames;
            _fps = fps;
            _loop = loop;
            _time = startTime;
            if (frames != null && frames.Length > 0) SetSprite(frames[FrameIndex()]);
        }

        public void Stop() => _frames = null;

        int FrameIndex()
        {
            int i = Mathf.FloorToInt(_time * _fps);
            return _loop ? i % _frames.Length : Mathf.Min(i, _frames.Length - 1);
        }

        // Clipire scurtă de culoare (lovitură = alb/roșu, vindecare = verde).
        public void Flash(Color color, float seconds = 0.12f)
        {
            _flashColor = color;
            _flash = seconds;
        }

        public void SetVisible(bool visible) => Renderer.enabled = visible;

        void LateUpdate()
        {
            if (_frames != null && _frames.Length > 0)
            {
                _time += Time.deltaTime;
                SetSprite(_frames[FrameIndex()]);
            }

            var cam = CameraRig.Instance != null ? CameraRig.Instance.Camera : null;
            if (FaceCamera && cam != null)
            {
                // Paralel cu planul camerei (nu "privește" spre ea): așa sprite-urile de pe margini nu se deformează.
                Vector3 f = cam.transform.forward;
                f.y = 0f;
                if (f.sqrMagnitude > 0.0001f) transform.rotation = Quaternion.LookRotation(f.normalized, Vector3.up);
            }

            Color c = (UseLocalLight ? LocalLight.Sample(transform.position + Vector3.up * 0.6f) : Color.white) * Tint * Brightness;
            if (_flash > 0f)
            {
                _flash -= Time.deltaTime;
                c = Color.Lerp(c, _flashColor, 0.85f);
            }
            c.a = Tint.a;
            Material.SetColor(Materials.BaseColor, c);
        }

        void OnDestroy()
        {
            if (Material != null) Destroy(Material);
        }
    }
}
