using DarkestBasement.FX;
using UnityEngine;

namespace DarkestBasement.Combat
{
    // Cum „ajunge” o acțiune la țintă.
    public enum Delivery
    {
        Melee,      // atacatorul sare la țintă (lunge)
        Projectile, // ceva zboară spre fiecare țintă, la fiecare lovitură (lacrimi, cuțite)
        Volley,     // un singur proiectil spre grup (Bomb Toss), apoi lovesc toți
        Beam,       // rază (Brimstone); o singură dată pentru toate țintele
        Stomp,      // piciorul lui Mom coboară de sus
        Hand,       // mâna lui Mom intră din dreapta
        Leap,       // săritură în arc (Hopper, Leaper, Monstro)
        Rain,       // picături din tavan pe fiecare țintă (Bile Rain)
        Spray,      // jet de sânge spre toată echipa (Blood Spray)
        Cast,       // vrajă: o bilă colorată zboară spre fiecare țintă (vindecări, blesteme)
        Self,       // efect asupra propriei persoane (block, strength)
        Summon,     // invocare (efectele vin ca evenimente Summoned)
        Eye,        // ochiul lui Mom apare în ușă
        None,
    }

    // Stilul vizual și sonor al unei cărți sau abilități de inamic.
    public sealed class ActionStyle
    {
        public Delivery Delivery = Delivery.Melee;
        public ProjectileKind Projectile = ProjectileKind.Tear;
        public float ProjectileSize = 1f;
        public bool FromIncubus;                 // la Lilith trage familiarul
        public BeamStyle Beam = BeamStyle.Brimstone;
        public bool MiniBeam;                    // Brimstone Swipe: o rază scurtă la impact
        public string LaunchSfx;                 // la lansare / lovitură
        public string Voice;                     // sunetul atacatorului (mârâit, bâzâit)
        public float Shake;                      // trauma în plus la primul impact
        public bool PunchIn;                     // zoom cinematic pe primul impact
        public bool Big;                         // atac greu: hit-stop și recul mai mari
        public ParticleKind CastParticles = ParticleKind.Spark;
        public Color CastColor = Color.white;
        public float LeapHeight = 1.4f;
        public bool Fallback;                    // stil generic: id-ul nu e în catalog (SelfTest îl raportează)
    }

    // Catalogul: id-ul cărții sau al abilității → stil. Un singur loc de reglat pentru „cum se simte” fiecare atac.
    public static class AbilityFx
    {
        static readonly Color Red = new Color(1f, 0.35f, 0.3f), Blue = new Color(0.45f, 0.7f, 1f), Green = new Color(0.5f, 1f, 0.4f);
        static readonly Color Gold = new Color(1f, 0.85f, 0.35f), Purple = new Color(0.75f, 0.5f, 1f), White = new Color(1f, 0.97f, 0.9f);

        public static ActionStyle ForCard(string cardId)
        {
            switch (cardId)
            {
                // ---- Azazel: Brimstone de aproape ----
                case "brimstone_swipe": return new ActionStyle { Delivery = Delivery.Melee, MiniBeam = true, LaunchSfx = "punch" };
                case "blood_beam": return new ActionStyle { Delivery = Delivery.Beam, Beam = BeamStyle.Brimstone, PunchIn = true, Shake = 0.25f };
                case "charged_brimstone": return new ActionStyle { Delivery = Delivery.Beam, Beam = BeamStyle.BrimstoneHuge, PunchIn = true, Shake = 0.45f, Big = true };
                case "maw_of_the_void": return new ActionStyle { Delivery = Delivery.Beam, Beam = BeamStyle.Void, PunchIn = true, Shake = 0.4f, Big = true };
                case "demon_wings": return Self(ParticleKind.Block, Blue);
                case "goat_head": return Self(ParticleKind.Block, Blue);
                case "blood_bag": return Self(ParticleKind.Heal, Green);
                case "pentagram": return Self(ParticleKind.Ember, Red);
                case "ceremonial_robes": return Self(ParticleKind.Block, Blue);
                case "abaddon": return Self(ParticleKind.Smoke, Red);

                // ---- Cain: noroc, pumni, bombe ----
                case "lucky_punch": return new ActionStyle { Delivery = Delivery.Melee, LaunchSfx = "punch" };
                case "crickets_head": return new ActionStyle { Delivery = Delivery.Melee, LaunchSfx = "punch", Big = true, Shake = 0.15f };
                case "midas_touch": return new ActionStyle { Delivery = Delivery.Melee, LaunchSfx = "punch", Big = true, CastParticles = ParticleKind.Gold };
                case "bomb_toss": return new ActionStyle { Delivery = Delivery.Volley, Projectile = ProjectileKind.Bomb, Shake = 0.2f };
                case "dr_fetus": return new ActionStyle { Delivery = Delivery.Projectile, Projectile = ProjectileKind.Bomb, ProjectileSize = 0.9f };
                case "the_poop": return new ActionStyle { Delivery = Delivery.Projectile, Projectile = ProjectileKind.Poop, LaunchSfx = "knife_throw" };
                case "moms_knife": return new ActionStyle { Delivery = Delivery.Projectile, Projectile = ProjectileKind.Knife, LaunchSfx = "knife_throw", Shake = 0.1f };
                case "lucky_foot": return Self(ParticleKind.Gold, Gold);
                case "lucky_penny": return Self(ParticleKind.Gold, Gold);
                case "dice_shard": return Self(ParticleKind.Spark, Gold);

                // ---- Isaac: lacrimi ----
                case "tear": return Tears(1f);
                case "the_sad_onion": return Tears(1.15f);
                case "spoon_bender": return Tears(1f);
                case "20_20": return Tears(0.9f);
                case "polyphemus": return new ActionStyle { Delivery = Delivery.Projectile, Projectile = ProjectileKind.Tear, ProjectileSize = 2.4f, LaunchSfx = "tear_shot", PunchIn = true, Big = true, Shake = 0.3f };
                case "ipecac": return new ActionStyle { Delivery = Delivery.Projectile, Projectile = ProjectileKind.Ipecac, LaunchSfx = "tear_shot", Shake = 0.15f };
                case "yum_heart": return Cast(ParticleKind.Heal, Green);
                case "book_of_belial": return Cast(ParticleKind.Ember, Red);
                case "holy_mantle": return Cast(ParticleKind.Block, White);
                case "the_d6": return Self(ParticleKind.Spark, White);

                // ---- Lilith: trage Incubus ----
                case "incubus_shot": return Incubus(1f);
                case "box_of_friends": return Incubus(0.85f);
                case "multidimensional_baby": return Incubus(0.9f);
                case "twisted_pair": return Incubus(1f);
                case "lil_brimstone": return new ActionStyle { Delivery = Delivery.Beam, Beam = BeamStyle.Lil, FromIncubus = true, Shake = 0.15f };
                case "cursed_eye": return Cast(ParticleKind.Hex, Purple);
                case "moms_eyeshadow": return Cast(ParticleKind.Hex, new Color(1f, 0.55f, 0.8f));
                case "cambion_shield": return Cast(ParticleKind.Block, Purple);
                case "succubus": return Cast(ParticleKind.Ember, new Color(1f, 0.45f, 0.7f));
                case "guardian_angel": return Cast(ParticleKind.Heal, White);
                default: return new ActionStyle { Delivery = Delivery.Melee, LaunchSfx = "punch", Fallback = true };
            }
        }

        public static ActionStyle ForEnemyAbility(string abilityId)
        {
            switch (abilityId)
            {
                case "buzz": return new ActionStyle { Delivery = Delivery.Melee, Voice = "fly_buzz" };
                case "grab": return new ActionStyle { Delivery = Delivery.Melee, Voice = "gaper_groan" };
                case "bump": return new ActionStyle { Delivery = Delivery.Melee, Voice = "gaper_groan" };
                case "slam": return new ActionStyle { Delivery = Delivery.Melee, Voice = "gaper_groan", Big = true, Shake = 0.15f };
                case "charge": return new ActionStyle { Delivery = Delivery.Melee, Voice = "monster_roar", Big = true, Shake = 0.2f };
                case "bite": return new ActionStyle { Delivery = Delivery.Melee };
                case "lance": return new ActionStyle { Delivery = Delivery.Melee, LaunchSfx = "knife_throw" };
                case "spit": return new ActionStyle { Delivery = Delivery.Projectile, Projectile = ProjectileKind.BloodGlob, ProjectileSize = 0.75f, Voice = "fly_buzz" };
                case "blood_shot": return new ActionStyle { Delivery = Delivery.Projectile, Projectile = ProjectileKind.BloodTear, LaunchSfx = "tear_shot" };
                case "clot_burst": return new ActionStyle { Delivery = Delivery.Projectile, Projectile = ProjectileKind.BloodGlob, ProjectileSize = 0.8f, Voice = "gaper_groan" };
                case "gore_spit": return new ActionStyle { Delivery = Delivery.Projectile, Projectile = ProjectileKind.BloodGlob, Voice = "gaper_groan" };
                case "bone_throw": return new ActionStyle { Delivery = Delivery.Projectile, Projectile = ProjectileKind.Bone, LaunchSfx = "knife_throw" };
                case "triple_shot": return new ActionStyle { Delivery = Delivery.Projectile, Projectile = ProjectileKind.BloodTear, ProjectileSize = 0.85f, LaunchSfx = "tear_shot" };
                case "hop": return new ActionStyle { Delivery = Delivery.Leap, LeapHeight = 1.6f };
                case "leap": return new ActionStyle { Delivery = Delivery.Leap, LeapHeight = 2.2f, Big = true };
                case "shield_up": return Self(ParticleKind.Block, Blue);
                case "closed": return new ActionStyle { Delivery = Delivery.Self, CastParticles = ParticleKind.Bone, CastColor = White };
                case "taunt": return new ActionStyle { Delivery = Delivery.Self, CastParticles = ParticleKind.Blood, CastColor = Red };
                case "reposition": return new ActionStyle { Delivery = Delivery.None };

                // ---- Boss-ii ----
                case "big_leap": return new ActionStyle { Delivery = Delivery.Leap, LeapHeight = 3.2f, Big = true, PunchIn = true, Shake = 0.35f, Voice = "monster_roar" };
                case "blood_spray": return new ActionStyle { Delivery = Delivery.Spray, Voice = "monster_roar", Shake = 0.15f };
                case "spit_glob": return new ActionStyle { Delivery = Delivery.Projectile, Projectile = ProjectileKind.BloodGlob, ProjectileSize = 1.5f, Voice = "monster_roar", PunchIn = true };
                case "spawn": return new ActionStyle { Delivery = Delivery.Summon, Voice = "monster_roar" };
                case "bile_rain": return new ActionStyle { Delivery = Delivery.Rain, Projectile = ProjectileKind.Bile, Voice = "monster_roar" };
                case "mouth_beam": return new ActionStyle { Delivery = Delivery.Beam, Beam = BeamStyle.Bile, PunchIn = true, Shake = 0.3f, Big = true, Voice = "monster_roar" };
                case "moms_foot": return new ActionStyle { Delivery = Delivery.Stomp, PunchIn = true, Big = true, Shake = 0.3f };
                case "moms_hand": return new ActionStyle { Delivery = Delivery.Hand, PunchIn = true, Big = true };
                case "moms_eye": return new ActionStyle { Delivery = Delivery.Eye, CastParticles = ParticleKind.Hex, CastColor = Purple };
                case "door_summon": return new ActionStyle { Delivery = Delivery.Eye, CastParticles = ParticleKind.Smoke, CastColor = White, Voice = "monster_roar" };
                default: return new ActionStyle { Delivery = Delivery.Melee, Fallback = true };
            }
        }

        static ActionStyle Self(ParticleKind p, Color c) => new ActionStyle { Delivery = Delivery.Self, CastParticles = p, CastColor = c };
        static ActionStyle Cast(ParticleKind p, Color c) => new ActionStyle { Delivery = Delivery.Cast, CastParticles = p, CastColor = c };
        static ActionStyle Tears(float size) => new ActionStyle { Delivery = Delivery.Projectile, Projectile = ProjectileKind.Tear, ProjectileSize = size, LaunchSfx = "tear_shot" };
        static ActionStyle Incubus(float size) => new ActionStyle { Delivery = Delivery.Projectile, Projectile = ProjectileKind.IncubusTear, ProjectileSize = size, FromIncubus = true };
    }

    // Geometria scenei: unde stă fiecare rang.
    public static class CombatStage
    {
        public const float CenterGap = 0.8f;     // rangul 1 al fiecărei părți, față de centru (= PartyFormation.StagePosition)
        public const float RankSpacing = 1.6f;   // distanța dintre ranguri

        public static Vector3 Right(CombatStageInfo s) => s.Right.sqrMagnitude > 1e-4f ? s.Right.normalized : Vector3.right;

        // Direcția „spre fundal” (dinspre cameră spre perete).
        public static Vector3 Depth(CombatStageInfo s) => Vector3.Cross(Right(s), Vector3.up);

        // Un punct pe linia scenei, la distanța `d` de centru, pe partea eroilor (stânga) sau a inamicilor (dreapta).
        public static Vector3 Point(CombatStageInfo s, bool hero, float d) => s.Center + Right(s) * (hero ? -d : d);
    }
}
