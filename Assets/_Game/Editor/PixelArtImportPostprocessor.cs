using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace DarkestBasement.EditorTools
{
    // Toate imaginile din Resources sunt pixel art: fără filtrare, fără compresie, fără mipmaps.
    // Așa fiecare pixel desenat rămâne un pătrat curat pe ecran.
    public class PixelArtImportPostprocessor : AssetPostprocessor
    {
        const string Root = "Assets/_Game/Resources/";
        const string SlicesPath = "Assets/_Game/Resources/Sprites/UI/ui_slices.json";
        const int PixelsPerUnit = 32;

        // Personajele și recuzita stau pe podea, deci pivotul e la picioare.
        static readonly string[] BottomPivotFolders = { "/Heroes/", "/Enemies/", "/Bosses/", "/Familiars/", "/Props/" };

        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(Root)) return;
            var ti = (TextureImporter)assetImporter;
            ti.filterMode = FilterMode.Point;
            ti.mipmapEnabled = false;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.alphaIsTransparency = true;
            ti.anisoLevel = 0;

            // Texturile 3D (pereți, podea) se repetă pe suprafețe mari.
            if (assetPath.StartsWith(Root + "Textures/"))
            {
                ti.textureType = TextureImporterType.Default;
                ti.wrapMode = TextureWrapMode.Repeat;
                ti.npotScale = TextureImporterNPOTScale.None;
                return;
            }

            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Single;
            ti.spritePixelsPerUnit = PixelsPerUnit;
            ti.wrapMode = TextureWrapMode.Clamp;

            var settings = new TextureImporterSettings();
            ti.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            settings.spriteExtrude = 0;
            settings.spriteAlignment = IsBottomPivot(assetPath) ? (int)SpriteAlignment.BottomCenter : (int)SpriteAlignment.Center;
            ti.SetTextureSettings(settings);

            // Panourile UI se întind fără să-și deformeze colțurile (9-slice).
            if (TryGetSliceBorder(Path.GetFileNameWithoutExtension(assetPath), out var border))
                ti.spriteBorder = border;
        }

        void OnPreprocessAudio()
        {
            if (!assetPath.StartsWith(Root)) return;
            var ai = (AudioImporter)assetImporter;
            var s = ai.defaultSampleSettings;
            // Muzica se citește din fișier pe măsură ce cântă; efectele scurte stau în memorie, gata instant.
            if (assetPath.Contains("/Music/"))
            {
                s.loadType = AudioClipLoadType.Streaming;
                s.compressionFormat = AudioCompressionFormat.Vorbis;
                s.quality = 0.6f;
            }
            else
            {
                s.loadType = AudioClipLoadType.DecompressOnLoad;
                s.compressionFormat = AudioCompressionFormat.Vorbis;
                s.quality = 0.8f;
            }
            ai.defaultSampleSettings = s;
        }

        static bool IsBottomPivot(string path)
        {
            foreach (var f in BottomPivotFolders)
                if (path.Contains(f)) return true;
            return false;
        }

        static Dictionary<string, Vector4> _slices;

        // ui_slices.json: { "panel_paper": { "left": 6, "right": 6, "top": 6, "bottom": 6 }, ... }
        static bool TryGetSliceBorder(string name, out Vector4 border)
        {
            border = Vector4.zero;
            if (_slices == null)
            {
                _slices = new Dictionary<string, Vector4>();
                if (File.Exists(SlicesPath))
                {
                    var json = File.ReadAllText(SlicesPath);
                    var entry = new Regex("\"([^\"]+)\"\\s*:\\s*\\{([^}]*)\\}");
                    foreach (Match m in entry.Matches(json))
                    {
                        var body = m.Groups[2].Value;
                        _slices[m.Groups[1].Value] = new Vector4(Num(body, "left"), Num(body, "bottom"), Num(body, "right"), Num(body, "top"));
                    }
                }
            }
            return _slices.TryGetValue(name, out border);
        }

        static float Num(string body, string key)
        {
            var m = Regex.Match(body, "\"" + key + "\"\\s*:\\s*(\\d+)");
            return m.Success ? float.Parse(m.Groups[1].Value) : 0f;
        }
    }
}
