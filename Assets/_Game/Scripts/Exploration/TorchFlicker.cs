using DarkestBasement.Core;
using UnityEngine;

namespace DarkestBasement.Exploration
{
    // O torță: sprite-ul animat (torch_0..3), un halo care strălucește și o lumină punctiformă care pâlpâie.
    // Pâlpâirea vine din zgomot Perlin: o valoare care se schimbă lin și imprevizibil în timp,
    // spre deosebire de Random, care ar sări haotic de la un frame la altul (ar arăta ca un bec stricat).
    public sealed class TorchFlicker : MonoBehaviour
    {
        public Light Light { get; private set; }
        Billboard _sprite, _halo;
        float _baseIntensity, _seed;
        Vector3 _lightHome;

        public static TorchFlicker Create(Transform parent, Vector3 localPos, Color color, float intensity, float range, float seed)
        {
            var go = new GameObject("Torch");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            var torch = go.AddComponent<TorchFlicker>();
            torch.Build(color, intensity, range, seed);
            return torch;
        }

        void Build(Color color, float intensity, float range, float seed)
        {
            _seed = seed;
            _baseIntensity = intensity;

            // Torța e "lipită" de lume: nu primește lumină locală, e chiar sursa ei.
            _sprite = Billboard.Create(transform, "Sprite", SpriteLibrary.Get("Sprites/Props/torch_0"), Vector3.zero);
            _sprite.UseLocalLight = false;
            _sprite.Brightness = 1.25f; // flacăra trece de pragul bloom-ului, mânerul rămâne întunecat
            _sprite.Play(SpriteLibrary.Frames("Sprites/Props/torch"), 9f, true, seed % 1f);

            _halo = Billboard.Create(transform, "Halo", null, new Vector3(0f, 0.72f, 0f), MaterialKind.Additive);
            _halo.Material.SetTexture(Materials.BaseMap, ProceduralTextures.Halo);
            _halo.Quad.localScale = new Vector3(2.2f, 2.2f, 1f);
            _halo.Quad.localPosition = new Vector3(0f, -1.1f, 0f);
            _halo.UseLocalLight = false;
            _halo.Tint = new Color(color.r, color.g * 0.85f, color.b * 0.7f, 0.22f);

            var lightGo = new GameObject("Light");
            lightGo.transform.SetParent(transform, false);
            _lightHome = new Vector3(0f, 0.85f, 0f);
            lightGo.transform.localPosition = _lightHome;
            Light = lightGo.AddComponent<Light>();
            Light.type = LightType.Point;
            Light.color = color;
            Light.range = range;
            Light.intensity = intensity;
            Light.shadows = LightShadows.None;
            Light.renderMode = LightRenderMode.ForcePixel;
            LocalLight.Register(Light);
        }

        void Update()
        {
            float t = Time.time;
            // Două frecvențe suprapuse: o respirație lentă + tremurul rapid al flăcării.
            float slow = Mathf.PerlinNoise(_seed, t * 1.3f);
            float fast = Mathf.PerlinNoise(_seed + 17.3f, t * 9f);
            float k = 0.72f + 0.38f * slow + 0.14f * (fast - 0.5f);
            Light.intensity = _baseIntensity * k;
            // Lumina se mișcă puțin: umbrele și petele de lumină de pe pereți "dansează".
            Light.transform.localPosition = _lightHome + new Vector3(
                (Mathf.PerlinNoise(_seed + 3f, t * 2.1f) - 0.5f) * 0.12f,
                (Mathf.PerlinNoise(_seed + 7f, t * 2.4f) - 0.5f) * 0.10f, 0f);
            var halo = _halo.Tint;
            halo.a = 0.16f + 0.12f * k;
            _halo.Tint = halo;
            _halo.Brightness = 0.9f + 0.4f * k;
        }

        void OnDestroy() => LocalLight.Unregister(Light);
    }
}
