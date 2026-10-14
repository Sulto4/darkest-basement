using System.Collections.Generic;
using DarkestBasement.Audio;
using DarkestBasement.Combat;
using DarkestBasement.Core;
using DarkestBasement.FX;
using DarkestBasement.Model.Map;
using UnityEngine;

namespace DarkestBasement.Exploration
{
    // O cameră la capătul unui hol, din aceleași texturi. E lungă pe Z (30 de unități), pentru că lupta se vede
    // din lateral: eroii în stânga (-Z), inamicii în dreapta (+Z), scena în centru. Camera de luptă iese prin
    // peretele +X (invizibil din exterior) și vede încăperea ca pe o scenă de teatru.
    //
    // Fiecare tip de cameră are "piesa" lui: piedestalul cu carte (Treasure), patul lui Isaac (Bedroom),
    // cufărul sau grămada misterioasă (Curio), iar după boss apare trapa spre etajul următor.
    public sealed class RoomBuilder : MonoBehaviour
    {
        public const float HalfLength = 15f;
        public const float HalfWidth = 5f;
        public const float Height = 5f;
        public const float EntryZ = -10.5f;  // unde apare echipa când intră
        public const float RestZ = -2.3f;    // unde se oprește (rândul din față)
        public static readonly Vector3 FeatureSpot = new Vector3(0f, 0f, 2.6f);

        public FloorTheme Theme { get; private set; }
        public RoomType Type { get; private set; }
        public readonly List<GameObject> NearSide = new List<GameObject>();

        // Piesa camerei (piedestal, pat, curio) și trapa.
        public Transform Feature { get; private set; }
        public Billboard FeatureSprite { get; private set; }
        public Billboard FloatingCard { get; private set; }
        public Light FeatureLight { get; private set; }
        public Transform Trapdoor { get; private set; }
        public Vector3 TrapdoorLocal => new Vector3(0f, 0f, 4.2f);

        readonly MaterialSet _materials = new MaterialSet();
        List<Mesh> _meshes = new List<Mesh>();
        readonly List<GameObject> _featureParts = new List<GameObject>();

        public MaterialSet MaterialSet => _materials;

        public static RoomBuilder Build(Transform parent, FloorTheme theme, RoomType type, bool cleared, bool bossDefeated, int seed)
        {
            var go = new GameObject("Room_" + type);
            go.transform.SetParent(parent, false);
            var room = go.AddComponent<RoomBuilder>();
            room.Theme = theme;
            room.Type = type;
            room.Construct(seed, cleared);
            if (type == RoomType.Boss && bossDefeated) room.ShowTrapdoor(false);
            return room;
        }

        void Construct(int seed, bool cleared)
        {
            var rng = new System.Random(seed);
            var batch = new MeshBatch();
            var floor = _materials.Get(MaterialKind.Lit, Tex("floor"));
            var ceiling = _materials.Get(MaterialKind.Lit, Tex("ceiling"));
            var beam = _materials.Get(MaterialKind.Lit, Tex("beam"));
            var walls = new[] { _materials.Get(MaterialKind.Lit, Tex("wall")), _materials.Get(MaterialKind.Lit, Tex("wall_1")),
                                _materials.Get(MaterialKind.Lit, Tex("wall_2")), _materials.Get(MaterialKind.Lit, Tex("wall_3")) };
            float L = HalfLength, W = HalfWidth, H = Height;

            for (float z = -L; z < L; z += 5f)
            {
                batch.Quad(floor, new Vector3(-W, 0f, z), new Vector3(W + 16f, 0f, 0f), new Vector3(0f, 0f, 5f), 2f);
                batch.Quad(ceiling, new Vector3(-W, H, z + 5f), new Vector3(2f * W, 0f, 0f), new Vector3(0f, 0f, -5f), 2f);
            }
            for (float z = -L; z < L; z += 2f)
            {
                batch.Quad(Wall(walls, rng), new Vector3(-W, 0f, z), new Vector3(0f, 0f, 2f), new Vector3(0f, H, 0f), 2f);
                batch.Quad(Wall(walls, rng), new Vector3(W, 0f, z + 2f), new Vector3(0f, 0f, -2f), new Vector3(0f, H, 0f), 2f);
                // Peste tavan: invizibil din interior, umple partea de sus a cadrului în vederea laterală.
                batch.Quad(walls[0], new Vector3(-W, H, z), new Vector3(0f, 0f, 2f), new Vector3(0f, 6f, 0f), 2f);
            }
            for (float x = -W; x < W; x += 2f)
            {
                float len = Mathf.Min(2f, W - x);
                batch.Quad(Wall(walls, rng), new Vector3(x, 0f, L), new Vector3(len, 0f, 0f), new Vector3(0f, H, 0f), 2f);
                batch.Quad(Wall(walls, rng), new Vector3(x + len, 0f, -L), new Vector3(-len, 0f, 0f), new Vector3(0f, H, 0f), 2f);
            }
            // Grinzi peste lățimea camerei și stâlpi pe peretele din spate.
            for (float z = -L + 2.5f; z < L; z += 5f)
            {
                batch.Box(beam, new Vector3(0f, H - 0.2f, z), new Vector3(2f * W, 0.36f, 0.4f), 1f, top: false);
                batch.Quad(beam, new Vector3(-W + 0.22f, 0f, z - 0.2f), new Vector3(0f, 0f, 0.4f), new Vector3(0f, H - 0.38f, 0f), 1f);
                batch.Quad(beam, new Vector3(-W, 0f, z - 0.2f), new Vector3(0.22f, 0f, 0f), new Vector3(0f, H - 0.38f, 0f), 1f);
                batch.Quad(beam, new Vector3(W - 0.22f, 0f, z + 0.2f), new Vector3(0f, 0f, -0.4f), new Vector3(0f, H - 0.38f, 0f), 1f);
                batch.Quad(beam, new Vector3(W - 0.22f, 0f, z - 0.2f), new Vector3(0.22f, 0f, 0f), new Vector3(0f, H - 0.38f, 0f), 1f);
            }
            _meshes = batch.Build(transform, "Room");
            EnvKit.AoAlongWall(transform, _materials, new Vector3(-W, 0f, -L), new Vector3(-W, 0f, L), Vector3.right);
            EnvKit.AoAlongWall(transform, _materials, new Vector3(W, 0f, -L), new Vector3(W, 0f, L), Vector3.left);
            EnvKit.AoAlongWall(transform, _materials, new Vector3(-W, 0f, L), new Vector3(W, 0f, L), Vector3.back);
            EnvKit.AoAlongWall(transform, _materials, new Vector3(-W, 0f, -L), new Vector3(W, 0f, -L), Vector3.forward);

            // Arcada întunecată din capăt: "drumul mai departe" în adâncul subsolului.
            var frameTex = Tex("door_frame");
            if (frameTex != null)
            {
                var arch = new GameObject("Arch");
                arch.transform.SetParent(transform, false);
                arch.transform.localPosition = new Vector3(0f, 0f, L - 0.03f);
                arch.transform.localScale = new Vector3(3f, 3f, 1f);
                arch.AddComponent<MeshFilter>().sharedMesh = MeshKit.BillboardQuad;
                arch.AddComponent<MeshRenderer>().sharedMaterial = _materials.Get(MaterialKind.LitCutout, frameTex);
            }

            BuildLights(rng);
            BuildProps(rng);
            BuildFeature(rng, cleared);
            DustMotes.Create(transform, new Color(1f, 0.85f, 0.65f), seed + 7);
        }

        Material Wall(Material[] walls, System.Random rng)
            => rng.NextDouble() >= Theme.WallVariantChance ? walls[0] : walls[1 + rng.Next(3)];

        void BuildLights(System.Random rng)
        {
            Color torch = Theme.TorchColor;
            // În camera boss-ului flăcările sunt mai roșii: pericolul se simte înainte să-l vezi.
            if (Type == RoomType.Boss) torch = Color.Lerp(torch, new Color(1f, 0.2f, 0.12f), 0.45f);
            float[] zs = { -11f, -4.5f, 4.5f, 11f };
            foreach (float z in zs)
            {
                TorchFlicker.Create(transform, new Vector3(-HalfWidth + 0.3f, 1.6f, z + EnvKit.Range(rng, -0.4f, 0.4f)), torch, Theme.TorchIntensity, 7.5f, (float)rng.NextDouble() * 50f);
                var near = TorchFlicker.Create(transform, new Vector3(HalfWidth - 0.3f, 1.6f, z + EnvKit.Range(rng, -0.4f, 0.4f)), torch, Theme.TorchIntensity * 0.8f, 7f, (float)rng.NextDouble() * 50f);
                NearSide.Add(near.transform.Find("Sprite").gameObject);
                NearSide.Add(near.transform.Find("Halo").gameObject);
            }
            // Două torțe înfipte în podea, în spatele liniei de luptă: unitățile de pe scenă primesc lumină caldă
            // (sprite-urile se colorează după lumina locală), iar în vederea laterală flăcările stau în fundal.
            TorchFlicker.Create(transform, new Vector3(-2.7f, 0f, -4.6f), torch, Theme.TorchIntensity * 0.9f, 6.5f, (float)rng.NextDouble() * 50f);
            TorchFlicker.Create(transform, new Vector3(-2.7f, 0f, 4.6f), torch, Theme.TorchIntensity * 0.9f, 6.5f, (float)rng.NextDouble() * 50f);
            TorchFlicker.Create(transform, new Vector3(-2.1f, 1.5f, HalfLength - 0.4f), torch, Theme.TorchIntensity, 6f, (float)rng.NextDouble() * 50f);
            TorchFlicker.Create(transform, new Vector3(2.1f, 1.5f, HalfLength - 0.4f), torch, Theme.TorchIntensity, 6f, (float)rng.NextDouble() * 50f);
        }

        void BuildProps(System.Random rng)
        {
            // Recuzita stă lângă peretele din spate (-X) și spre capete, nu pe scena de luptă (x ≈ 0, |z| < 7).
            int count = Type == RoomType.Boss ? 16 : 11;
            for (int i = 0; i < count; i++)
            {
                float z = EnvKit.Range(rng, -HalfLength + 1.5f, HalfLength - 1.5f);
                float x = Mathf.Abs(z) < 8f ? EnvKit.Range(rng, -HalfWidth + 0.6f, -2.4f) : EnvKit.Range(rng, -HalfWidth + 0.6f, HalfWidth - 0.8f);
                string name = Type == RoomType.Boss && rng.NextDouble() < 0.5 ? (rng.NextDouble() < 0.5 ? "bones_0" : "bones_1") : EnvKit.Pick(rng, Theme.FloorProps);
                var prop = EnvKit.Prop(transform, name, new Vector3(x, 0f, z), EnvKit.Range(rng, 0.9f, 1.3f), rng.NextDouble() < 0.5);
                if (x > 0.3f) NearSide.Add(prop.gameObject);
            }
            int blood = Type == RoomType.Boss ? 9 : 5;
            for (int i = 0; i < blood; i++)
                EnvKit.FloorDecal(transform, _materials, "blood_decal_" + rng.Next(5),
                    new Vector3(EnvKit.Range(rng, -HalfWidth + 0.8f, HalfWidth - 0.8f), 0f, EnvKit.Range(rng, -HalfLength + 2f, HalfLength - 2f)),
                    EnvKit.Range(rng, 1.1f, 2.2f), EnvKit.Range(rng, 0f, 360f));
            for (float z = -HalfLength + 2.5f; z < HalfLength; z += 5f)
                if (rng.NextDouble() < 0.35 && System.Array.IndexOf(Theme.WallDecals, "web_0") >= 0)
                    EnvKit.WallDecal(transform, _materials, "web_0", new Vector3(-HalfWidth + 0.02f, Height - 1.4f, z + 0.7f), true);
        }

        void BuildFeature(System.Random rng, bool cleared)
        {
            var root = new GameObject("Feature").transform;
            root.SetParent(transform, false);
            root.localPosition = FeatureSpot;
            Feature = root;

            switch (Type)
            {
                case RoomType.Treasure:
                {
                    // Piedestalul din Isaac: o carte plutește deasupra, cu halo auriu și o rază de lumină.
                    FeatureSprite = EnvKit.Prop(root, "pedestal", Vector3.zero, 1.5f);
                    FeatureLight = AddLight(root, new Vector3(0f, 2.4f, -0.6f), new Color(1f, 0.8f, 0.4f), cleared ? 0.6f : 2.8f, 7f);
                    if (!cleared)
                    {
                        FloatingCard = Billboard.Create(root, "Card", SpriteLibrary.Get("Sprites/UI/card_back"), new Vector3(0f, 2.0f, 0f));
                        FloatingCard.SetScale(0.45f);
                        FloatingCard.UseLocalLight = false;
                        FloatingCard.Brightness = 1.15f;
                        var halo = Billboard.Create(root, "Glow", null, new Vector3(0f, 2.4f, 0.05f), MaterialKind.Additive);
                        halo.Material.SetTexture(Materials.BaseMap, ProceduralTextures.Halo);
                        halo.Quad.localScale = new Vector3(2.6f, 2.6f, 1f);
                        halo.Quad.localPosition = new Vector3(0f, -1.3f, 0f);
                        halo.UseLocalLight = false;
                        halo.Tint = new Color(1f, 0.8f, 0.35f, 0.45f);
                        var shaft = Billboard.Create(root, "Shaft", null, new Vector3(0f, 1.2f, 0.08f), MaterialKind.Additive);
                        shaft.Material.SetTexture(Materials.BaseMap, ProceduralTextures.Shaft);
                        shaft.Quad.localScale = new Vector3(1.1f, 3.6f, 1f);
                        shaft.Quad.localPosition = new Vector3(0f, 0f, 0f);
                        shaft.UseLocalLight = false;
                        shaft.Tint = new Color(1f, 0.85f, 0.5f, 0.28f);
                        _featureParts.Add(FloatingCard.gameObject);
                        _featureParts.Add(halo.gameObject);
                        _featureParts.Add(shaft.gameObject);
                    }
                    break;
                }
                case RoomType.Bedroom:
                    // Patul lui Isaac, sub o lumină rece de lună, ca singurul loc liniștit din subsol.
                    root.localPosition = FeatureSpot + new Vector3(-1.6f, 0f, 0.4f);
                    FeatureSprite = EnvKit.Prop(root, "bed", Vector3.zero, 2f);
                    FeatureLight = AddLight(root, new Vector3(0.6f, 2.6f, -1.2f), new Color(0.65f, 0.75f, 1f), 2.2f, 6f);
                    break;
                case RoomType.Curio:
                {
                    // Cufărul (sau, din când în când, o grămadă suspectă). Are collider: îl poți și apăsa cu mouse-ul.
                    string sprite = cleared ? "chest_open" : (rng.NextDouble() < 0.7 ? "chest_closed" : "tinted_rock");
                    FeatureSprite = EnvKit.Prop(root, sprite, Vector3.zero, 1.6f);
                    FeatureLight = AddLight(root, new Vector3(0f, 1.8f, -1f), new Color(1f, 0.75f, 0.45f), cleared ? 0.6f : 1.6f, 5f);
                    var col = root.gameObject.AddComponent<BoxCollider>();
                    col.center = new Vector3(0f, 0.6f, 0f);
                    col.size = new Vector3(1.8f, 1.4f, 1.8f);
                    break;
                }
            }
        }

        static Light AddLight(Transform parent, Vector3 localPos, Color color, float intensity, float range)
        {
            var go = new GameObject("FeatureLight");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            var l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = color;
            l.intensity = intensity;
            l.range = range;
            l.shadows = LightShadows.None;
            LocalLight.Register(l);
            return l;
        }

        // Cartea de pe piedestal plutește și pulsează (doar cât încă e acolo).
        void Update()
        {
            if (FloatingCard != null)
            {
                float t = Time.time;
                FloatingCard.transform.localPosition = new Vector3(0f, 2.0f + Mathf.Sin(t * 2f) * 0.1f, 0f);
                if (FeatureLight != null) FeatureLight.intensity = 2.6f + Mathf.Sin(t * 3f) * 0.4f;
            }
        }

        // După ce ai ales o carte: lumina piedestalului se stinge, cartea dispare.
        public void ConsumeFeature()
        {
            foreach (var go in _featureParts) if (go != null) Destroy(go);
            _featureParts.Clear();
            FloatingCard = null;
            if (FeatureLight != null)
            {
                var l = FeatureLight;
                float from = l.intensity;
                Tween.Value(0.8f, k => { if (l != null) l.intensity = Mathf.Lerp(from, 0.6f, k); }, Ease.OutCubic, null, 0f, false, l);
            }
            if (Type == RoomType.Curio && FeatureSprite != null && FeatureSprite.Sprite != null && FeatureSprite.Sprite.name == "chest_closed")
                FeatureSprite.SetSprite(SpriteLibrary.Get("Sprites/Props/chest_open"));
        }

        // Trapa spre etajul următor: apare după ce boss-ul moare, cu un nor de praf și un bubuit.
        public void ShowTrapdoor(bool animate)
        {
            if (Trapdoor != null) return;
            var r = EnvKit.FloorDecal(transform, _materials, "trapdoor", TrapdoorLocal, 2.6f, 0f);
            // Desenul e "privit puțin de sus"; culcat pe podea l-ar turti perspectiva, așa că îl lungim pe Z.
            var s = r.transform.localScale;
            r.transform.localScale = new Vector3(s.x, s.y, s.z * 1.7f);
            Trapdoor = r.transform;
            if (!animate) return;
            Vector3 full = r.transform.localScale;
            r.transform.localScale = new Vector3(0.01f, 1f, 0.01f);
            Tween.Scale(r.transform, full, 0.45f, Ease.OutBack);
            AudioManager.Play("monstro_land", 0.6f, 0.8f);
            ScreenShake.Add(0.35f);
        }

        public CombatStageInfo StageInfo(int floor, bool boss)
        {
            return new CombatStageInfo
            {
                Center = transform.position,
                Right = transform.forward,
                Floor = floor,
                IsBoss = boss,
                IsAmbush = false,
            };
        }

        public void SetCutaway(bool on)
        {
            foreach (var go in NearSide) if (go != null) go.SetActive(!on);
        }

        Texture2D Tex(string name) => SpriteLibrary.Texture(Theme.Texture(name));

        void OnDestroy()
        {
            foreach (var m in _meshes) if (m != null) Destroy(m);
            _materials.DestroyAll();
            if (FeatureLight != null) LocalLight.Unregister(FeatureLight);
        }
    }
}
