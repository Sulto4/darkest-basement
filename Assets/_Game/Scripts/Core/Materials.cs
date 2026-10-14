using System.Collections.Generic;
using UnityEngine;

namespace DarkestBasement.Core
{
    public enum MaterialKind
    {
        Lit,              // pereți, podea: lumina torțelor cade pe ei (URP Simple Lit, opac)
        LitCutout,        // pete de sânge, pânze pe perete: luminate, cu găuri transparente
        UnlitCutout,      // sprite-uri billboard (eroi, recuzită): culoarea o calculăm noi (LocalLight)
        UnlitTransparent, // fade-uri, umbre moi
        Additive,         // strălucire (halo-ul torțelor, praful din aer); folosește culoarea vârfurilor
    }

    // Fabrica de materiale. Unity scoate din build variantele de shader pe care nu le folosește niciun
    // material salvat, așa că ProjectSetup salvează câte un "șablon" pentru fiecare tip în Resources/Materials.
    // La rulare copiem șablonul; dacă lipsește (proiect nou), îl construim din cod cu aceleași setări.
    public static class Materials
    {
        public static readonly int BaseMap = Shader.PropertyToID("_BaseMap");
        public static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

        static readonly Dictionary<MaterialKind, Material> Templates = new Dictionary<MaterialKind, Material>();

        public static string TemplatePath(MaterialKind kind) => "Materials/T_" + kind;

        public static string ShaderName(MaterialKind kind)
        {
            switch (kind)
            {
                case MaterialKind.Lit:
                case MaterialKind.LitCutout: return "Universal Render Pipeline/Simple Lit";
                case MaterialKind.Additive: return "Universal Render Pipeline/Particles/Unlit";
                default: return "Universal Render Pipeline/Unlit";
            }
        }

        public static Material Create(MaterialKind kind)
        {
            if (!Templates.TryGetValue(kind, out var template) || template == null)
            {
                template = Resources.Load<Material>(TemplatePath(kind));
                if (template == null)
                {
                    Debug.LogWarning("[Materials] lipsește șablonul " + TemplatePath(kind) + "; îl construiesc din cod (rulează tools/forge.sh setup).");
                    template = BuildTemplate(kind);
                }
                Templates[kind] = template;
            }
            return new Material(template);
        }

        public static Material Create(MaterialKind kind, Texture texture, Color? color = null)
        {
            var m = Create(kind);
            if (texture != null) m.SetTexture(BaseMap, texture);
            m.SetColor(BaseColor, color ?? Color.white);
            return m;
        }

        // Folosit și de editor (ProjectSetup) ca să salveze șabloanele: aceleași setări în ambele locuri.
        public static Material BuildTemplate(MaterialKind kind)
        {
            var shader = Shader.Find(ShaderName(kind));
            if (shader == null)
            {
                Debug.LogError("[Materials] shader-ul " + ShaderName(kind) + " nu e în build.");
                shader = Shader.Find("Sprites/Default");
            }
            var m = new Material(shader) { name = "T_" + kind };
            Configure(m, kind);
            return m;
        }

        public static void Configure(Material m, MaterialKind kind)
        {
            // Fără reflexii lucioase: pixel art-ul e mat.
            m.SetFloat("_Smoothness", 0f);
            m.SetFloat("_ReceiveShadows", 0f);
            m.EnableKeyword("_RECEIVE_SHADOWS_OFF");
            m.SetColor(BaseColor, Color.white);

            switch (kind)
            {
                case MaterialKind.Lit:
                    m.SetFloat("_Surface", 0f);
                    m.SetFloat("_AlphaClip", 0f);
                    m.SetFloat("_Cull", 2f); // Back: un perete se vede doar dinspre interior (secretul vederii "în secțiune")
                    m.SetFloat("_ZWrite", 1f);
                    m.SetOverrideTag("RenderType", "Opaque");
                    m.renderQueue = 2000;
                    break;

                case MaterialKind.LitCutout:
                case MaterialKind.UnlitCutout:
                    m.SetFloat("_Surface", 0f);
                    m.SetFloat("_AlphaClip", 1f);
                    m.SetFloat("_Cutoff", 0.5f);
                    m.EnableKeyword("_ALPHATEST_ON");
                    m.SetFloat("_Cull", 0f); // ambele fețe: billboard-urile se pot oglindi cu scale.x negativ
                    m.SetFloat("_ZWrite", 1f);
                    m.SetOverrideTag("RenderType", "TransparentCutout");
                    m.renderQueue = 2450;
                    break;

                case MaterialKind.UnlitTransparent:
                    SetTransparent(m, 5f, 10f); // SrcAlpha, OneMinusSrcAlpha
                    m.SetFloat("_Blend", 0f);
                    break;

                case MaterialKind.Additive:
                    SetTransparent(m, 5f, 1f); // SrcAlpha, One: lumina se adună
                    m.SetFloat("_Blend", 2f);
                    break;
            }
        }

        static void SetTransparent(Material m, float src, float dst)
        {
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_AlphaClip", 0f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.SetFloat("_SrcBlend", src);
            m.SetFloat("_DstBlend", dst);
            m.SetFloat("_SrcBlendAlpha", 1f);
            m.SetFloat("_DstBlendAlpha", 10f);
            m.SetFloat("_ZWrite", 0f);
            m.SetFloat("_Cull", 0f);
            m.SetOverrideTag("RenderType", "Transparent");
            m.renderQueue = 3000;
        }

        public static void SetCull(Material m, bool backfaceOnly) => m.SetFloat("_Cull", backfaceOnly ? 2f : 0f);
    }

    // Materialele construite de un obiect (hol, cameră) le ține aici și le distruge la final,
    // altfel fiecare hol nou ar lăsa materiale orfane în memorie.
    public sealed class MaterialSet
    {
        readonly Dictionary<string, Material> _byKey = new Dictionary<string, Material>();
        readonly List<Material> _all = new List<Material>();

        public Material Get(MaterialKind kind, Texture texture, Color? color = null)
        {
            string key = kind + "|" + (texture != null ? texture.GetInstanceID().ToString() : "none") + "|" + (color ?? Color.white);
            if (_byKey.TryGetValue(key, out var m)) return m;
            m = Materials.Create(kind, texture, color);
            _byKey[key] = m;
            _all.Add(m);
            return m;
        }

        // Un material doar al tău (de ex. o ușă care își schimbă textura).
        public Material Unique(MaterialKind kind, Texture texture, Color? color = null)
        {
            var m = Materials.Create(kind, texture, color);
            _all.Add(m);
            return m;
        }

        public void DestroyAll()
        {
            foreach (var m in _all) if (m != null) Object.Destroy(m);
            _all.Clear();
            _byKey.Clear();
        }
    }
}
