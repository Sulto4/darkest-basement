using System.Collections;
using DarkestBasement.Core;
using UnityEngine;

namespace DarkestBasement.Exploration
{
    // Mută echipa înainte (+Z) cât timp jucătorul ține W / D / → / click stânga.
    // Se oprește singură la TargetZ (următorul eveniment de pe hol sau ușa), cu frânare lină:
    // viteza maximă la o distanță d de țintă e √(2·frânare·d), adică exact cât să ajungă la zero fix pe țintă.
    public sealed class PartyWalker : MonoBehaviour
    {
        public PartyFormation Party;
        public float BaseSpeed = 2.4f;        // unități pe secundă
        public float SpeedMultiplier = 1f;    // ×3 pe holurile deja curățate
        public static float GlobalSpeedScale = 1f; // QA-ul poate grăbi mersul
        public float TargetZ;
        public bool InputEnabled;

        const float Acceleration = 7f;
        const float Braking = 9f;

        float _speed;
        bool _scripted, _wasMoving;

        public float Speed => _speed;
        public float Z => transform.localPosition.z;
        public bool Arrived => Z >= TargetZ - 0.001f;
        public bool IsMoving => _speed > 0.05f;

        public static PartyWalker Attach(PartyFormation party)
        {
            var w = party.gameObject.AddComponent<PartyWalker>();
            w.Party = party;
            return w;
        }

        public void Teleport(float z)
        {
            var p = transform.localPosition;
            transform.localPosition = new Vector3(p.x, p.y, z);
            _speed = 0f;
        }

        void Update()
        {
            if (_scripted) return;
            bool wantsToWalk = InputEnabled && !Arrived && GameInput.WalkHeld;
            float max = BaseSpeed * SpeedMultiplier * GlobalSpeedScale;
            Step(wantsToWalk ? max : 0f);
        }

        void Step(float desiredSpeed)
        {
            float dt = Time.deltaTime;
            float remaining = Mathf.Max(0f, TargetZ - Z);
            desiredSpeed = Mathf.Min(desiredSpeed, Mathf.Sqrt(2f * Braking * remaining) + 0.15f);
            float rate = desiredSpeed > _speed ? Acceleration : Braking;
            _speed = Mathf.MoveTowards(_speed, desiredSpeed, rate * dt * Mathf.Max(1f, SpeedMultiplier * GlobalSpeedScale));

            float z = Z + _speed * dt;
            if (z >= TargetZ && _speed > 0f) { z = TargetZ; _speed = 0f; }
            var p = transform.localPosition;
            transform.localPosition = new Vector3(p.x, p.y, z);

            bool moving = _speed > 0.05f;
            // Spunem formației doar când mersul nostru pornește, se oprește sau își schimbă viteza;
            // altfel am strica animațiile ei proprii (pașii spre scena de luptă).
            if (Party != null && (moving || _wasMoving)) Party.SetWalking(moving, _speed);
            _wasMoving = moving;
            var rig = CameraRig.Instance;
            // Camera se leagănă proporțional cu viteza; pe holurile parcurse rapid, mai puțin (ar da rău de mare).
            if (rig != null && Party != null)
                rig.SetWalkBob(moving ? Mathf.Clamp01(_speed / BaseSpeed) * (SpeedMultiplier > 1f ? 0.5f : 1f) : 0f, Party.WalkPhase);
        }

        // Mers automat (intrarea în cameră, trecerea prin ușă): același mers, fără input.
        public IEnumerator WalkTo(float z, float speed)
        {
            _scripted = true;
            TargetZ = z;
            while (!Arrived)
            {
                Step(speed * GlobalSpeedScale);
                yield return null;
            }
            Step(0f);
            _scripted = false;
            if (Party != null) Party.SetWalking(false, 0f);
            if (CameraRig.Instance != null) CameraRig.Instance.SetWalkBob(0f, 0f);
        }
    }
}
