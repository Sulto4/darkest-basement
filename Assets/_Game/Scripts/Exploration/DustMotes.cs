using DarkestBasement.Core;
using UnityEngine;

namespace DarkestBasement.Exploration
{
    // Praful care plutește în aer. Un singur mesh cu N pătrățele mici (un singur draw call),
    // rescris în fiecare frame. Fiecare fir de praf luminează doar când e aproape de o lumină:
    // în întuneric e invizibil, lângă torță sclipește. Așa se "vede" aerul din jurul flăcărilor.
    public sealed class DustMotes : MonoBehaviour
    {
        const int Count = 110;
        static readonly Vector3 Box = new Vector3(9f, 4f, 16f); // zona din fața camerei în care trăiește praful

        Vector3[] _pos = new Vector3[Count];
        float[] _seed = new float[Count];
        Vector3[] _verts = new Vector3[Count * 4];
        Color[] _colors = new Color[Count * 4];
        Mesh _mesh;
        Material _material;
        public Color Tint = new Color(1f, 0.85f, 0.65f);

        public static DustMotes Create(Transform parent, Color tint, int seed)
        {
            var go = new GameObject("DustMotes");
            go.transform.SetParent(parent, false);
            var d = go.AddComponent<DustMotes>();
            d.Tint = tint;
            d.Init(seed);
            return d;
        }

        void Init(int seed)
        {
            var rng = new System.Random(seed);
            var cam = CameraRig.Instance.Camera.transform;
            for (int i = 0; i < Count; i++)
            {
                _pos[i] = cam.position + new Vector3(
                    ((float)rng.NextDouble() - 0.5f) * Box.x,
                    (float)rng.NextDouble() * Box.y - 1.5f,
                    (float)rng.NextDouble() * Box.z);
                _seed[i] = (float)rng.NextDouble() * 100f;
            }

            _mesh = new Mesh { name = "DustMotes" };
            _mesh.MarkDynamic();
            var uv = new Vector2[Count * 4];
            var tris = new int[Count * 6];
            for (int i = 0; i < Count; i++)
            {
                uv[i * 4] = new Vector2(0, 0); uv[i * 4 + 1] = new Vector2(0, 1); uv[i * 4 + 2] = new Vector2(1, 1); uv[i * 4 + 3] = new Vector2(1, 0);
                tris[i * 6] = i * 4; tris[i * 6 + 1] = i * 4 + 1; tris[i * 6 + 2] = i * 4 + 2;
                tris[i * 6 + 3] = i * 4; tris[i * 6 + 4] = i * 4 + 2; tris[i * 6 + 5] = i * 4 + 3;
            }
            _mesh.vertices = _verts;
            _mesh.uv = uv;
            _mesh.colors = _colors;
            _mesh.triangles = tris;
            _mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 1000f);

            _material = Materials.Create(MaterialKind.Additive, SpriteLibrary.Get("Sprites/Props/dust_mote").texture);
            gameObject.AddComponent<MeshFilter>().sharedMesh = _mesh;
            var r = gameObject.AddComponent<MeshRenderer>();
            r.sharedMaterial = _material;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            transform.position = Vector3.zero;
            transform.rotation = Quaternion.identity;
        }

        void LateUpdate()
        {
            var camT = CameraRig.Instance.Camera.transform;
            Vector3 right = camT.right, up = camT.up;
            Vector3 center = camT.position + camT.forward * (Box.z * 0.5f);
            float t = Time.time, dt = Time.deltaTime;
            const float size = 3f / 32f; // 3 pixeli de artă

            for (int i = 0; i < Count; i++)
            {
                float s = _seed[i];
                // Plutire lentă: zgomot Perlin pe fiecare axă + o cădere foarte mică.
                Vector3 drift = new Vector3(
                    Mathf.PerlinNoise(s, t * 0.15f) - 0.5f,
                    (Mathf.PerlinNoise(s + 5f, t * 0.12f) - 0.55f) * 0.6f,
                    Mathf.PerlinNoise(s + 9f, t * 0.15f) - 0.5f) * 0.35f;
                Vector3 p = _pos[i] + drift * dt;

                // Firul care iese din cutie reapare pe partea cealaltă (cutia se mută odată cu camera).
                Vector3 d = p - center;
                p.x = center.x + Wrap(d.x, Box.x);
                p.y = Mathf.Clamp(p.y, 0.1f, 3.9f);
                p.z = center.z + Wrap(d.z, Box.z);
                _pos[i] = p;

                // Lumina locală decide cât se vede; sclipirea vine din zgomot rapid.
                Color light = LocalLight.Sample(p) - LocalLight.Ambient;
                float lum = Mathf.Clamp01((light.r + light.g + light.b) / 3f * 1.6f);
                float twinkle = 0.55f + 0.45f * Mathf.PerlinNoise(s + 13f, t * 1.7f);
                Color c = Tint * (lum * twinkle);
                c.a = Mathf.Clamp01(lum * twinkle * 1.4f);

                Vector3 r = right * (size * 0.5f), u = up * (size * 0.5f);
                int v = i * 4;
                _verts[v] = p - r - u; _verts[v + 1] = p - r + u; _verts[v + 2] = p + r + u; _verts[v + 3] = p + r - u;
                _colors[v] = _colors[v + 1] = _colors[v + 2] = _colors[v + 3] = c;
            }
            _mesh.vertices = _verts;
            _mesh.colors = _colors;
        }

        static float Wrap(float v, float size)
        {
            float half = size * 0.5f;
            if (v > half) return v - size;
            if (v < -half) return v + size;
            return v;
        }

        void OnDestroy()
        {
            if (_mesh != null) Destroy(_mesh);
            if (_material != null) Destroy(_material);
        }
    }
}
