using System.Collections.Generic;
using DarkestBasement.Core;
using DarkestBasement.FX;
using UnityEngine;
using UnityEngine.Rendering;

namespace DarkestBasement.Combat
{
    // Toate cadrele unei unități, încărcate după convenția de nume din docs/ARCHITECTURE.md.
    // Eroii: Heroes/<id>/side_*. Inamicii: Enemies/<id>/*. Boss-ii: Bosses/<id>/* (Mom are foot_*, hand_*, eye_*).
    public sealed class UnitFrames
    {
        public Sprite[] Idle, Attack, Hurt, Dead;
        public Sprite Portrait;
        // Piciorul lui Mom coboară din tavan: deasupra sprite-ului continuăm ultimul rând de pixeli (rochia).
        public bool ExtendTop;
        readonly Dictionary<string, Sprite[]> _extra = new Dictionary<string, Sprite[]>();

        public Sprite[] Extra(string name) => _extra.TryGetValue(name, out var s) ? s : null;

        // Lățimea și înălțimea pânzei în unități de lume (32 px = 1).
        public float Width => Idle[0].rect.width / Idle[0].pixelsPerUnit;
        public float Height => Idle[0].rect.height / Idle[0].pixelsPerUnit;

        public static UnitFrames ForHero(string heroId)
        {
            string p = "Sprites/Heroes/" + heroId + "/side_";
            var f = new UnitFrames();
            f.Idle = SpriteLibrary.TryFrames(p + "idle") ?? new[] { PixelSprites.Placeholder(48, 48, false) };
            f.Attack = SpriteLibrary.TryFrames(p + "attack") ?? f.Idle;
            f.Hurt = SpriteLibrary.TryFrames(p + "hurt") ?? f.Idle;
            f.Dead = SpriteLibrary.TryFrames(p + "dead");
            f.Portrait = SpriteLibrary.TryGet("Sprites/Heroes/" + heroId + "/portrait") ?? CropPortrait(f.Idle[0]);
            return f;
        }

        public static UnitFrames ForEnemy(string defId, bool boss)
        {
            string p = (boss ? "Sprites/Bosses/" : "Sprites/Enemies/") + defId + "/";
            var f = new UnitFrames();
            f.Idle = SpriteLibrary.TryFrames(p + "idle") ?? SpriteLibrary.TryFrames(p + "foot_idle");
            if (f.Idle == null)
            {
                Vector2Int size = PlaceholderSize(defId);
                f.Idle = new[] { PixelSprites.Placeholder(size.x, size.y, defId == "fly" || defId == "pooter") };
            }
            f.Attack = SpriteLibrary.TryFrames(p + "attack") ?? SpriteLibrary.TryFrames(p + "foot_stomp") ?? f.Idle;
            f.Hurt = SpriteLibrary.TryFrames(p + "hurt") ?? f.Idle;
            f.Dead = SpriteLibrary.TryFrames(p + "dead");
            f.Portrait = SpriteLibrary.TryGet(p + "portrait") ?? CropPortrait(f.Idle[0]);
            foreach (var extra in new[] { "closed", "open", "regen", "hand", "eye" })
            {
                var set = SpriteLibrary.TryFrames(p + extra);
                if (set != null) f._extra[extra] = set;
            }
            f.ExtendTop = defId == "mom";
            return f;
        }

        // Mărimile din docs/ART.md, pentru înlocuitorii inamicilor fără artă.
        static Vector2Int PlaceholderSize(string id)
        {
            switch (id)
            {
                case "fly": case "spider": case "pooter": return new Vector2Int(32, 32);
                case "monstro": return new Vector2Int(96, 96);
                case "gurdy": return new Vector2Int(128, 128);
                case "mom": return new Vector2Int(128, 160);
                default: return new Vector2Int(48, 48);
            }
        }

        // Portret de rezervă: decupăm capul (pătratul de sus) din primul cadru. Sprite.Create nu cere textură citibilă.
        static Sprite CropPortrait(Sprite s)
        {
            if (s == null || s.texture == null) return s;
            Rect r = s.textureRect;
            float side = Mathf.Min(24f, r.width, r.height);
            var crop = new Rect(r.x + (r.width - side) / 2f, r.y + r.height - side, side, side);
            return Sprite.Create(s.texture, crop, new Vector2(0.5f, 0.5f), s.pixelsPerUnit, 0, SpriteMeshType.FullRect);
        }
    }

    // Silueta albă a unui sprite (pentru clipirea la lovitură și conturul de țintă) + marginile pixelilor opaci.
    // Texturile importate nu sunt citibile din CPU, așa că le copiem întâi pe GPU într-un RenderTexture
    // (Graphics.Blit) și citim înapoi pixelii (ReadPixels). Ne trebuie doar canalul alfa: forma.
    public static class SpriteSilhouette
    {
        public sealed class Info
        {
            public Sprite White;
            public RectInt Opaque;   // dreptunghiul pixelilor vizibili, în pixeli față de colțul stânga-jos al sprite-ului
        }

        static readonly Dictionary<Sprite, Info> Cache = new Dictionary<Sprite, Info>();

        public static Info Of(Sprite s)
        {
            if (s == null) return null;
            if (Cache.TryGetValue(s, out var info)) return info;
            info = Build(s);
            Cache[s] = info;
            return info;
        }

        static Info Build(Sprite s)
        {
            var tex = s.texture;
            Rect r = s.textureRect;
            int x0 = Mathf.RoundToInt(r.x), y0 = Mathf.RoundToInt(r.y), w = Mathf.RoundToInt(r.width), h = Mathf.RoundToInt(r.height);
            var full = new RectInt(0, 0, w, h);
            if (tex == null || w <= 0 || h <= 0) return new Info { Opaque = full };

            Color32[] px = null;
            try
            {
                if (tex.isReadable)
                {
                    var all = tex.GetPixels32();
                    px = new Color32[w * h];
                    for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) px[y * w + x] = all[(y0 + y) * tex.width + x0 + x];
                }
                else if (SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null)
                {
                    var rt = RenderTexture.GetTemporary(tex.width, tex.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
                    var prev = RenderTexture.active;
                    Texture2D read = null;
                    try
                    {
                        Graphics.Blit(tex, rt);
                        RenderTexture.active = rt;
                        read = new Texture2D(w, h, TextureFormat.RGBA32, false);
                        read.ReadPixels(new Rect(x0, y0, w, h), 0, 0, false);
                        read.Apply(false);
                        px = read.GetPixels32();
                    }
                    finally
                    {
                        // Orice s-ar întâmpla, ținta de randare revine la cea veche și textura temporară se eliberează.
                        RenderTexture.active = prev;
                        RenderTexture.ReleaseTemporary(rt);
                        if (read != null) Object.Destroy(read);
                    }
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[SpriteSilhouette] nu pot citi " + s.name + ": " + e.Message);
                px = null;
            }
            if (px == null) return new Info { Opaque = full };

            int minX = w, minY = h, maxX = -1, maxY = -1;
            var white = new Color32[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    byte a = px[y * w + x].a > 16 ? (byte)255 : (byte)0;
                    white[y * w + x] = new Color32(255, 255, 255, a);
                    if (a == 0) continue;
                    if (x < minX) minX = x; if (x > maxX) maxX = x;
                    if (y < minY) minY = y; if (y > maxY) maxY = y;
                }
            var wt = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = s.name + "_white" };
            wt.SetPixels32(white);
            wt.Apply(false, true);
            Vector2 pivot = new Vector2(s.pivot.x / s.rect.width, s.pivot.y / s.rect.height);
            var sprite = Sprite.Create(wt, new Rect(0, 0, w, h), pivot, s.pixelsPerUnit, 0, SpriteMeshType.FullRect);
            sprite.name = s.name + "_white";
            var opaque = maxX < 0 ? full : new RectInt(minX, minY, maxX - minX + 1, maxY - minY + 1);
            return new Info { White = sprite, Opaque = opaque };
        }
    }
}
