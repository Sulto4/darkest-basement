using System;
using System.Collections.Generic;
using UnityEngine;

namespace DarkestBasement.FX
{
    // Culorile efectelor, luate din paleta din docs/ART.md.
    public static class FxColors
    {
        public static readonly Color32 Outline = Hex(0x1b0f0f);
        public static readonly Color32 TearLight = Hex(0x7fb2e5), TearDark = Hex(0x4a7fbf), TearHi = Hex(0xd6ecff);
        public static readonly Color32 Blood = Hex(0xd33c3c), BloodDark = Hex(0x9e1b1b), BloodDeep = Hex(0x5c0d0d);
        public static readonly Color32 Poop = Hex(0xa8743d), PoopDark = Hex(0x6b4423), PoopDeep = Hex(0x4a2e17);
        public static readonly Color32 Poison = Hex(0x7cc04a), PoisonDark = Hex(0x4f8a2a);
        public static readonly Color32 Block = Hex(0x6fa8dc), Stun = Hex(0xf2d14a), Weak = Hex(0x9a8fb0), Mark = Hex(0xff8a3d);
        public static readonly Color32 Strength = Hex(0xe05050), Taunt = Hex(0xc0c0c0);
        public static readonly Color32 Gold = Hex(0xe0b04a), GoldDark = Hex(0xa8782a);
        public static readonly Color32 Bone = Hex(0xe8dcc4), BoneDark = Hex(0xb9a888);
        public static readonly Color32 Steel = Hex(0xc8ccd4), SteelDark = Hex(0x7a7f86), Handle = Hex(0x6b4423);
        public static readonly Color32 BombBody = Hex(0x3a3640), BombHi = Hex(0x6a6470);
        public static readonly Color32 Incubus = Hex(0xff5a5a), Lilith = Hex(0x6b3f7d), Purple = Hex(0x9a6fc0);
        public static readonly Color32 Bile = Hex(0xc9c24a), BileDark = Hex(0x8a8a2a);
        public static readonly Color32 Dust = Hex(0x8a7a6a), Smoke = Hex(0x4a4048);
        public static readonly Color32 White = Hex(0xfff0e8);

        public static Color32 Hex(int rgb) => new Color32((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb, 255);
        public static Color32 A(Color32 c, byte a) => new Color32(c.r, c.g, c.b, a);
    }

    // Sprite-uri mici de efecte, desenate din cod la prima folosire (pixel cu pixel, ca în tools/art).
    // Le generăm aici pentru că sunt simple geometric (cercuri, inele, dungi) și vrem să le reglăm din cod.
    public static class PixelSprites
    {
        public const float Ppu = 32f;
        static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();
        static readonly Dictionary<string, Sprite[]> FrameCache = new Dictionary<string, Sprite[]>();

        // Construiește o textură w×h punct cu punct. pixel(x, y) primește coordonate cu (0,0) jos-stânga.
        public static Sprite Make(string key, int w, int h, Func<int, int, Color32> pixel, Vector2? pivot = null)
        {
            if (Cache.TryGetValue(key, out var cached)) return cached;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = "fx_" + key };
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    px[y * w + x] = pixel(x, y);
            tex.SetPixels32(px);
            tex.Apply(false, true);
            var s = Sprite.Create(tex, new Rect(0, 0, w, h), pivot ?? new Vector2(0.5f, 0.5f), Ppu, 0, SpriteMeshType.FullRect);
            s.name = key;
            Cache[key] = s;
            return s;
        }

        static readonly Color32 Clear = new Color32(0, 0, 0, 0);

        public static Sprite Pixel => Make("pixel", 1, 1, (x, y) => new Color32(255, 255, 255, 255));
        public static Sprite Square2 => Make("square2", 2, 2, (x, y) => new Color32(255, 255, 255, 255));

        // Un „plus” de 3×3 (scântei de vindecare).
        public static Sprite Plus => Make("plus", 3, 3, (x, y) => (x == 1 || y == 1) ? new Color32(255, 255, 255, 255) : Clear);

        // Disc plin, alb (se colorează din SpriteRenderer.color).
        public static Sprite Disc(int d) => Make("disc" + d, d, d, (x, y) => Inside(x, y, d, d) ? new Color32(255, 255, 255, 255) : Clear);

        // Umbra de sub picioare: elipsă moale în două trepte de transparență.
        public static Sprite Shadow(int w, int h) => Make("shadow" + w + "x" + h, w, h, (x, y) =>
        {
            float dx = (x + 0.5f - w / 2f) / (w / 2f), dy = (y + 0.5f - h / 2f) / (h / 2f);
            float r = dx * dx + dy * dy;
            if (r > 1f) return Clear;
            return new Color32(20, 10, 12, (byte)(r < 0.45f ? 120 : 70));
        });

        // Inel de selecție sub unitatea activă.
        public static Sprite RingEllipse(int w, int h) => Make("ringE" + w + "x" + h, w, h, (x, y) =>
        {
            float dx = (x + 0.5f - w / 2f) / (w / 2f), dy = (y + 0.5f - h / 2f) / (h / 2f);
            float r = Mathf.Sqrt(dx * dx + dy * dy);
            float edge = 1f - 1.6f / Mathf.Min(w, h * 2f);
            return r <= 1f && r >= edge ? new Color32(255, 255, 255, 255) : Clear;
        });

        // Lacrima lui Isaac: bilă cu contur, umbră jos-dreapta și un punct de lumină sus-stânga.
        public static Sprite Tear(int d, Color32 light, Color32 dark, Color32 hi, string key)
        {
            return Make("tear_" + key + d, d + 2, d + 2, (x, y) =>
            {
                int cx = x - 1, cy = y - 1;
                bool inside = Inside(cx, cy, d, d);
                if (!inside) return Near(cx, cy, d) ? FxColors.Outline : Clear;
                float fx = (cx + 0.5f) / d - 0.5f, fy = (cy + 0.5f) / d - 0.5f;
                if (fx < -0.1f && fy > 0.12f && fx > -0.36f && fy < 0.36f && d >= 5) return hi;
                return fx + (-fy) > 0.25f ? dark : light;
            });
        }

        public static Sprite TearSprite(int d = 7) => Tear(d, FxColors.TearLight, FxColors.TearDark, FxColors.TearHi, "blue");
        public static Sprite BloodTearSprite(int d = 7) => Tear(d, FxColors.Blood, FxColors.BloodDark, FxColors.A(FxColors.White, 255), "red");
        public static Sprite PoopSprite(int d = 8) => Tear(d, FxColors.Poop, FxColors.PoopDark, FxColors.Hex(0xc8945d), "poop");
        public static Sprite IpecacSprite(int d = 9) => Tear(d, FxColors.Poison, FxColors.PoisonDark, FxColors.Hex(0xc8f0a0), "ipecac");
        public static Sprite BileSprite(int d = 6) => Tear(d, FxColors.Bile, FxColors.BileDark, FxColors.Hex(0xf0ec9a), "bile");

        // Bomba din Isaac: corp rotund închis, cu fitil și scânteie.
        public static Sprite Bomb => Make("bomb", 12, 14, (x, y) =>
        {
            if (x >= 6 && x <= 7 && y >= 10 && y <= 11) return FxColors.PoopDark;          // fitil
            if ((x == 7 || x == 8) && y == 12) return FxColors.Stun;                       // scânteia
            if (x == 8 && y == 13) return FxColors.White;
            int cx = x - 1, cy = y - 1;
            bool inside = Inside(cx, cy, 10, 10);
            if (!inside) return Near(cx, cy, 10) && y < 11 ? FxColors.Outline : Clear;
            return cx < 4 && cy > 5 ? FxColors.BombHi : FxColors.BombBody;
        });

        // Cuțitul lui Mom: lamă argintie + mâner maro, orientat spre dreapta.
        public static Sprite Knife => Make("knife", 16, 5, (x, y) =>
        {
            if (x <= 4) return y >= 1 && y <= 3 ? (y == 3 ? FxColors.PoopDark : FxColors.Handle) : (x <= 4 && (y == 0 || y == 4) ? FxColors.Outline : Clear);
            if (x == 5) return y >= 0 && y <= 4 ? FxColors.SteelDark : Clear;               // garda
            int top = x >= 13 ? 3 - (x - 13) : 3;                                            // vârful se subțiază
            if (y >= 1 && y <= top) return y == top ? FxColors.White : FxColors.Steel;
            if (y == 0 && x < 15) return FxColors.Outline;
            if (y == top + 1 && top < 4) return FxColors.Outline;
            return Clear;
        });

        // Osul aruncat de Bony: desenat ca text, '#' = os, 'd' = umbră, 'o' = contur.
        public static Sprite BoneSprite => Make("bone", 12, 6, (x, y) =>
        {
            string[] rows = { ".oo......oo.", "o##oooooo##o", "o##########o", "o#dddddddd#o", "o##oooooo##o", ".oo......oo." };
            char c = rows[5 - y][x];
            return c == '#' ? FxColors.Bone : c == 'd' ? FxColors.BoneDark : c == 'o' ? FxColors.Outline : Clear;
        });

        // Steluța de stun (5 colțuri, 7×7).
        public static Sprite Star => Make("star", 7, 7, (x, y) =>
        {
            string[] rows = { "...#...", "...#...", "#######", ".#####.", "..###..", ".##.##.", "##...##" };
            return rows[6 - y][x] == '#' ? FxColors.Stun : Clear;
        });

        // Săgeată (vârf de țintire) pentru UI, 7×7, albă, cu vârful spre DREAPTA: rotită cu unghiul direcției
        // de mers (Atan2), arată spre țintă. (Desenată invers, „<”, vârful ar fi arătat înapoi, spre atacator.)
        public static Sprite Chevron => Make("chevron_right", 7, 7, (x, y) =>
        {
            int d = Mathf.Abs(y - 3);
            return x >= 4 - d && x <= 6 - d ? new Color32(255, 255, 255, 255) : Clear;
        });

        // Aceeași săgeată, mai mare (11×11) și cu contur închis de 1 px: se vede și peste fundaluri aglomerate.
        // Miezul e alb, deci ia culoarea din Image.color; conturul rămâne închis.
        public static Sprite ChevronOutlined => Make("chevron_outlined", 11, 11, (x, y) =>
        {
            if (ChevronCore(x, y)) return new Color32(255, 255, 255, 255);
            bool edge = ChevronCore(x - 1, y) || ChevronCore(x + 1, y) || ChevronCore(x, y - 1) || ChevronCore(x, y + 1);
            return edge ? OutlineDark : Clear;
        });

        static bool ChevronCore(int x, int y)
        {
            if (y < 1 || y > 9) return false;
            int d = Mathf.Abs(y - 5);
            return x >= 6 - d && x <= 8 - d;
        }

        // Punct de 4×4: miez alb 2×2 cu contur închis (colțurile goale, ca să pară rotund).
        public static Sprite DotOutlined => Make("dot_outlined", 4, 4, (x, y) =>
        {
            bool edgeX = x == 0 || x == 3, edgeY = y == 0 || y == 3;
            if (edgeX && edgeY) return Clear;
            return edgeX || edgeY ? OutlineDark : new Color32(255, 255, 255, 255);
        });

        static readonly Color32 OutlineDark = new Color32(0x1b, 0x0f, 0x0f, 255);

        // Dunga verticală pentru raza Brimstone: alb în miez, roșu spre margini, transparent la capete.
        // E de 1 px lățime; o întindem pe orizontală cât e de lungă raza.
        public static Sprite BeamStrip(string key, Color32 core, Color32 mid, Color32 edge)
        {
            const int h = 16;
            return Make("beam_" + key, 1, h, (x, y) =>
            {
                float d = Mathf.Abs(y + 0.5f - h / 2f) / (h / 2f);
                if (d < 0.25f) return core;
                if (d < 0.55f) return mid;
                if (d < 0.85f) return edge;
                return FxColors.A(edge, 110);
            }, new Vector2(0f, 0.5f));
        }

        // Cadre de explozie: disc alb → disc galben-portocaliu → inel de fum care se destramă.
        public static Sprite[] Explosion(int size = 40)
        {
            string key = "explosion" + size;
            if (FrameCache.TryGetValue(key, out var set)) return set;
            var frames = new Sprite[6];
            for (int f = 0; f < frames.Length; f++)
            {
                int frame = f;
                frames[f] = Make(key + "_" + f, size, size, (x, y) =>
                {
                    float dx = x + 0.5f - size / 2f, dy = y + 0.5f - size / 2f;
                    float r = Mathf.Sqrt(dx * dx + dy * dy) / (size / 2f);
                    // „Zgomot” determinist ca marginea să fie zimțată, nu un cerc de compas.
                    float jag = ((x * 7 + y * 13 + frame * 5) % 5) * 0.03f;
                    switch (frame)
                    {
                        case 0: return r < 0.42f + jag ? FxColors.White : Clear;
                        case 1: return r < 0.55f + jag ? (r < 0.3f ? FxColors.White : FxColors.Stun) : (r < 0.68f + jag ? FxColors.Mark : Clear);
                        case 2: return r < 0.35f ? FxColors.Stun : r < 0.75f + jag ? FxColors.Mark : r < 0.88f + jag ? FxColors.Blood : Clear;
                        case 3: return r < 0.5f ? FxColors.Mark : r < 0.9f + jag ? FxColors.Smoke : Clear;
                        case 4: return r > 0.55f + jag && r < 0.98f ? FxColors.A(FxColors.Smoke, 200) : Clear;
                        default: return r > 0.78f + jag && r < 1f && (x + y) % 2 == 0 ? FxColors.A(FxColors.Smoke, 150) : Clear;
                    }
                });
            }
            FrameCache[key] = frames;
            return frames;
        }

        // Stropi la impactul unei lacrimi: inel care se lărgește și se rupe.
        public static Sprite[] Splash(Color32 color, string key, int size = 14)
        {
            key = "splash_" + key + size;
            if (FrameCache.TryGetValue(key, out var set)) return set;
            var frames = new Sprite[4];
            for (int f = 0; f < frames.Length; f++)
            {
                int frame = f;
                frames[f] = Make(key + "_" + f, size, size, (x, y) =>
                {
                    float dx = x + 0.5f - size / 2f, dy = y + 0.5f - size / 2f;
                    float r = Mathf.Sqrt(dx * dx + dy * dy) / (size / 2f);
                    float inner = 0.15f + frame * 0.22f, outer = inner + 0.3f - frame * 0.04f;
                    bool on = r >= inner && r <= outer;
                    if (frame >= 2 && ((x + y + frame) % 3 == 0)) on = false;
                    return on ? color : Clear;
                });
            }
            FrameCache[key] = frames;
            return frames;
        }

        // Înlocuitor pentru sprite-urile de inamici care încă nu există: o siluetă închisă cu contur magenta,
        // ca să se vadă imediat că lipsește arta, dar fără să strice compoziția scenei.
        public static Sprite Placeholder(int w, int h, bool flying)
        {
            return Make("placeholder" + w + "x" + h + (flying ? "f" : ""), w, h, (x, y) =>
            {
                float cx = w / 2f;
                float headR = w * 0.3f, headCy = h * (flying ? 0.62f : 0.68f);
                float dx = x + 0.5f - cx, dy = y + 0.5f - headCy;
                bool head = dx * dx + dy * dy <= headR * headR;
                bool body = !flying && Mathf.Abs(x + 0.5f - cx) <= w * 0.2f && y >= 1 && y <= headCy;
                bool wing = flying && Mathf.Abs(dy) < h * 0.1f && Mathf.Abs(dx) < w * 0.46f;
                bool inside = head || body || wing;
                if (inside)
                {
                    // Doi ochi roșii, mutați spre stânga: inamicii privesc spre eroi.
                    bool eyeRow = Mathf.Abs(dy) < 1.2f;
                    bool eye = eyeRow && (Mathf.Abs(dx + headR * 0.55f) < 1.2f || Mathf.Abs(dx + headR * 0.05f) < 1.2f);
                    return eye ? FxColors.Blood : FxColors.Hex(0x2a1f24);
                }
                // Contur magenta de 1 px.
                for (int oy = -1; oy <= 1; oy++)
                    for (int ox = -1; ox <= 1; ox++)
                    {
                        if (ox == 0 && oy == 0) continue;
                        float ndx = x + ox + 0.5f - cx, ndy = y + oy + 0.5f - headCy;
                        bool n = ndx * ndx + ndy * ndy <= headR * headR
                                 || (!flying && Mathf.Abs(x + ox + 0.5f - cx) <= w * 0.2f && y + oy >= 1 && y + oy <= headCy)
                                 || (flying && Mathf.Abs(ndy) < h * 0.1f && Mathf.Abs(ndx) < w * 0.46f);
                        if (n) return new Color32(255, 0, 255, 255);
                    }
                return Clear;
            }, new Vector2(0.5f, 0f));
        }

        static bool Inside(int x, int y, int w, int h)
        {
            if (x < 0 || y < 0 || x >= w || y >= h) return false;
            float dx = (x + 0.5f - w / 2f) / (w / 2f), dy = (y + 0.5f - h / 2f) / (h / 2f);
            return dx * dx + dy * dy <= 1.0f;
        }

        // Vecin al discului (pentru contur de 1 px).
        static bool Near(int x, int y, int d)
        {
            return Inside(x - 1, y, d, d) || Inside(x + 1, y, d, d) || Inside(x, y - 1, d, d) || Inside(x, y + 1, d, d);
        }
    }
}
