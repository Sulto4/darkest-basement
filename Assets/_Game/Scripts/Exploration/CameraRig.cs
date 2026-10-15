using DarkestBasement.Combat;
using DarkestBasement.Core;
using DarkestBasement.UI;
using UnityEngine;

namespace DarkestBasement.Exploration
{
    // Camera lumii. Ierarhia are trei niveluri, fiecare cu treaba lui:
    //   CameraRig  — poza "de bază": urmărirea echipei, tranziția spre vederea laterală (o scrie doar clasa asta)
    //   └ ShakeRoot — tremurul ecranului (îl scrie ScreenShake, echipa C)
    //     └ Camera  — legănarea pașilor (head-bob) și punch-in-ul cinematic
    // Așa efectele se adună fără să se calce pe picioare.
    public sealed class CameraRig : MonoBehaviour
    {
        public enum Mode { Manual, Follow, Transition, Side }

        public static CameraRig Instance;
        public Camera Camera;
        public Transform ShakeRoot;

        public Mode CurrentMode { get; private set; } = Mode.Manual;

        // Explorare: în spatele echipei și mai sus, privind puțin în jos, spre holul din fața ei.
        // Destul de sus încât rândul din față să apară deasupra celui din spate (nu ascuns de el).
        public Vector3 FollowOffset = new Vector3(0f, 3.0f, -5.9f);
        public Vector3 LookOffset = new Vector3(0f, 0.6f, 3.2f);
        public float FollowFov = 50f;

        // Vederea laterală: pe planul scenei de luptă se văd exact 10 unități în lățime, adică 640 px / 10 = 64 px pe unitate:
        // fiecare pixel de artă (32 pe unitate) devine exact 2×2 pixeli în imagine. E cel mai mare zoom ÎNTREG la care încap
        // 4 eroi + 4 inamici pe ecran (la ×3 ar trebui ~1000 px lățime), deci sprite-urile rămân perfect pixelate.
        public const float SideFov = 40f;
        public const float SideViewWidth = 10f;
        // Centrul cadrului e la 1 unitate deasupra podelei: picioarele unităților ajung pe la o treime din înălțimea ecranului,
        // ca dedesubt să încapă HUD-ul lor (HP, statusuri) și mâna de cărți, iar deasupra boss-ii mari.
        public const float SideLookHeight = 1.0f;
        const float SidePitch = 6f; // grade în jos: se vede puțin din podea, ca scena să aibă adâncime
        public static float SideDistance => SideViewWidth * UIRoot.RefHeight / UIRoot.RefWidth * 0.5f / Mathf.Tan(SideFov * 0.5f * Mathf.Deg2Rad);
        public static float SideCameraHeight => SideLookHeight + SideDistance * Mathf.Tan(SidePitch * Mathf.Deg2Rad);

        // Ceața (liniară: start, end): aproape în explorare (holul se pierde în beznă), departe în vederea laterală,
        // unde camera stă la ~15 unități de scenă și altfel ar vedea lupta printr-un zid de ceață. Tranziția o schimbă lin.
        public static Vector2 ExploreFog = new Vector2(5.5f, 27f);
        public static readonly Vector2 SideFog = new Vector2(17f, 44f);

        // Meniul: plutire lentă a camerei în jurul unei poze fixe.
        public float ManualDrift;

        Transform _target;
        Vector3 _velocity;
        float _baseFov = 50f;
        CombatStageInfo _stage;
        bool _hasStage;
        TweenHandle _transition;
        Pose _manual;

        float _bobWeight, _bobTargetWeight, _bobPhase;

        bool _punching;
        Vector3 _punchFocus;
        float _punchAmount, _punchDuration, _punchTime;

        public static CameraRig Create(Transform parent)
        {
            var go = new GameObject("CameraRig");
            go.transform.SetParent(parent, false);
            var rig = go.AddComponent<CameraRig>();

            var shake = new GameObject("ShakeRoot");
            shake.transform.SetParent(go.transform, false);
            rig.ShakeRoot = shake.transform;

            var camGo = new GameObject("WorldCamera");
            camGo.transform.SetParent(shake.transform, false);
            rig.Camera = camGo.AddComponent<Camera>();
            rig.Camera.fieldOfView = rig.FollowFov;
            rig.Camera.nearClipPlane = 0.05f;
            rig.Camera.farClipPlane = 70f;
            rig.Camera.clearFlags = CameraClearFlags.SolidColor;
            rig.Camera.backgroundColor = Color.black;
            camGo.AddComponent<AudioListener>();

            Instance = rig;
            return rig;
        }

        // ---- Explorare ----

        public void Follow(Transform target)
        {
            _target = target;
            _transition?.Kill();
            CurrentMode = Mode.Follow;
        }

        // Sare direct în poziția de urmărire (după un fade, când nu vrem să vedem camera zburând).
        public void SnapToFollow(Transform target)
        {
            Follow(target);
            var pose = FollowPose();
            transform.SetPositionAndRotation(pose.position, pose.rotation);
            _velocity = Vector3.zero;
            _baseFov = FollowFov;
        }

        public Pose FollowPose()
        {
            if (_target == null) return new Pose(transform.position, transform.rotation);
            Vector3 pos = _target.position + _target.rotation * FollowOffset;
            Vector3 look = _target.position + _target.rotation * LookOffset;
            return new Pose(pos, Quaternion.LookRotation(look - pos, Vector3.up));
        }

        public void SetManualPose(Vector3 position, Quaternion rotation, float fov)
        {
            _transition?.Kill();
            CurrentMode = Mode.Manual;
            _manual = new Pose(position, rotation);
            _baseFov = fov;
            transform.SetPositionAndRotation(position, rotation);
        }

        // PartyWalker spune cât de tare merge echipa (0..1) și în ce fază a pasului e (1 ciclu = 2 pași).
        public void SetWalkBob(float weight, float phase)
        {
            _bobTargetWeight = weight;
            _bobPhase = phase;
        }

        // ---- Vederea laterală (lupta) ----

        // Unde stă camera ca scena să se vadă din lateral: eroii (-Right) în stânga ecranului, inamicii (+Right) în dreapta.
        public static Pose SidePose(CombatStageInfo stage)
        {
            Vector3 right = stage.Right.sqrMagnitude > 0.0001f ? stage.Right.normalized : Vector3.forward;
            // Cross(sus, dreapta) = direcția spre cameră: privind înapoi spre scenă, +Right iese în dreapta.
            Vector3 toCamera = Vector3.Cross(Vector3.up, right).normalized;
            Vector3 look = stage.Center + Vector3.up * SideLookHeight;
            Vector3 pos = stage.Center + toCamera * SideDistance + Vector3.up * SideCameraHeight;
            return new Pose(pos, Quaternion.LookRotation(look - pos, Vector3.up));
        }

        // Momentul-semnătură: camera iese din spatele echipei și se rotește pe un arc în jurul scenei,
        // ca o macara de film, până o vede din lateral. Poziția merge pe arc (unghi + rază), rotația prin slerp,
        // totul pe o curbă in-out: pornește încet, accelerează, frânează moale.
        public TweenHandle GoToSideView(CombatStageInfo stage, float duration = 1.2f)
        {
            _stage = stage;
            _hasStage = true;
            var from = new Pose(transform.position, transform.rotation);
            return Orbit(stage.Center, from, SidePose(stage), _baseFov, SideFov, duration, Mode.Side);
        }

        public TweenHandle ReturnToFollow(float duration = 1.0f)
        {
            var from = new Pose(transform.position, transform.rotation);
            var to = FollowPose();
            Vector3 pivot = _hasStage ? _stage.Center : (_target != null ? _target.position : transform.position + transform.forward * 5f);
            return Orbit(pivot, from, to, _baseFov, FollowFov, duration, Mode.Follow);
        }

        TweenHandle Orbit(Vector3 pivot, Pose from, Pose to, float fromFov, float toFov, float duration, Mode endMode)
        {
            _transition?.Kill();
            CurrentMode = Mode.Transition;

            Vector3 a = from.position - pivot, b = to.position - pivot;
            float angleA = Mathf.Atan2(a.x, a.z);
            float delta = Mathf.DeltaAngle(angleA * Mathf.Rad2Deg, Mathf.Atan2(b.x, b.z) * Mathf.Rad2Deg) * Mathf.Deg2Rad;
            float radiusA = new Vector2(a.x, a.z).magnitude, radiusB = new Vector2(b.x, b.z).magnitude;
            float rollSign = delta < 0f ? 1f : -1f;
            Mode end = endMode;
            Vector2 fogFrom = new Vector2(RenderSettings.fogStartDistance, RenderSettings.fogEndDistance);
            Vector2 fogTo = endMode == Mode.Side ? SideFog : ExploreFog;

            _transition = Tween.Value(duration, k =>
            {
                float angle = angleA + delta * k;
                float radius = Mathf.Lerp(radiusA, radiusB, k);
                // Arcul urcă puțin la mijloc: camera "respiră" ca o macara, nu alunecă pe o linie dreaptă.
                float height = Mathf.Lerp(a.y, b.y, k) + Mathf.Sin(k * Mathf.PI) * 0.9f;
                transform.position = pivot + new Vector3(Mathf.Sin(angle) * radius, height, Mathf.Cos(angle) * radius);
                // O înclinare mică (dutch angle) la mijlocul mișcării, pentru senzația de film.
                float roll = Mathf.Sin(k * Mathf.PI) * 2.5f * rollSign;
                transform.rotation = Quaternion.Slerp(from.rotation, to.rotation, k) * Quaternion.Euler(0f, 0f, roll);
                _baseFov = Mathf.Lerp(fromFov, toFov, k);
                Vector2 fog = Vector2.Lerp(fogFrom, fogTo, k);
                RenderSettings.fogStartDistance = fog.x;
                RenderSettings.fogEndDistance = fog.y;
            }, Ease.InOutCubic, () =>
            {
                RenderSettings.fogStartDistance = fogTo.x;
                RenderSettings.fogEndDistance = fogTo.y;
                transform.SetPositionAndRotation(to.position, to.rotation);
                _baseFov = toFov;
                _velocity = Vector3.zero;
                CurrentMode = end;
                if (end == Mode.Follow) _hasStage = false;
            }, 0f, false, this);
            return _transition;
        }

        // Zoom cinematic scurt spre un punct (atacator + țintă), apoi revine singur.
        public void PunchIn(Vector3 worldFocus, float amount = 0.12f, float duration = 0.35f)
        {
            _punching = true;
            _punchFocus = worldFocus;
            _punchAmount = Mathf.Clamp(amount, 0f, 0.5f);
            _punchDuration = Mathf.Max(0.05f, duration);
            _punchTime = 0f;
        }

        // ---- Conversii ----

        // Poziția în UIRoot.Root: (0,0) = stânga-jos, (640,360) = dreapta-sus. Merge direct ca anchoredPosition
        // pentru un element creat cu UiKit.Rect într-un strat UI (World, Hud...).
        public Vector2 WorldToUI(Vector3 worldPos) => UIRoot.Instance.WorldToUI(Camera, worldPos);

        // Raza din cameră prin punctul de sub mouse (pentru click pe obiecte 3D).
        public Ray ScreenToWorldRay(Vector2 screenPos)
        {
            Vector2 ui = UIRoot.Instance.ScreenToUI(screenPos);
            var p = new Vector3(ui.x * Camera.pixelWidth / UIRoot.RefWidth, ui.y * Camera.pixelHeight / UIRoot.RefHeight, 0f);
            return Camera.ScreenPointToRay(p);
        }

        // ---- Update ----

        void LateUpdate()
        {
            float dt = Time.deltaTime;

            if (CurrentMode == Mode.Follow && _target != null)
            {
                var pose = FollowPose();
                transform.position = Vector3.SmoothDamp(transform.position, pose.position, ref _velocity, 0.22f);
                transform.rotation = Quaternion.Slerp(transform.rotation, pose.rotation, 1f - Mathf.Exp(-9f * dt));
                _baseFov = Mathf.Lerp(_baseFov, FollowFov, 1f - Mathf.Exp(-4f * dt));
            }
            else if (CurrentMode == Mode.Manual && ManualDrift > 0f)
            {
                float t = Time.time;
                Vector3 sway = new Vector3(Mathf.Sin(t * 0.21f) * 0.22f, Mathf.Sin(t * 0.33f) * 0.07f, Mathf.Sin(t * 0.17f) * 0.15f) * ManualDrift;
                Quaternion yaw = Quaternion.Euler(Mathf.Sin(t * 0.27f) * 0.6f * ManualDrift, Mathf.Sin(t * 0.19f) * 1.4f * ManualDrift, 0f);
                transform.SetPositionAndRotation(_manual.position + _manual.rotation * sway, _manual.rotation * yaw);
            }

            // Legănarea pașilor: sus-jos de două ori pe ciclu (un pas, alt pas), stânga-dreapta o dată.
            _bobWeight = Mathf.MoveTowards(_bobWeight, _bobTargetWeight, dt * 3f);
            float cycle = _bobPhase * Mathf.PI * 2f;
            Vector3 bob = new Vector3(Mathf.Sin(cycle) * 0.025f, Mathf.Abs(Mathf.Sin(cycle)) * 0.05f, 0f) * _bobWeight;
            Quaternion bobRoll = Quaternion.Euler(0f, 0f, Mathf.Sin(cycle) * 0.35f * _bobWeight);

            // Punch-in: urcă repede (30% din timp), coboară lin (70%).
            float punch = 0f;
            if (_punching)
            {
                _punchTime += dt;
                float t = _punchTime / _punchDuration;
                punch = t < 0.3f ? Easing.Evaluate(Ease.OutCubic, t / 0.3f) : 1f - Easing.Evaluate(Ease.InOutSine, (t - 0.3f) / 0.7f);
                if (t >= 1f) { _punching = false; punch = 0f; }
            }

            Quaternion punchRot = Quaternion.identity;
            if (punch > 0f)
            {
                Vector3 local = ShakeRoot.InverseTransformDirection(_punchFocus - ShakeRoot.position);
                if (local.sqrMagnitude > 0.0001f)
                    punchRot = Quaternion.Slerp(Quaternion.identity, Quaternion.FromToRotation(Vector3.forward, local.normalized), 0.45f * punch);
            }

            Camera.transform.localPosition = bob;
            Camera.transform.localRotation = punchRot * bobRoll;
            Camera.fieldOfView = _baseFov * (1f - _punchAmount * punch);
        }
    }
}
