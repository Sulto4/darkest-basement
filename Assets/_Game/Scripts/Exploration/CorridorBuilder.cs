using System.Collections.Generic;
using DarkestBasement.Combat;
using DarkestBasement.Core;
using DarkestBasement.Model.Map;
using UnityEngine;

namespace DarkestBasement.Exploration
{
    // Holul 3D dintre două camere, construit procedural din texturile etajului (Textures/Env/<etaj>).
    // Merge pe axa +Z: echipa pornește de la z = 0, trece prin trei segmente (fiecare poate ascunde un eveniment)
    // și ajunge la ușa de la capăt. Decorul (torțe, recuzită, sânge) e ales cu un Random cu seed,
    // așa că același hol arată la fel de fiecare dată.
    //
    // Holul e mai lung în spate și are podeaua prelungită spre +X: în ambuscadă camera iese prin peretele
    // din dreapta (care dispare, fiind văzut din spate) și vede holul "în secțiune", ca o casă de păpuși.
    public sealed class CorridorBuilder : MonoBehaviour
    {
        public const float HalfWidth = 2f;
        public const float Height = 4f;
        public const float BackZ = -16f;
        public const float DoorZ = 28f;
        public const float StopBeforeDoorZ = 25.0f;
        public const float EnterDoorZ = 27.3f;
        public static readonly float[] TriggerZ = { 4.5f, 11.5f, 18.5f };

        public FloorTheme Theme { get; private set; }
        public Door Door { get; private set; }

        // Obiectele de pe partea camerei (+X): în vederea laterală ar sta între cameră și luptă, așa că le ascundem.
        public readonly List<GameObject> NearSide = new List<GameObject>();
        // Grămezile din care iese un curio, câte una pe segment (null unde nu e curio).
        public readonly Billboard[] CurioPiles = new Billboard[3];

        readonly MaterialSet _materials = new MaterialSet();
        List<Mesh> _meshes = new List<Mesh>();

        public MaterialSet MaterialSet => _materials;

        public static CorridorBuilder Build(Transform parent, FloorTheme theme, string doorType, int seed, CorridorEventKind[] slotEvents)
        {
            var go = new GameObject("Corridor");
            go.transform.SetParent(parent, false);
            var c = go.AddComponent<CorridorBuilder>();
            c.Theme = theme;
            c.Construct(doorType, seed, slotEvents);
            return c;
        }

        void Construct(string doorType, int seed, CorridorEventKind[] slotEvents)
        {
            var rng = new System.Random(seed);
            var batch = new MeshBatch();
            var near = new MeshBatch(); // stâlpii de pe peretele dinspre cameră (+X): ascunși în vederea laterală
            var floor = _materials.Get(MaterialKind.Lit, Tex("floor"));
            var ceiling = _materials.Get(MaterialKind.Lit, Tex("ceiling"));
            var beam = _materials.Get(MaterialKind.Lit, Tex("beam"));
            var walls = new[] { _materials.Get(MaterialKind.Lit, Tex("wall")), _materials.Get(MaterialKind.Lit, Tex("wall_1")),
                                _materials.Get(MaterialKind.Lit, Tex("wall_2")), _materials.Get(MaterialKind.Lit, Tex("wall_3")) };

            float w = HalfWidth, h = Height;

            // Podea (prelungită spre +X pentru vederea în secțiune) și tavan, în bucăți de câte 4 unități.
            for (float z = BackZ; z < DoorZ; z += 4f)
            {
                float len = Mathf.Min(4f, DoorZ - z);
                batch.Quad(floor, new Vector3(-w, 0f, z), new Vector3(w + 14f, 0f, 0f), new Vector3(0f, 0f, len), 2f);
                batch.Quad(ceiling, new Vector3(-w, h, z + len), new Vector3(2f * w, 0f, 0f), new Vector3(0f, 0f, -len), 2f);
            }

            // Pereții laterali: coloane de 2 unități, din când în când o variantă (crăpături, sânge, caca).
            for (float z = BackZ; z < DoorZ; z += 2f)
            {
                batch.Quad(WallMaterial(walls, rng), new Vector3(-w, 0f, z), new Vector3(0f, 0f, 2f), new Vector3(0f, h, 0f), 2f);
                batch.Quad(WallMaterial(walls, rng), new Vector3(w, 0f, z + 2f), new Vector3(0f, 0f, -2f), new Vector3(0f, h, 0f), 2f);
                // Peretele din fund continuă deasupra tavanului: din interior nu se vede (tavanul îl acoperă),
                // dar în vederea laterală umple cerul negru de deasupra holului cu piatră.
                batch.Quad(walls[0], new Vector3(-w, h, z), new Vector3(0f, 0f, 2f), new Vector3(0f, 6f, 0f), 2f);
            }
            // Capetele: peretele cu ușa (spre -Z, îl vedem venind) și peretele din spate.
            batch.Quad(walls[0], new Vector3(-w, 0f, DoorZ), new Vector3(2f * w, 0f, 0f), new Vector3(0f, h, 0f), 2f);
            batch.Quad(walls[0], new Vector3(w, 0f, BackZ), new Vector3(-2f * w, 0f, 0f), new Vector3(0f, h, 0f), 2f);

            // Grinzi de lemn sub tavan, la fiecare 4 unități, cu stâlpi pe pereți: dau ritm mersului.
            for (float z = BackZ + 2f; z < DoorZ - 1f; z += 4f)
            {
                batch.Box(beam, new Vector3(0f, h - 0.16f, z), new Vector3(2f * w, 0.3f, 0.32f), 1f, top: false);
                float py = h - 0.3f;
                // Stâlpul din stânga: fața dinspre hol și fața dinspre noi (-Z). Din exterior ambele sunt invizibile.
                batch.Quad(beam, new Vector3(-w + 0.18f, 0f, z - 0.16f), new Vector3(0f, 0f, 0.32f), new Vector3(0f, py, 0f), 1f);
                batch.Quad(beam, new Vector3(-w, 0f, z - 0.16f), new Vector3(0.18f, 0f, 0f), new Vector3(0f, py, 0f), 1f);
                near.Quad(beam, new Vector3(w - 0.18f, 0f, z + 0.16f), new Vector3(0f, 0f, -0.32f), new Vector3(0f, py, 0f), 1f);
                near.Quad(beam, new Vector3(w - 0.18f, 0f, z - 0.16f), new Vector3(0.18f, 0f, 0f), new Vector3(0f, py, 0f), 1f);

                // Pânze în colțul dintre stâlp, perete și tavan.
                if (rng.NextDouble() < 0.28 && System.Array.IndexOf(Theme.WallDecals, "web_0") >= 0)
                {
                    bool left = rng.NextDouble() < 0.5;
                    EnvKit.WallDecal(transform, _materials, "web_0", new Vector3(left ? -w + 0.02f : w - 0.02f, py - 1f, z + 0.16f + 0.5f), left);
                }
                if (rng.NextDouble() < 0.35 && System.Array.IndexOf(Theme.WallDecals, "chain_0") >= 0)
                {
                    bool left = rng.NextDouble() < 0.5;
                    EnvKit.WallDecal(transform, _materials, "chain_0", new Vector3(left ? -w + 0.02f : w - 0.02f, h - 1.6f, z + 1.3f), left, 1.5f);
                }
            }
            _meshes = batch.Build(transform, "Corridor");
            var nearRoot = new GameObject("NearSide").transform;
            nearRoot.SetParent(transform, false);
            _meshes.AddRange(near.Build(nearRoot, "CorridorNear"));
            NearSide.Add(nearRoot.gameObject);

            // Colțurile întunecate dintre podea și pereți dau adâncime (fără ele, holul pare o cutie de carton).
            EnvKit.AoAlongWall(transform, _materials, new Vector3(-w, 0f, BackZ), new Vector3(-w, 0f, DoorZ), Vector3.right);
            EnvKit.AoAlongWall(transform, _materials, new Vector3(w, 0f, BackZ), new Vector3(w, 0f, DoorZ), Vector3.left);
            EnvKit.AoAlongWall(transform, _materials, new Vector3(-w, 0f, DoorZ), new Vector3(w, 0f, DoorZ), Vector3.back);

            BuildTorches(rng);
            BuildProps(rng, slotEvents);

            // Ușa de la capăt, luminată de două torțe.
            Door = Door.Create(transform, _materials, Theme, doorType, new Vector3(0f, 0f, DoorZ - 0.03f), 2.5f);
            TorchFlicker.Create(transform, new Vector3(-1.55f, 1.25f, DoorZ - 0.35f), Theme.TorchColor, Theme.TorchIntensity * 0.9f, 5.5f, (float)rng.NextDouble() * 50f);
            TorchFlicker.Create(transform, new Vector3(1.55f, 1.25f, DoorZ - 0.35f), Theme.TorchColor, Theme.TorchIntensity * 0.9f, 5.5f, (float)rng.NextDouble() * 50f);

            DustMotes.Create(transform, new Color(1f, 0.85f, 0.65f), seed);
        }

        Material WallMaterial(Material[] walls, System.Random rng)
        {
            if (rng.NextDouble() >= Theme.WallVariantChance) return walls[0];
            return walls[1 + rng.Next(3)];
        }

        // Torțe alternând pe pereți, cu goluri de întuneric între ele: lumina vine în valuri pe măsură ce mergi.
        void BuildTorches(System.Random rng)
        {
            bool left = rng.NextDouble() < 0.5;
            for (float z = -7f + EnvKit.Range(rng, 0f, 1.5f); z < DoorZ - 4f; z += EnvKit.Range(rng, 6.2f, 7.6f))
            {
                // Nu punem torța fix pe un stâlp.
                float fromBeam = Mathf.Repeat(z - (BackZ + 2f), 4f);
                float tz = fromBeam < 0.6f ? z + 0.7f : fromBeam > 3.4f ? z - 0.7f : z;
                var torch = TorchFlicker.Create(transform, new Vector3(left ? -HalfWidth + 0.28f : HalfWidth - 0.28f, 1.45f, tz),
                    Theme.TorchColor, Theme.TorchIntensity, 6.5f, (float)rng.NextDouble() * 50f);
                if (!left) NearSide.Add(torch.gameObject.transform.Find("Sprite").gameObject);
                if (!left) NearSide.Add(torch.gameObject.transform.Find("Halo").gameObject);
                left = !left;
            }
        }

        void BuildProps(System.Random rng, CorridorEventKind[] slotEvents)
        {
            // Recuzită pe lângă pereți, lăsând liber culoarul pe unde merg eroii.
            for (float z = BackZ + 3f; z < DoorZ - 2f; z += EnvKit.Range(rng, 1.2f, 2.4f))
            {
                if (rng.NextDouble() > 0.6) continue;
                bool left = rng.NextDouble() < 0.5;
                float x = (left ? -1f : 1f) * EnvKit.Range(rng, 1.5f, 1.75f);
                string name = EnvKit.Pick(rng, Theme.FloorProps);
                if (name == "tinted_rock" && rng.NextDouble() < 0.7) name = "rock_1";
                var prop = EnvKit.Prop(transform, name, new Vector3(x, 0f, z), EnvKit.Range(rng, 0.9f, 1.25f), rng.NextDouble() < 0.5);
                if (x > 0.3f) NearSide.Add(prop.gameObject);
            }

            // Sânge pe jos, unele pete chiar în calea eroilor.
            for (float z = BackZ + 2f; z < DoorZ - 1f; z += EnvKit.Range(rng, 3f, 6f))
            {
                string decal = "blood_decal_" + rng.Next(5);
                EnvKit.FloorDecal(transform, _materials, decal, new Vector3(EnvKit.Range(rng, -1.5f, 1.5f), 0f, z),
                    EnvKit.Range(rng, 1f, 1.7f), EnvKit.Range(rng, 0f, 360f));
            }

            // Curio-urile de pe hol arată ca orice altă grămadă de caca: nu știi ce ascund până nu ajungi la ele.
            for (int i = 0; i < 3; i++)
            {
                if (slotEvents == null || slotEvents[i] != CorridorEventKind.Curio) continue;
                CurioPiles[i] = EnvKit.Prop(transform, "poop_0", new Vector3(1.4f, 0f, TriggerZ[i] + 2.1f), 1.3f);
            }
        }

        Texture2D Tex(string name) => SpriteLibrary.Texture(Theme.Texture(name));

        // Scena de luptă a unei ambuscade: chiar pe hol, puțin în fața echipei, dar nu prea aproape de capete
        // (în vederea laterală se văd ~11 unități în stânga și în dreapta centrului).
        public CombatStageInfo AmbushStage(float partyZ, int floor)
        {
            float z = Mathf.Clamp(partyZ + 2.4f, BackZ + 11.5f, DoorZ - 10.5f);
            return new CombatStageInfo
            {
                Center = transform.TransformPoint(new Vector3(0f, 0f, z)),
                Right = transform.forward,
                Floor = floor,
                IsBoss = false,
                IsAmbush = true,
            };
        }

        public void SetCutaway(bool on)
        {
            foreach (var go in NearSide) if (go != null) go.SetActive(!on);
        }

        void OnDestroy()
        {
            foreach (var m in _meshes) if (m != null) Destroy(m);
            _materials.DestroyAll();
        }
    }
}
