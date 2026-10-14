using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace DarkestBasement.Core
{
    // Post-procesarea lumii, construită din cod: Bloom (flăcările strălucesc), Vignette închisă (colțurile se pierd în beznă),
    // Film Grain (textura de film vechi) și Color Adjustments (nuanța sepia-roșcată din Isaac).
    // Se aplică pe imaginea de 640×360, deci și granulația are mărimea unui pixel de artă.
    public static class PostFx
    {
        // ProjectSetup salvează aici o copie: build-ul păstrează variantele de shader doar pentru efectele găsite în asset-uri.
        public const string AssetPath = "Assets/_Game/Resources/PostFx/WorldPostFx.asset";

        public const float BaseVignette = 0.42f;

        public static VolumeProfile CreateProfile()
        {
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            profile.name = "WorldPostFx";
            Configure(profile);
            return profile;
        }

        public static void Configure(VolumeProfile profile)
        {
            var bloom = GetOrAdd<Bloom>(profile);
            bloom.threshold.Override(0.85f);
            bloom.intensity.Override(0.75f);
            bloom.scatter.Override(0.62f);
            bloom.tint.Override(new Color(1f, 0.82f, 0.62f));
            bloom.highQualityFiltering.Override(false);

            var vignette = GetOrAdd<Vignette>(profile);
            vignette.color.Override(new Color(0.03f, 0.005f, 0.004f));
            vignette.intensity.Override(BaseVignette);
            vignette.smoothness.Override(0.42f);
            vignette.rounded.Override(false);

            var grain = GetOrAdd<FilmGrain>(profile);
            grain.type.Override(FilmGrainLookup.Medium2);
            grain.intensity.Override(0.3f);
            grain.response.Override(0.75f);

            // Sepia-roșcat: filtru cald, puțin desaturat, contrast mai mare ca negrul să fie adânc.
            var color = GetOrAdd<ColorAdjustments>(profile);
            color.postExposure.Override(0.35f);
            color.contrast.Override(14f);
            color.colorFilter.Override(new Color(1f, 0.87f, 0.76f));
            color.saturation.Override(-6f);
            color.hueShift.Override(0f);

            // Fără tonemapping: culorile din paleta de artă rămân exact cum au fost desenate.
            var tone = GetOrAdd<Tonemapping>(profile);
            tone.mode.Override(TonemappingMode.None);

            // Echipa C pulsează aberația cromatică la Brimstone; o lăsăm la 0 ca varianta să existe în build.
            var chroma = GetOrAdd<ChromaticAberration>(profile);
            chroma.intensity.Override(0f);
        }

        static T GetOrAdd<T>(VolumeProfile profile) where T : VolumeComponent
        {
            if (!profile.TryGet(out T component)) component = profile.Add<T>(false);
            component.active = true;
            return component;
        }
    }
}
