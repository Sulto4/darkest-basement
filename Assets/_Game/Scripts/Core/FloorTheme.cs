using UnityEngine;

namespace DarkestBasement.Core
{
    // Tot ce schimbă atmosfera de la un etaj la altul: lumină ambientală, ceață, culoarea torțelor, recuzită, sunet.
    // Întunericul e intenționat: ambientul e aproape negru, ca torțele să creeze insule de lumină (ca în Darkest Dungeon).
    public sealed class FloorTheme
    {
        public int Index;
        public string Id;          // numele folderului din Textures/Env/<id>
        public string Title;       // numele afișat pe cartonașul de etaj
        public Color Ambient;      // lumina "de nicăieri": cât de negre sunt colțurile
        public Color Fog;          // culoarea în care se pierde holul în depărtare
        public float FogStart;     // până aici (unități de la cameră) nu e ceață: echipa rămâne clară
        public float FogEnd;       // de aici încolo e doar beznă
        public Color TorchColor;
        public float TorchIntensity;
        public Color Accent;       // culoarea titlului și a detaliilor UI ale etajului
        public string Ambience;    // bucla de fundal din audio_manifest
        public string[] FloorProps;   // recuzită de podea (Sprites/Props/<nume>)
        public string[] WallDecals;   // lipite pe perete (pânze, lanțuri)
        public float WallVariantChance;

        public string Texture(string name) => "Textures/Env/" + Id + "/" + name;

        public static FloorTheme ForFloor(int floor)
        {
            switch (floor)
            {
                case 2:
                    return new FloorTheme
                    {
                        Index = 2, Id = "caves", Title = "CAVES",
                        Ambient = new Color(0.050f, 0.060f, 0.070f),
                        Fog = new Color(0.010f, 0.016f, 0.020f), FogStart = 5f, FogEnd = 25f,
                        TorchColor = new Color(1.00f, 0.66f, 0.36f), TorchIntensity = 3.2f,
                        Accent = Palette("#9fb6c8"),
                        Ambience = "caves_ambience",
                        FloorProps = new[] { "rock_0", "rock_1", "rock_2", "mushroom_0", "mushroom_1", "bones_0", "bones_1", "tinted_rock" },
                        WallDecals = new[] { "web_0" },
                        WallVariantChance = 0.35f,
                    };
                case 3:
                    return new FloorTheme
                    {
                        Index = 3, Id = "depths", Title = "DEPTHS",
                        Ambient = new Color(0.070f, 0.030f, 0.040f),
                        Fog = new Color(0.022f, 0.006f, 0.010f), FogStart = 4.5f, FogEnd = 23f,
                        TorchColor = new Color(1.00f, 0.48f, 0.30f), TorchIntensity = 3.4f,
                        Accent = Palette("#d33c3c"),
                        Ambience = "depths_ambience",
                        FloorProps = new[] { "bones_0", "bones_1", "rock_0", "rock_2", "poop_2" },
                        WallDecals = new[] { "chain_0", "web_0" },
                        WallVariantChance = 0.4f,
                    };
                default:
                    return new FloorTheme
                    {
                        Index = 1, Id = "basement", Title = "BASEMENT",
                        Ambient = new Color(0.085f, 0.055f, 0.042f),
                        Fog = new Color(0.020f, 0.011f, 0.008f), FogStart = 5.5f, FogEnd = 27f,
                        TorchColor = new Color(1.00f, 0.60f, 0.30f), TorchIntensity = 3.0f,
                        Accent = Palette("#e0b04a"),
                        Ambience = "basement_ambience",
                        FloorProps = new[] { "poop_0", "poop_1", "poop_2", "rock_0", "rock_1", "rock_2", "bones_0" },
                        WallDecals = new[] { "web_0" },
                        WallVariantChance = 0.3f,
                    };
            }
        }

        // Aplică lumina și ceața pe toată scena.
        public void Apply()
        {
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = Ambient;
            // Ceață liniară: aproape de cameră nu există (eroii rămân clari), apoi holul se scufundă în beznă.
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = Fog;
            RenderSettings.fogStartDistance = FogStart;
            RenderSettings.fogEndDistance = FogEnd;
            Exploration.CameraRig.ExploreFog = new Vector2(FogStart, FogEnd);
            // Sprite-urile primesc mai mult decât pereții: eroii trebuie să rămână lizibili și în întuneric.
            LocalLight.Ambient = Ambient * 3f + new Color(0.12f, 0.1f, 0.1f);
            var rig = Exploration.CameraRig.Instance;
            if (rig != null && rig.Camera != null) rig.Camera.backgroundColor = Fog;
        }

        static Color Palette(string hex) => ColorUtility.TryParseHtmlString(hex, out var c) ? c : Color.white;
    }
}
