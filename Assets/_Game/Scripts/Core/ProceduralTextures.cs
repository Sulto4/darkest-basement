using UnityEngine;

namespace DarkestBasement.Core
{
    // Texturi mici generate din cod (strălucire, umbră), desenate tot "în trepte", ca pixel art-ul.
    // Un gradient neted ar arăta ca o pată moale de Photoshop lângă sprite-urile cu pixeli clari.
    public static class ProceduralTextures
    {
        static Texture2D _halo, _shadow, _shaft, _white, _fade;

        // Strălucirea din jurul flăcărilor: scade lin spre margine, cu dithering (pixeli împrăștiați în tipar Bayer)
        // în loc de inele: de aproape se vede ca o pâclă pixelată, nu ca un disc portocaliu cu margine tăiată.
        public static Texture2D Halo => _halo != null ? _halo : (_halo = DitheredRadial(32, 2.6f, "Halo"));

        // Umbra ovală de sub personaje (o pată închisă pe podea).
        public static Texture2D Shadow => _shadow != null ? _shadow : (_shadow = Radial(16, 3, 0.6f, "Shadow"));

        // Rază de lumină verticală (piedestalul de comoară), stinsă spre vârf.
        public static Texture2D Shaft
        {
            get
            {
                if (_shaft != null) return _shaft;
                const int w = 8, h = 32;
                _shaft = NewTexture(w, h, "Shaft");
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                    {
                        float edge = 1f - Mathf.Abs((x + 0.5f) / w * 2f - 1f);
                        float fade = 1f - (float)y / h;
                        float a = Quantize(edge * fade, 4);
                        _shaft.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                    }
                _shaft.Apply();
                return _shaft;
            }
        }

        // Umbra din colțuri (ambient occlusion "pictat"): opacă jos, stinsă în sus, în 6 trepte.
        // Pusă la baza pereților, face podeaua și peretele să pară că se întâlnesc într-un colț adânc.
        public static Texture2D Fade
        {
            get
            {
                if (_fade != null) return _fade;
                const int w = 4, h = 16;
                _fade = NewTexture(w, h, "Fade");
                for (int y = 0; y < h; y++)
                {
                    float a = Quantize(Mathf.Pow(1f - (y + 0.5f) / h, 1.6f), 6);
                    for (int x = 0; x < w; x++) _fade.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
                _fade.Apply();
                return _fade;
            }
        }

        public static Texture2D White
        {
            get
            {
                if (_white != null) return _white;
                _white = NewTexture(4, 4, "White");
                var px = new Color[16];
                for (int i = 0; i < px.Length; i++) px[i] = Color.white;
                _white.SetPixels(px);
                _white.Apply();
                return _white;
            }
        }

        // Alfa scade de la centru spre margine, rotunjit la "steps" niveluri (efect de bandă, ca în jocurile pixel).
        static Texture2D Radial(int size, int steps, float power, string name)
        {
            var tex = NewTexture(size, size, name);
            float c = (size - 1) / 2f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = (x - c) / (c + 0.5f), dy = (y - c) / (c + 0.5f);
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Pow(Mathf.Clamp01(1f - r), power);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, Quantize(a, steps)));
                }
            tex.Apply();
            return tex;
        }

        // Matricea Bayer 4×4: pragul de "aprindere" al fiecărui pixel. Vecinii au praguri foarte diferite,
        // așa că o valoare de 0.3 aprinde ~30% din pixeli, răspândiți uniform (dithering ordonat).
        static readonly float[] Bayer = { 0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5 };

        static Texture2D DitheredRadial(int size, float power, string name)
        {
            const int levels = 4;
            var tex = NewTexture(size, size, name);
            float c = (size - 1) / 2f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = (x - c) / (c + 0.5f), dy = (y - c) / (c + 0.5f);
                    float a = Mathf.Pow(Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy)), power) * levels;
                    float threshold = (Bayer[(y % 4) * 4 + x % 4] + 0.5f) / 16f;
                    float level = Mathf.Floor(a) + (a - Mathf.Floor(a) > threshold ? 1f : 0f);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(level / levels)));
                }
            tex.Apply();
            return tex;
        }

        static float Quantize(float v, int steps) => Mathf.Ceil(Mathf.Clamp01(v) * steps - 0.0001f) / steps;

        static Texture2D NewTexture(int w, int h, string name)
        {
            return new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                name = name,
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
            };
        }
    }
}
