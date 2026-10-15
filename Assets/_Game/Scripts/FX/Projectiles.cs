using System.Collections;
using DarkestBasement.Audio;
using DarkestBasement.Combat;
using UnityEngine;

namespace DarkestBasement.FX
{
    public enum ProjectileKind { Tear, BloodTear, IncubusTear, Knife, Bomb, Bone, BloodGlob, Poop, Ipecac, Bile, Hex }

    // Proiectilele: lacrimi, cuțite, bombe, oase. Fiecare zboară pe o parabolă:
    //   poziție(k) = lerp(start, țintă, k) + sus · înălțime · 4k(1−k)
    // 4k(1−k) e 0 la capete și 1 la mijloc, deci „înălțime” = cât de sus urcă arcul.
    // Corutina Fly se termină exact în momentul impactului; așa lovitura și numărul de damage sunt sincrone.
    public static class Projectiles
    {
        struct Spec { public Sprite Sprite; public float Speed, Arc, Spin; public bool FaceVelocity; public ParticleKind Trail; public float TrailChance; }

        static Spec Get(ProjectileKind kind, float size)
        {
            switch (kind)
            {
                case ProjectileKind.Tear: return new Spec { Sprite = PixelSprites.TearSprite(Mathf.RoundToInt(7 * size)), Speed = 11f, Arc = 0.35f, Trail = ParticleKind.Tear, TrailChance = 0.15f };
                case ProjectileKind.BloodTear: return new Spec { Sprite = PixelSprites.BloodTearSprite(Mathf.RoundToInt(7 * size)), Speed = 10f, Arc = 0.3f, Trail = ParticleKind.Blood, TrailChance = 0.25f };
                case ProjectileKind.IncubusTear: return new Spec { Sprite = PixelSprites.BloodTearSprite(Mathf.RoundToInt(6 * size)), Speed = 13f, Arc = 0.15f, Trail = ParticleKind.Blood, TrailChance = 0.2f };
                case ProjectileKind.Knife: return new Spec { Sprite = PixelSprites.Knife, Speed = 15f, Arc = 0.2f, Spin = -1440f };
                case ProjectileKind.Bomb: return new Spec { Sprite = PixelSprites.Bomb, Speed = 6.5f, Arc = 2.2f, Spin = -420f, Trail = ParticleKind.Spark, TrailChance = 0.5f };
                case ProjectileKind.Bone: return new Spec { Sprite = PixelSprites.BoneSprite, Speed = 9f, Arc = 0.9f, Spin = 900f };
                case ProjectileKind.BloodGlob: return new Spec { Sprite = PixelSprites.BloodTearSprite(Mathf.RoundToInt(9 * size)), Speed = 8f, Arc = 1.1f, Trail = ParticleKind.Blood, TrailChance = 0.4f };
                case ProjectileKind.Poop: return new Spec { Sprite = PixelSprites.PoopSprite(Mathf.RoundToInt(8 * size)), Speed = 8f, Arc = 1.3f, Spin = 360f, Trail = ParticleKind.Poop, TrailChance = 0.2f };
                case ProjectileKind.Ipecac: return new Spec { Sprite = PixelSprites.IpecacSprite(Mathf.RoundToInt(9 * size)), Speed = 7f, Arc = 1.8f, Trail = ParticleKind.Poison, TrailChance = 0.4f };
                case ProjectileKind.Bile: return new Spec { Sprite = PixelSprites.BileSprite(Mathf.RoundToInt(6 * size)), Speed = 9f, Arc = 0.6f, Trail = ParticleKind.Bile, TrailChance = 0.3f };
                default: return new Spec { Sprite = PixelSprites.Disc(5), Speed = 9f, Arc = 0.4f, Trail = ParticleKind.Hex, TrailChance = 0.6f };
            }
        }

        // duration < 0 = calculată din distanță și viteza proiectilului; tint colorează bilele de vrajă (Hex).
        public static IEnumerator Fly(ProjectileKind kind, Vector3 from, Vector3 to, float size = 1f, float arcScale = 1f, float duration = -1f, Color? tint = null)
        {
            var spec = Get(kind, size);
            var go = FxHost.Spawn("Projectile " + kind);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = spec.Sprite;
            sr.sortingOrder = 45;
            if (kind == ProjectileKind.Hex) sr.color = tint ?? (Color)FxColors.Purple;
            float distance = Vector3.Distance(from, to);
            if (duration <= 0f) duration = Mathf.Clamp(distance / spec.Speed, 0.12f, 0.7f);
            float arc = spec.Arc * arcScale * Mathf.Clamp(distance / 4f, 0.5f, 1.5f);
            float scale = kind == ProjectileKind.Knife || kind == ProjectileKind.Bomb || kind == ProjectileKind.Bone ? size : 1f;
            float t = 0f, spin = 0f;
            Vector3 prev = from;
            while (t < duration)
            {
                t += Time.deltaTime;
                if (go == null) yield break;
                float k = Mathf.Clamp01(t / duration);
                Vector3 pos = Vector3.Lerp(from, to, k) + Vector3.up * arc * 4f * k * (1f - k);
                Quaternion face = CombatCamera.Facing;
                float angle;
                if (spec.Spin != 0f) { spin += spec.Spin * Time.deltaTime; angle = spin; }
                else
                {
                    // Lacrima se întinde puțin pe direcția mișcării (squash & stretch în zbor).
                    Vector3 d = Quaternion.Inverse(face) * (pos - prev);
                    angle = d.sqrMagnitude > 1e-8f ? Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg : 0f;
                }
                if (kind == ProjectileKind.Knife && to.x < from.x) angle += 180f;
                go.transform.position = pos;
                go.transform.rotation = face * Quaternion.Euler(0f, 0f, angle);
                go.transform.localScale = spec.Spin != 0f ? Vector3.one * scale : new Vector3(1.15f, 0.88f, 1f) * scale;
                if (spec.TrailChance > 0f && Random.value < spec.TrailChance) Particles.Burst(pos, spec.Trail, 1, Vector3.zero, 0.2f, 1f);
                prev = pos;
                yield return null;
            }
            if (go != null) Object.Destroy(go);
        }

        // Ce se întâmplă la impact: stropi, explozie, sunet. Nu blochează.
        public static void Impact(ProjectileKind kind, Vector3 at, Vector3 dir, float size = 1f)
        {
            switch (kind)
            {
                case ProjectileKind.Tear:
                    PlayFrames(PixelSprites.Splash(FxColors.TearLight, "tear"), at, 22f, size);
                    Particles.Burst(at, ParticleKind.Tear, Mathf.RoundToInt(8 * size), -dir, 0.9f, 1f);
                    AudioManager.Play("tear_splash");
                    break;
                case ProjectileKind.BloodTear:
                case ProjectileKind.IncubusTear:
                case ProjectileKind.BloodGlob:
                    PlayFrames(PixelSprites.Splash(FxColors.Blood, "blood"), at, 22f, size);
                    Particles.Burst(at, ParticleKind.Blood, Mathf.RoundToInt(6 * size), -dir, 0.8f, 1f);
                    AudioManager.Play("tear_splash", 0.8f, 0.8f);
                    break;
                case ProjectileKind.Bomb:
                    Explosion(at, 1.1f * size, false);
                    break;
                case ProjectileKind.Ipecac:
                    Explosion(at, 0.9f * size, true);
                    break;
                case ProjectileKind.Poop:
                    Particles.Burst(at, ParticleKind.Poop, 12, -dir, 1f, 1f);
                    AudioManager.Play("tear_splash", 0.9f, 0.7f);
                    break;
                case ProjectileKind.Bile:
                    PlayFrames(PixelSprites.Splash(FxColors.Bile, "bile"), at, 22f, size);
                    Particles.Burst(at, ParticleKind.Bile, 8, -dir, 0.8f, 1f);
                    AudioManager.Play("tear_splash", 0.8f, 0.75f);
                    break;
                case ProjectileKind.Bone:
                    Particles.Burst(at, ParticleKind.Bone, 6, -dir, 0.8f, 1f);
                    break;
                case ProjectileKind.Hex:
                    Particles.Burst(at, ParticleKind.Hex, 10, Vector3.up, 0.8f, 1f);
                    break;
            }
        }

        // Explozia de bombă (sau de Ipecac, în verde): cadre + praf + fum + shake.
        public static void Explosion(Vector3 at, float scale, bool green)
        {
            var frames = PixelSprites.Explosion(40);
            PlayFrames(frames, at + Vector3.up * 0.3f * scale, 18f, scale, green ? new Color(0.55f, 1f, 0.45f, 1f) : Color.white);
            Particles.Burst(at + Vector3.up * 0.2f, ParticleKind.Smoke, 10, Vector3.up, 1f, 1f);
            Particles.Burst(at, green ? ParticleKind.Poison : ParticleKind.Spark, 14, Vector3.up, 1.2f, 1f);
            Particles.Burst(at, ParticleKind.Dust, 8, Vector3.up, 1f, 1f);
            AudioManager.Play("bomb_explode", green ? 0.7f : 1f, green ? 1.2f : 1f);
            ScreenShake.Add(green ? 0.25f : 0.4f);
            ScreenFlash.Flash(green ? new Color(0.6f, 1f, 0.4f) : new Color(1f, 0.85f, 0.5f), 0.12f, 0.1f);
        }

        // Rulează o animație din cadre într-un punct din lume și se distruge la final.
        public static void PlayFrames(Sprite[] frames, Vector3 at, float fps, float scale, Color? tint = null, int order = 49)
        {
            if (frames == null || frames.Length == 0) return;
            FxHost.Run(FramesRoutine(frames, at, fps, scale, tint ?? Color.white, order));
        }

        static IEnumerator FramesRoutine(Sprite[] frames, Vector3 at, float fps, float scale, Color tint, int order)
        {
            var go = FxHost.Spawn("FxFrames");
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sortingOrder = order;
            sr.color = tint;
            go.transform.position = at;
            go.transform.localScale = Vector3.one * scale;
            float t = 0f;
            int index = 0;
            while (index < frames.Length)
            {
                if (sr == null) yield break;
                sr.sprite = frames[index];
                go.transform.rotation = CombatCamera.Facing;
                yield return null;
                t += Time.deltaTime;
                index = Mathf.FloorToInt(t * fps);
            }
            if (go != null) Object.Destroy(go);
        }
    }
}
