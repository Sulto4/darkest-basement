using DarkestBasement.Core;
using UnityEngine;

namespace DarkestBasement.Exploration
{
    // Praful care plutește în aer. Un singur mesh cu N pătrățele (un singur draw call), rescris în fiecare frame.
    // Fiecare fir are mereu ~1 pixel pe ecran, oricât de aproape sau departe ar fi: mărimea lui în lume crește cu distanța
    // exact cât scade perspectiva. Luminează doar lângă o lumină: în întuneric e invizibil, lângă torță sclipește discret.
    public sealed class DustMotes : MonoBehaviour
    {
        const int Count = 56;
        const float NearDistance = 2.5f;              // niciun fir mai aproape de cameră (ar deveni o pată mare)
        static readonly Vector3 Box = new Vector3(10f, 3.6f, 11.5f);
        const float PixelsOnScreen = 1.4f;

        readonly Vector3[] _pos = new Vector3[Count];
        readonly float[] _seed = new float[Count];
        readonly Vector3[] _verts = new Vector3[Count * 4];
        readonly Color[] _colors = new Color[Count * 4];
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
            Vector3 center = BoxCenter();
            for (int i = 0; i < Count; i++)
            {
                _pos[i] = center + new Vector3(
                    ((float)rng.NextDouble() - 0.5f) * Box.x, 0f, ((float)rng.NextDouble() - 0.5f) * Box.z);
                _pos[i].y = 0.25f + (float)rng.NextDouble() * Box.y;
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

            // Un pătrățel plin (nu sprite-ul în formă de cruce): la 1 pixel, orice formă devine oricum un punct.
            _material = Materials.Create(MaterialKind.Additive, ProceduralTextures.White);
            gameObject.AddComponent<MeshFilter>().sharedMesh = _mesh;
            var r = gameObject.AddComponent<MeshRenderer>();
            r.sharedMaterial = _material;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            transform.position = Vector3.zero;
            transform.rotation = Quaternion.identity;
        }

        // Cutia de praf stă în fața camerei, între NearDistance și NearDistance + Box.z.
        static Vector3 BoxCenter()
        {
            var cam = CameraRig.Instance.Camera.transform;
            Vector3 forward = cam.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.0001f) forward = Vector3.forward;
            return cam.position + forward.normalized * (NearDistance + Box.z * 0.5f);
        }

        void LateUpdate()
        {
            var cam = CameraRig.Instance.Camera;
            var camT = cam.transform;
            Vector3 right = camT.right, up = camT.up, camPos = camT.position;
            Vector3 center = BoxCenter();
            float t = Time.time, dt = Time.deltaTime;
            // Cât de mare e un pixel al imaginii de 640×360 la distanța 1 de cameră.
            float pixelAtOne = 2f * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) / cam.pixelHeight;

            for (int i = 0; i < Count; i++)
            {
                float s = _seed[i];
                // Plutire foarte lentă: zgomot Perlin pe fiecare axă și o cădere abia simțită.
                Vector3 drift = new Vector3(
                    Mathf.PerlinNoise(s, t * 0.1f) - 0.5f,
                    (Mathf.PerlinNoise(s + 5f, t * 0.08f) - 0.56f) * 0.5f,
                    Mathf.PerlinNoise(s + 9f, t * 0.1f) - 0.5f) * 0.16f;
                Vector3 p = _pos[i] + drift * dt;

                // Firul care iese din cutie reapare pe partea cealaltă (cutia se mută odată cu camera).
                Vector3 d = p - center;
                p.x = center.x + Wrap(d.x, Box.x);
                p.z = center.z + Wrap(d.z, Box.z);
                if (p.y < 0.2f) p.y += Box.y;
                if (p.y > 0.2f + Box.y) p.y -= Box.y;
                _pos[i] = p;

                // Lumina locală decide cât se vede; sclipirea vine din zgomot.
                Color light = LocalLight.Sample(p) - LocalLight.Ambient;
                float lum = Mathf.Clamp01((light.r + light.g + light.b) / 3f * 1.2f);
                float twinkle = 0.45f + 0.55f * Mathf.PerlinNoise(s + 13f, t * 1.2f);
                float a = Mathf.Clamp01(lum * twinkle) * 0.75f;
                Color c = Tint * a;
                c.a = a;

                float distance = Mathf.Max(NearDistance, Vector3.Dot(p - camPos, camT.forward));
                float half = distance * pixelAtOne * PixelsOnScreen * 0.5f;
                Vector3 r = right * half, u = up * half;
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
            while (v > half) v -= size;
            while (v < -half) v += size;
            return v;
        }

        void OnDestroy()
        {
            if (_mesh != null) Destroy(_mesh);
            if (_material != null) Destroy(_material);
        }
    }
}
