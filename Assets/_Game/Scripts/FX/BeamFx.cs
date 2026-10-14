using System.Collections;
using DarkestBasement.Combat;
using UnityEngine;

namespace DarkestBasement.FX
{
    // Aspectul unei raze: culorile (exterior → miez) și cât de tare „arde” miezul.
    public struct BeamStyle
    {
        public string Key;
        public Color32 Edge, Mid, Core;
        public Color Glow;      // culoarea HDR a miezului (>1 = trece pragul de bloom)
        public float Width;     // 1 = 16 px de artă grosime
        public float Duration;  // cât stă aprinsă, în secunde
        public ParticleKind Sparks;

        public static BeamStyle Brimstone => new BeamStyle
        {
            Key = "brim", Edge = FxColors.BloodDeep, Mid = FxColors.Blood, Core = new Color32(255, 200, 190, 255),
            Glow = new Color(5f, 1.1f, 0.9f, 1f), Width = 1.15f, Duration = 0.55f, Sparks = ParticleKind.Blood,
        };
        public static BeamStyle BrimstoneHuge => new BeamStyle
        {
            Key = "brim", Edge = FxColors.BloodDeep, Mid = FxColors.Blood, Core = new Color32(255, 200, 190, 255),
            Glow = new Color(7f, 1.4f, 1.1f, 1f), Width = 1.8f, Duration = 0.7f, Sparks = ParticleKind.Blood,
        };
        public static BeamStyle Lil => new BeamStyle
        {
            Key = "brim", Edge = FxColors.BloodDeep, Mid = FxColors.Blood, Core = new Color32(255, 200, 190, 255),
            Glow = new Color(3.5f, 0.8f, 0.7f, 1f), Width = 0.6f, Duration = 0.4f, Sparks = ParticleKind.Blood,
        };
        public static BeamStyle Void => new BeamStyle
        {
            Key = "void", Edge = new Color32(20, 6, 10, 255), Mid = FxColors.BloodDeep, Core = FxColors.Blood,
            Glow = new Color(4f, 0.3f, 0.4f, 1f), Width = 2.1f, Duration = 0.75f, Sparks = ParticleKind.Smoke,
        };
        public static BeamStyle Bile => new BeamStyle
        {
            Key = "bile", Edge = FxColors.BileDark, Mid = FxColors.Bile, Core = new Color32(250, 248, 200, 255),
            Glow = new Color(3f, 3f, 1.2f, 1f), Width = 1.2f, Duration = 0.5f, Sparks = ParticleKind.Bile,
        };
    }

    // Raza Brimstone: o dungă de 1 px lățime întinsă pe lungime, cu un miez în culoare HDR.
    // Culorile peste 1.0 nu există pe ecran, dar trec de pragul efectului Bloom, care le „împrăștie” în jur:
    // de aici senzația că raza arde.
    public static class BeamFx
    {
        static Material _hdrMaterial;

        // Încărcarea dinainte de foc: o bilă care pulsează la gura atacatorului. Blochează cât durează.
        public static IEnumerator Charge(Vector3 at, float duration, BeamStyle style)
        {
            var go = FxHost.Spawn("BeamCharge");
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = PixelSprites.Disc(11);
            sr.material = HdrMaterial();
            sr.sortingOrder = 46;
            var block = new MaterialPropertyBlock();
            float t = 0f;
            while (t < duration && go != null)
            {
                t += Time.deltaTime;
                if (sr == null) yield break;
                float k = Mathf.Clamp01(t / duration);
                // Crește și „tremură” tot mai repede: anticiparea face lovitura să pară mai grea.
                float pulse = 1f + Mathf.Sin(t * (30f + 50f * k)) * 0.18f * k;
                go.transform.position = at;
                go.transform.rotation = CombatCamera.Facing;
                go.transform.localScale = Vector3.one * Mathf.Lerp(0.2f, 1.1f, k) * pulse;
                block.SetColor("_Color", Color.Lerp((Color)style.Mid, style.Glow, k));
                sr.SetPropertyBlock(block);
                if (Random.value < 0.5f) Particles.Burst(at + Random.insideUnitSphere * 0.5f, style.Sparks, 1, Vector3.zero, 0.3f, 1f);
                yield return null;
            }
            if (go != null) Object.Destroy(go);
        }

        // Pornește raza și întoarce imediat; raza se animă și se distruge singură.
        public static void Fire(Vector3 from, Vector3 to, BeamStyle style)
        {
            FxHost.Run(FireRoutine(from, to, style));
        }

        static IEnumerator FireRoutine(Vector3 from, Vector3 to, BeamStyle style)
        {
            var root = FxHost.Spawn("Beam");
            var glow = NewStrip(root.transform, "glow", PixelSprites.BeamStrip(style.Key, style.Mid, style.Mid, style.Edge), 47, null);
            var core = NewStrip(root.transform, "core", PixelSprites.BeamStrip(style.Key + "_core", style.Core, style.Core, FxColors.A(style.Core, 0)), 48, HdrMaterial());
            var block = new MaterialPropertyBlock();
            block.SetColor("_Color", style.Glow);
            core.SetPropertyBlock(block);

            float length = Vector3.Distance(from, to);
            float t = 0f, life = style.Duration;
            while (t < life && root != null)
            {
                t += Time.deltaTime;
                float k = t / life;
                // Anvelopă: aprindere bruscă (cu depășire), tremur susținut, stingere pe ultimele 30%.
                float attack = Mathf.Clamp01(t / 0.06f);
                float width = style.Width * (attack < 1f ? Mathf.Lerp(0f, 1.3f, attack) : 1f + Mathf.Sin(t * 70f) * 0.1f);
                if (k > 0.7f) width *= 1f - (k - 0.7f) / 0.3f;
                float reach = Mathf.Clamp01(t / 0.05f);

                Quaternion face = CombatCamera.Facing;
                Vector3 d = Quaternion.Inverse(face) * (to - from);
                float angle = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
                root.transform.position = from;
                root.transform.rotation = face * Quaternion.Euler(0f, 0f, angle);
                // Dunga are 1 px lățime la 32 px/unitate: scale.x = lungimea în pixeli de artă.
                glow.transform.localScale = new Vector3(length * reach * PixelSprites.Ppu, width, 1f);
                core.transform.localScale = new Vector3(length * reach * PixelSprites.Ppu, width * 0.55f, 1f);

                if (Random.value < 0.6f)
                {
                    Vector3 along = Vector3.Lerp(from, to, Random.value * reach);
                    Particles.Burst(along, style.Sparks, 1, Vector3.up, 0.5f, 1f);
                }
                yield return null;
            }
            if (root != null) Object.Destroy(root);
        }

        static SpriteRenderer NewStrip(Transform parent, string name, Sprite sprite, int order, Material material)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = order;
            if (material != null) sr.material = material;
            return sr;
        }

        // Shader-ul Sprites/Default înmulțește textura cu _Color (float, deci acceptă valori > 1).
        // E inclus mereu în build (GraphicsSettings → Always Included Shaders).
        public static Material HdrMaterial()
        {
            if (_hdrMaterial != null) return _hdrMaterial;
            var shader = Shader.Find("Sprites/Default");
            if (shader == null)
            {
                var probe = new GameObject("probe").AddComponent<SpriteRenderer>();
                _hdrMaterial = new Material(probe.sharedMaterial);
                Object.Destroy(probe.gameObject);
            }
            else _hdrMaterial = new Material(shader);
            _hdrMaterial.name = "FX HDR";
            return _hdrMaterial;
        }
    }
}
