using System.Collections.Generic;
using DarkestBasement.Audio;
using DarkestBasement.Combat;
using DarkestBasement.Core;
using DarkestBasement.Model.Core;
using UnityEngine;

namespace DarkestBasement.Exploration
{
    // Echipa văzută din spate, în formație 2×2 eșalonată: rangurile 1 și 2 în față, 3 și 4 în spate.
    // Fiecare erou are faza lui de mers, ca să nu pășească toți în același timp (ar arăta ca o paradă de roboți).
    // Eroii morți lipsesc. Lilith are lângă ea pe Incubus, care plutește și o urmează cu o mică întârziere.
    public sealed class PartyFormation : MonoBehaviour
    {
        public sealed class Member
        {
            public string HeroId;
            public int Rank;
            public Transform Root;      // poziția pe podea
            public Billboard Body;
            public MeshRenderer Shadow;
            public float Phase;         // decalajul pasului (0..1)
            public bool Floats;         // Azazel plutește: fără pași, doar o legănare
            public int LastFrame = -1;
            public Vector3 Offset;      // salturi, tremur (peste poziția de bază)
            public Sprite[] Idle, Walk, Side;
        }

        public readonly List<Member> Members = new List<Member>();

        // Mărimea unui ciclu de mers (doi pași), în unități: viteza / pas = cât de repede se schimbă cadrele.
        const float StrideLength = 1.15f;

        Billboard _incubus;
        Vector3 _incubusVelocity;
        Light _lantern;
        Material _shadowMaterial;
        bool _walking, _sideView, _visible = true;
        float _walkSpeed, _walkPhase;

        public float WalkPhase => _walkPhase;

        public static PartyFormation Create(Transform parent, RunState state)
        {
            var go = new GameObject("Party");
            go.transform.SetParent(parent, false);
            var party = go.AddComponent<PartyFormation>();
            party.Init();
            party.Rebuild(state);
            return party;
        }

        void Init()
        {
            _shadowMaterial = Materials.Create(MaterialKind.UnlitTransparent, ProceduralTextures.Shadow, new Color(0f, 0f, 0f, 0.6f));

            // "Felinarul" echipei: o lumină caldă și slabă care merge cu eroii, ca să rămână lizibili între torțe.
            var lantern = new GameObject("Lantern");
            lantern.transform.SetParent(transform, false);
            lantern.transform.localPosition = new Vector3(0f, 2.4f, -1.6f);
            _lantern = lantern.AddComponent<Light>();
            _lantern.type = LightType.Point;
            _lantern.color = new Color(1f, 0.72f, 0.48f);
            _lantern.range = 5.5f;
            _lantern.intensity = 1.15f;
            _lantern.shadows = LightShadows.None;
            LocalLight.Register(_lantern);
        }

        // Pozițiile din formație, relative la centrul echipei (rândul din față e la z = 0).
        public static Vector3 SlotFor(int rank)
        {
            switch (rank)
            {
                case 1: return new Vector3(-0.5f, 0f, 0.15f);
                case 2: return new Vector3(0.55f, 0f, -0.1f);
                case 3: return new Vector3(-0.55f, 0f, -1.25f);
                default: return new Vector3(0.5f, 0f, -1.5f);
            }
        }

        // Unde stă eroul cu rangul dat pe scena de luptă (contractul cu echipa C: rangul 1 lângă centru, pas de 1,6).
        public static Vector3 StagePosition(CombatStageInfo stage, int rank)
        {
            Vector3 right = stage.Right.sqrMagnitude > 0.0001f ? stage.Right.normalized : Vector3.forward;
            return stage.Center - right * (0.8f + 1.6f * (rank - 1));
        }

        public void Rebuild(RunState state)
        {
            foreach (var m in Members) if (m.Root != null) Destroy(m.Root.gameObject);
            Members.Clear();
            if (_incubus != null) { Destroy(_incubus.gameObject); _incubus = null; }

            var living = new List<PartyHero>();
            foreach (var hero in state.Party) if (hero.IsAlive) living.Add(hero);
            living.Sort((a, b) => a.Rank.CompareTo(b.Rank));

            foreach (var hero in living)
            {
                string id = hero.Def.Id;
                var root = new GameObject(id).transform;
                root.SetParent(transform, false);
                root.localPosition = SlotFor(hero.Rank);

                string folder = "Sprites/Heroes/" + id + "/";
                var m = new Member
                {
                    HeroId = id,
                    Rank = hero.Rank,
                    Root = root,
                    Phase = StablePhase(id),
                    Floats = id == "azazel",
                    Idle = SpriteLibrary.Frames(folder + "back_idle"),
                    Walk = SpriteLibrary.Frames(folder + "back_walk"),
                    Side = SpriteLibrary.Frames(folder + "side_idle"),
                };
                m.Body = Billboard.Create(root, "Body", m.Idle[0], Vector3.zero);
                m.Shadow = MeshKit.FlatDecal(root, "Shadow", _shadowMaterial, new Vector3(0f, 0.012f, 0f), new Vector2(0.95f, 0.5f), 0f);
                Members.Add(m);

                if (id == "lilith")
                {
                    _incubus = Billboard.Create(transform, "Incubus", SpriteLibrary.Get("Sprites/Familiars/incubus/idle_0"), root.localPosition + new Vector3(0.65f, 0.9f, 0.1f));
                    _incubus.Play(SpriteLibrary.Frames("Sprites/Familiars/incubus/idle"), 3f);
                }
            }
            SetVisible(_visible);
        }

        // Faza pasului dedusă din nume (aceeași la fiecare rulare, spre deosebire de GetHashCode).
        static float StablePhase(string id)
        {
            int sum = 0;
            foreach (char c in id) sum = sum * 31 + c;
            return Mathf.Repeat(sum * 0.618034f, 1f);
        }

        public Member Find(string heroId)
        {
            foreach (var m in Members) if (m.HeroId == heroId) return m;
            return null;
        }

        public Vector3 HeadOf(string heroId)
        {
            var m = Find(heroId);
            return m != null ? m.Root.position + Vector3.up * 1.55f : transform.position + Vector3.up * 1.5f;
        }

        public Vector3 Center => transform.position + new Vector3(0f, 0.8f, -0.6f);

        public void SetWalking(bool walking, float speed)
        {
            _walking = walking;
            _walkSpeed = speed;
        }

        public void SetVisible(bool visible)
        {
            _visible = visible;
            foreach (var m in Members)
            {
                m.Body.SetVisible(visible);
                m.Shadow.enabled = visible;
            }
            if (_incubus != null) _incubus.SetVisible(visible);
        }

        // ---- Trecerea în vederea laterală și înapoi ----

        // Eroii pleacă din formație spre locurile lor pe scena de luptă și se întorc cu fața spre inamici.
        public TweenHandle MoveToStage(CombatStageInfo stage, float duration)
        {
            var starts = new List<Vector3>();
            var ends = new List<Vector3>();
            foreach (var m in Members) { starts.Add(m.Root.position); ends.Add(StagePosition(stage, m.Rank)); }
            _walking = true;
            _walkSpeed = 2.2f;
            return Tween.Value(duration, k =>
            {
                for (int i = 0; i < Members.Count; i++) Members[i].Root.position = Vector3.Lerp(starts[i], ends[i], k);
                // Pe la o treime din drum camera e deja destul de lateral încât să-i vedem din profil.
                _sideView = k > 0.35f;
            }, Ease.InOutCubic, () => { _walking = false; _sideView = true; }, 0f, false, this);
        }

        // Pune eroii direct pe scenă, din profil (când revin după luptă).
        public void PlaceOnStage(CombatStageInfo stage)
        {
            foreach (var m in Members) m.Root.position = StagePosition(stage, m.Rank);
            _sideView = true;
            _walking = false;
        }

        public TweenHandle MoveToFormation(float duration)
        {
            var starts = new List<Vector3>();
            foreach (var m in Members) starts.Add(m.Root.localPosition);
            _walking = true;
            _walkSpeed = 2.2f;
            return Tween.Value(duration, k =>
            {
                for (int i = 0; i < Members.Count; i++) Members[i].Root.localPosition = Vector3.Lerp(starts[i], SlotFor(Members[i].Rank), k);
                _sideView = k < 0.5f;
            }, Ease.InOutCubic, () => { _walking = false; _sideView = false; }, 0f, false, this);
        }

        // ---- Reacții (capcane, curio) ----

        public void Hurt(string heroId)
        {
            var m = Find(heroId);
            if (m == null) return;
            m.Body.Flash(new Color(1f, 0.25f, 0.2f), 0.18f);
            Tween.Value(0.3f, k => m.Offset = new Vector3(Mathf.Sin(k * 50f) * 0.08f * (1f - k), 0f, 0f), Ease.Linear, () => m.Offset = Vector3.zero, 0f, false, this);
        }

        public void Heal(string heroId)
        {
            var m = Find(heroId);
            if (m != null) m.Body.Flash(new Color(0.55f, 1f, 0.45f), 0.3f);
        }

        // Eschivă: un salt mic și rapid.
        public void Hop(string heroId, float height = 0.55f)
        {
            var m = Find(heroId);
            if (m == null) return;
            Tween.Value(0.42f, k => m.Offset = new Vector3(0f, Mathf.Sin(k * Mathf.PI) * height, 0f), Ease.Linear, () => m.Offset = Vector3.zero, 0f, false, this);
        }

        // Eroul moare: clipește și se scufundă în podea, apoi dispare din formație.
        public void Die(string heroId)
        {
            var m = Find(heroId);
            if (m == null) return;
            m.Body.Flash(Color.white, 0.25f);
            Members.Remove(m);
            var body = m.Body;
            Tween.Value(0.7f, k =>
            {
                if (body == null) return;
                body.transform.localPosition = new Vector3(0f, -1.6f * k, 0f);
                body.transform.localScale = new Vector3(1f + 0.3f * k, 1f - 0.4f * k, 1f);
            }, Ease.InBack, () => { if (m.Root != null) Destroy(m.Root.gameObject); }, 0.15f, false, this);
            if (heroId == "lilith" && _incubus != null) { Destroy(_incubus.gameObject); _incubus = null; }
        }

        // Trapa: eroii sar în gaură unul după altul (arc în aer, apoi cad și se micșorează în beznă).
        // Întoarce cât durează tot saltul.
        public float JumpInto(Vector3 hole)
        {
            float stagger = 0.16f, jump = 0.55f;
            for (int i = 0; i < Members.Count; i++)
            {
                var m = Members[i];
                var root = m.Root;
                Vector3 from = root.position;
                Vector3 to = hole + new Vector3((i % 2 == 0 ? -0.25f : 0.25f), 0f, (i / 2) * 0.2f);
                Tween.Value(jump, k =>
                {
                    if (root == null) return;
                    // Până la mijloc urcă (arcul saltului), după margine cade în gaură.
                    float y = k < 0.6f ? Mathf.Sin(k / 0.6f * Mathf.PI) * 0.7f : -(k - 0.6f) / 0.4f * 2.2f;
                    root.position = Vector3.Lerp(from, to, Mathf.Min(1f, k / 0.6f)) + Vector3.up * y;
                    float s = k < 0.6f ? 1f : Mathf.Lerp(1f, 0.3f, (k - 0.6f) / 0.4f);
                    root.localScale = new Vector3(s, s, s);
                }, Ease.Linear, null, i * stagger, false, root);
            }
            if (_incubus != null) Tween.Move(_incubus.transform, hole + Vector3.down * 2f, 0.6f, Ease.InCubic, null, 0.3f);
            _walking = false;
            return Members.Count * stagger + jump;
        }

        // După ce modelul a re-numerotat rangurile (un erou a murit), ceilalți își iau locurile noi.
        public void SyncRanks(RunState state)
        {
            foreach (var m in Members)
                foreach (var hero in state.Party)
                    if (hero.Def.Id == m.HeroId) m.Rank = hero.Rank;
            if (!_sideView) MoveToFormation(0.5f);
        }

        // ---- Animație ----

        void Update()
        {
            float dt = Time.deltaTime;
            if (_walking) _walkPhase += dt * _walkSpeed / StrideLength;
            float t = Time.time;

            foreach (var m in Members)
            {
                float phase = Mathf.Repeat(_walkPhase + m.Phase, 1f);
                Sprite sprite;
                Vector3 bob = Vector3.zero;

                if (_sideView)
                {
                    sprite = m.Side[Mathf.FloorToInt(t * 2.5f + m.Phase * 2f) % m.Side.Length];
                    if (_walking) bob.y = Mathf.Abs(Mathf.Sin(phase * Mathf.PI * 2f)) * 0.06f;
                }
                else if (_walking)
                {
                    int frame = Mathf.FloorToInt(phase * m.Walk.Length) % m.Walk.Length;
                    sprite = m.Walk[frame];
                    bob.y = Mathf.Abs(Mathf.Sin(phase * Mathf.PI * 2f)) * 0.05f;
                    // Pasul se aude când piciorul atinge podeaua: cadrele 1 și 3 din ciclul de 4.
                    if (frame != m.LastFrame && (frame == 1 || frame == 3) && !m.Floats && _visible)
                        AudioManager.Play("footstep_stone", 0.45f, 0.9f + m.Phase * 0.2f);
                    m.LastFrame = frame;
                }
                else
                {
                    sprite = m.Idle[Mathf.FloorToInt(t * 2f + m.Phase * 2f) % m.Idle.Length];
                    m.LastFrame = -1;
                }

                if (m.Floats) bob.y = 0.16f + Mathf.Sin(t * 2.6f + m.Phase * 6.28f) * 0.06f;
                m.Body.SetSprite(sprite);
                m.Body.transform.localPosition = bob + m.Offset;
            }

            if (_incubus != null)
            {
                var lilith = Find("lilith");
                if (lilith != null)
                {
                    Vector3 target = lilith.Root.position + new Vector3(0.65f, 0.95f + Mathf.Sin(t * 3.1f) * 0.08f, 0.15f);
                    _incubus.transform.position = Vector3.SmoothDamp(_incubus.transform.position, target, ref _incubusVelocity, 0.25f);
                }
            }
        }

        void OnDestroy()
        {
            LocalLight.Unregister(_lantern);
            if (_shadowMaterial != null) Destroy(_shadowMaterial);
        }
    }
}
