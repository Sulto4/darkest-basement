using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace DarkestBasement.EditorTools
{
    // Toate imaginile din Resources sunt pixel art: fără filtrare, fără compresie, fără mipmaps (excepție: texturile 3D, mai jos).
    // Așa fiecare pixel desenat rămâne un pătrat curat pe ecran.
    public class PixelArtImportPostprocessor : AssetPostprocessor
    {
        // Schimbă numărul când se schimbă regulile de mai jos: Unity reimportă atunci toate imaginile.
        public override uint GetVersion() => 2;

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
            // Au mipmap-uri (tot cu filtru Point): fără ele, podeaua din depărtare "fâșâie" ca zăpada TV când camera se mișcă.
            if (assetPath.StartsWith(Root + "Textures/"))
            {
                ti.textureType = TextureImporterType.Default;
                ti.wrapMode = TextureWrapMode.Repeat;
                ti.npotScale = TextureImporterNPOTScale.None;
                ti.mipmapEnabled = true;
                ti.mipmapFilter = TextureImporterMipFilter.BoxFilter;
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

        // ui_slices.json: { "version": 1, "slices": [ { "sprite": "panel_paper", "left": 12, "right": 12, "top": 12, "bottom": 12, "drawMode": "tiled" }, ... ] }
        [Serializable] sealed class SliceEntry { public string sprite; public int left, right, top, bottom; public string drawMode; }
        [Serializable] sealed class SliceFile { public List<SliceEntry> slices = new List<SliceEntry>(); }

        static Dictionary<string, Vector4> _slices;

        static bool TryGetSliceBorder(string name, out Vector4 border)
        {
            border = Vector4.zero;
            if (_slices == null)
            {
                _slices = new Dictionary<string, Vector4>();
                if (File.Exists(SlicesPath))
                {
                    var file = JsonUtility.FromJson<SliceFile>(File.ReadAllText(SlicesPath));
                    if (file != null && file.slices != null)
                        foreach (var e in file.slices)
                            // Ordinea Unity pentru margini: stânga, jos, dreapta, sus.
                            if (!string.IsNullOrEmpty(e.sprite)) _slices[e.sprite] = new Vector4(e.left, e.bottom, e.right, e.top);
                }
            }
            return _slices.TryGetValue(name, out border);
        }
    }
}
