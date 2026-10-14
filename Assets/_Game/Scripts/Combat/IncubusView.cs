using System.Collections;
using DarkestBasement.Audio;
using DarkestBasement.Core;
using DarkestBasement.FX;
using UnityEngine;

namespace DarkestBasement.Combat
{
    // Incubus, familiarul lui Lilith: plutește lângă ea și trage în locul ei (Lilith „nu atacă”, GDD §6).
    public sealed class IncubusView : MonoBehaviour
    {
        UnitView _owner;
        SpriteRenderer _sr;
        Sprite[] _idle, _attack;
        Vector3 _offset;        // deplasare temporară (zbor spre țintă)
        float _hold;
        bool _vanishing;
        float _alpha = 1f;

        // Locul lui obișnuit: deasupra umărului, puțin în fața lui Lilith.
        Vector3 Rest => _owner.Feet + Vector3.up * 1.3f + _owner.FrontDir * 0.5f;
        public Vector3 Position => transform.position;
        public Vector3 Muzzle => transform.position + _owner.FrontDir * 0.3f + Vector3.up * 0.3f;

        public static IncubusView Attach(UnitView lilith)
        {
            var go = new GameObject("Incubus");
            var v = go.AddComponent<IncubusView>();
            v._owner = lilith;
            v._idle = SpriteLibrary.TryFrames("Sprites/Familiars/incubus/idle") ?? new[] { PixelSprites.Placeholder(24, 24, true) };
            v._attack = SpriteLibrary.TryFrames("Sprites/Familiars/incubus/attack") ?? v._idle;
            v._sr = go.AddComponent<SpriteRenderer>();
            v._sr.sprite = v._idle[0];
            v._sr.sortingOrder = 14;
            go.transform.position = v.Rest;
            lilith.Incubus = v;
            return v;
        }

        // Zboară puțin spre țintă, trage și se întoarce. Corutina se termină la impact.
        public IEnumerator Shoot(Vector3 target, ProjectileKind kind, float size = 1f)
        {
            Vector3 toward = (target - Rest); toward.y = 0f;
            Vector3 dash = toward.normalized * 0.45f;
            yield return MoveOffset(_offset, dash, 0.07f, Ease.OutCubic);
            _hold = 0.25f;
            AudioManager.Play("incubus_shot");
            StartCoroutine(MoveOffset(_offset, dash - toward.normalized * 0.15f, 0.06f, Ease.OutQuad));
            yield return Projectiles.Fly(kind, Muzzle, target, size);
        }

        public IEnumerator Beam(Vector3 target, BeamStyle style)
        {
            _hold = 0.6f;
            yield return BeamFx.Charge(Muzzle, 0.22f, style);
            BeamFx.Fire(Muzzle, target, style);
        }

        public IEnumerator Return()
        {
            if (_offset.sqrMagnitude > 1e-6f) yield return MoveOffset(_offset, Vector3.zero, 0.2f, Ease.InOutQuad);
        }

        public void Vanish() { _vanishing = true; Particles.Burst(transform.position, ParticleKind.Smoke, 8, Vector3.up, 0.8f, 1f); }

        IEnumerator MoveOffset(Vector3 from, Vector3 to, float duration, Ease ease)
        {
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                _offset = Vector3.LerpUnclamped(from, to, Easing.Evaluate(ease, Mathf.Clamp01(t / duration)));
                yield return null;
            }
            _offset = to;
        }

        void LateUpdate()
        {
            if (_owner == null) { Destroy(gameObject); return; }
            // Plutire: un opt leneș (sinus pe Y, sinus de frecvență dublă pe X).
            float t = Time.time;
            Vector3 bob = Vector3.up * Mathf.Sin(t * 2.6f) * 0.08f + _owner.FrontDir * Mathf.Sin(t * 1.3f) * 0.05f;
            transform.position = Rest + _offset + bob;
            transform.rotation = CombatCamera.Billboard;
            _hold -= Time.deltaTime;
            _sr.sprite = _hold > 0f ? _attack[0] : _idle[(int)(t * 4f) % _idle.Length];
            if (_vanishing) { _alpha -= Time.deltaTime * 2f; if (_alpha <= 0f) { Destroy(gameObject); return; } }
            var c = Color.Lerp(LocalLight.Sample(transform.position), Color.white, 0.55f); c.a = _alpha * _owner.Alpha;
            _sr.color = c;
        }
    }
}
