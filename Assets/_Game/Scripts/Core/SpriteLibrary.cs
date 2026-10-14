using System.Collections.Generic;
using UnityEngine;

namespace DarkestBasement.Core
{
    // Încarcă sprite-urile după convenția de nume din docs/ARCHITECTURE.md și le ține în cache.
    // Exemplu: SpriteLibrary.Frames("Sprites/Heroes/isaac/side_idle") → [side_idle_0, side_idle_1].
    public static class SpriteLibrary
    {
        static readonly Dictionary<string, Sprite> Sprites = new Dictionary<string, Sprite>();
        static readonly Dictionary<string, Sprite[]> FrameSets = new Dictionary<string, Sprite[]>();
        static readonly Dictionary<string, Texture2D> Textures = new Dictionary<string, Texture2D>();
        static Sprite _missing;

        public static Sprite Get(string path)
        {
            if (Sprites.TryGetValue(path, out var s)) return s;
            s = Resources.Load<Sprite>(path);
            if (s == null)
            {
                Debug.LogWarning("[SpriteLibrary] lipsește sprite-ul " + path);
                s = Missing;
            }
            Sprites[path] = s;
            return s;
        }

        public static bool Exists(string path) => Resources.Load<Sprite>(path) != null;

        // Cadrele unei animații: prefix_0, prefix_1, ... până la primul care lipsește.
        public static Sprite[] Frames(string prefix)
        {
            if (FrameSets.TryGetValue(prefix, out var set)) return set;
            var list = new List<Sprite>();
            for (int i = 0; i < 64; i++)
            {
                var s = Resources.Load<Sprite>(prefix + "_" + i);
                if (s == null) break;
                list.Add(s);
            }
            if (list.Count == 0)
            {
                Debug.LogWarning("[SpriteLibrary] nu există cadre pentru " + prefix);
                list.Add(Missing);
            }
            set = list.ToArray();
            FrameSets[prefix] = set;
            return set;
        }

        public static Texture2D Texture(string path)
        {
            if (Textures.TryGetValue(path, out var t)) return t;
            t = Resources.Load<Texture2D>(path);
            if (t == null) Debug.LogWarning("[SpriteLibrary] lipsește textura " + path);
            Textures[path] = t;
            return t;
        }

        // Un pătrat magenta bătător la ochi, ca să vezi imediat ce asset lipsește.
        static Sprite Missing
        {
            get
            {
                if (_missing != null) return _missing;
                var tex = new Texture2D(16, 16) { filterMode = FilterMode.Point };
                var pixels = new UnityEngine.Color[256];
                for (int i = 0; i < pixels.Length; i++) pixels[i] = new UnityEngine.Color(1f, 0f, 1f);
                tex.SetPixels(pixels);
                tex.Apply();
                _missing = Sprite.Create(tex, new Rect(0, 0, 16, 16), new Vector2(0.5f, 0f), 32f);
                return _missing;
            }
        }
    }
}
