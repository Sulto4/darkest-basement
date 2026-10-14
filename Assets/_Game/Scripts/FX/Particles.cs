using DarkestBasement.Combat;
using UnityEngine;

namespace DarkestBasement.FX
{
    public enum ParticleKind { Blood, Tear, Poison, Heal, Dust, Spark, Smoke, Gold, Bone, Hex, Block, Bile, Poop, Ember }

    // Sistem de particule propriu, foarte simplu: fiecare particulă e un pătrățel (1–3 px de artă)
    // cu viteză, gravitație și frecare. Integrăm mișcarea pas cu pas (metoda lui Euler):
    //   v = v + g·dt ;  v = v·(1 − frecare·dt) ;  x = x + v·dt
    // Folosim timp scalat, deci particulele îngheață în hit-stop (sângele „atârnă” în aer o clipă).
    public static class Particles
    {
        struct P
        {
            public Transform T;
            public SpriteRenderer R;
            public Vector3 Vel;
            public float Age, Life, Gravity, Drag, Size;
            public Color C0, C1;
            public bool Sticks, Stuck, Alive;
        }

        const int Max = 600;
        static readonly P[] Pool = new P[Max];
        static int _next;
        static Transform _root;

        // Înălțimea podelei (y) pentru sângele care cade și rămâne pe jos.
        public static float FloorY;

        // Stil pe tip: (culoare 1, culoare 2, viteză, gravitație, frecare, viață, mărime px, rămâne pe jos)
        struct Style { public Color A, B; public float Speed, Gravity, Drag, Life, Size; public bool Sticks; public Sprite Sprite; }

        static Style Get(ParticleKind kind)
        {
            switch (kind)
            {
                case ParticleKind.Blood: return new Style { A = FxColors.Blood, B = FxColors.BloodDark, Speed = 4.2f, Gravity = -16f, Drag = 0.6f, Life = 1.6f, Size = 1.5f, Sticks = true, Sprite = PixelSprites.Pixel };
                case ParticleKind.Tear: return new Style { A = FxColors.TearLight, B = FxColors.TearHi, Speed = 3.2f, Gravity = -12f, Drag = 1f, Life = 0.55f, Size = 1.5f, Sprite = PixelSprites.Pixel };
                case ParticleKind.Poison: return new Style { A = FxColors.Poison, B = FxColors.PoisonDark, Speed = 0.9f, Gravity = 1.6f, Drag = 2f, Life = 0.9f, Size = 2f, Sprite = PixelSprites.Pixel };
                case ParticleKind.Heal: return new Style { A = FxColors.Poison, B = FxColors.White, Speed = 0.8f, Gravity = 2.2f, Drag = 2.5f, Life = 0.9f, Size = 1f, Sprite = PixelSprites.Plus };
                case ParticleKind.Dust: return new Style { A = FxColors.Dust, B = FxColors.A(FxColors.Dust, 0), Speed = 1.6f, Gravity = 0.4f, Drag = 3.5f, Life = 0.6f, Size = 3f, Sprite = PixelSprites.Pixel };
                case ParticleKind.Spark: return new Style { A = FxColors.White, B = FxColors.Stun, Speed = 6.5f, Gravity = -6f, Drag = 3f, Life = 0.28f, Size = 1f, Sprite = PixelSprites.Pixel };
                case ParticleKind.Smoke: return new Style { A = FxColors.Smoke, B = FxColors.A(FxColors.Smoke, 0), Speed = 1.2f, Gravity = 1.2f, Drag = 2.5f, Life = 0.9f, Size = 4f, Sprite = PixelSprites.Pixel };
                case ParticleKind.Gold: return new Style { A = FxColors.Gold, B = FxColors.White, Speed = 2.6f, Gravity = -2f, Drag = 2f, Life = 0.8f, Size = 1f, Sprite = PixelSprites.Plus };
                case ParticleKind.Bone: return new Style { A = FxColors.Bone, B = FxColors.BoneDark, Speed = 3.5f, Gravity = -14f, Drag = 0.8f, Life = 1.2f, Size = 2f, Sticks = true, Sprite = PixelSprites.Pixel };
                case ParticleKind.Hex: return new Style { A = FxColors.Purple, B = FxColors.Lilith, Speed = 1.4f, Gravity = 1.2f, Drag = 2.2f, Life = 0.8f, Size = 1.5f, Sprite = PixelSprites.Pixel };
                case ParticleKind.Block: return new Style { A = FxColors.Block, B = FxColors.White, Speed = 2.2f, Gravity = -3f, Drag = 2.5f, Life = 0.5f, Size = 1.5f, Sprite = PixelSprites.Pixel };
                case ParticleKind.Bile: return new Style { A = FxColors.Bile, B = FxColors.BileDark, Speed = 3.5f, Gravity = -14f, Drag = 0.8f, Life = 1.1f, Size = 1.5f, Sticks = true, Sprite = PixelSprites.Pixel };
                case ParticleKind.Poop: return new Style { A = FxColors.Poop, B = FxColors.PoopDark, Speed = 3.6f, Gravity = -15f, Drag = 0.8f, Life = 1.3f, Size = 2f, Sticks = true, Sprite = PixelSprites.Pixel };
                default: return new Style { A = FxColors.Mark, B = FxColors.Blood, Speed = 2f, Gravity = 1f, Drag = 2f, Life = 0.5f, Size = 1f, Sprite = PixelSprites.Pixel };
            }
        }

        // Explozie de particule. dir = direcția principală (de ex. dinspre atacator spre țintă);
        // spread = cât de mult se împrăștie (0 = toate pe direcție, 1 = în toate părțile).
        public static void Burst(Vector3 pos, ParticleKind kind, int count, Vector3 dir = default, float speed = 1f, float spread = 1f)
        {
            if (count <= 0) return;
            FxHost.Ensure();
            EnsureRoot();
            var st = Get(kind);
            Quaternion face = CombatCamera.Facing;
            Vector3 right = face * Vector3.right, up = Vector3.up, depth = face * Vector3.forward;
            Vector3 main = dir.sqrMagnitude > 1e-4f ? dir.normalized : Vector3.zero;
            for (int i = 0; i < count; i++)
            {
                ref P p = ref Pool[_next];
                _next = (_next + 1) % Max;
                if (p.T == null)
                {
                    var go = new GameObject("p");
                    go.transform.SetParent(_root, false);
                    p.T = go.transform;
                    p.R = go.AddComponent<SpriteRenderer>();
                }
                // Direcție aleatoare în planul ecranului (mișcarea spre cameră nu s-ar vedea).
                Vector2 rnd = Random.insideUnitCircle;
                Vector3 v = (main * (1f - spread * 0.5f) + (right * rnd.x + up * Mathf.Abs(rnd.y) * 1.3f) * spread).normalized;
                v += depth * Random.Range(-0.15f, 0.15f);
                p.Vel = v * st.Speed * speed * Random.Range(0.45f, 1.15f);
                p.Age = 0f;
                p.Life = st.Life * Random.Range(0.7f, 1.3f);
                p.Gravity = st.Gravity;
                p.Drag = st.Drag;
                p.Size = Mathf.Max(1f, Mathf.Round(st.Size * Random.Range(0.7f, 1.4f)));
                p.C0 = Color.Lerp(st.A, st.B, Random.value * 0.6f);
                p.C1 = st.B; p.C1.a = st.Sticks ? 1f : 0f;
                p.Sticks = st.Sticks;
                p.Stuck = false;
                p.Alive = true;
                p.R.sprite = st.Sprite;
                p.R.color = p.C0;
                p.R.sortingOrder = 40;
                p.T.position = pos + Random.insideUnitSphere * 0.06f;
                p.T.rotation = face;
                p.T.localScale = Vector3.one * p.Size;
                p.T.gameObject.SetActive(true);
            }
        }

        internal static void Tick()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            Quaternion face = CombatCamera.Facing;
            for (int i = 0; i < Max; i++)
            {
                ref P p = ref Pool[i];
                if (!p.Alive) continue;
                p.Age += dt;
                if (p.Age >= p.Life || p.T == null) { Kill(ref p); continue; }
                float k = p.Age / p.Life;
                if (!p.Stuck)
                {
                    p.Vel.y += p.Gravity * dt;
                    p.Vel *= Mathf.Max(0f, 1f - p.Drag * dt);
                    Vector3 pos = p.T.position + p.Vel * dt;
                    if (p.Sticks && pos.y <= FloorY + 0.01f && p.Vel.y < 0f)
                    {
                        // A atins podeaua: rămâne lipit și trăiește mai mult (pată mică de sânge).
                        pos.y = FloorY + 0.01f;
                        p.Stuck = true;
                        p.Life = p.Age + Random.Range(2.5f, 4.5f);
                        p.R.sortingOrder = -40;
                    }
                    p.T.position = pos;
                    p.T.rotation = face;
                }
                Color c = p.Stuck ? p.C0 : Color.Lerp(p.C0, p.C1, k);
                if (p.Stuck) c.a = Mathf.Clamp01((p.Life - p.Age) / 0.8f);
                p.R.color = c;
            }
        }

        static void Kill(ref P p)
        {
            p.Alive = false;
            if (p.T != null) p.T.gameObject.SetActive(false);
        }

        public static void Clear()
        {
            for (int i = 0; i < Max; i++) if (Pool[i].Alive) Kill(ref Pool[i]);
        }

        static void EnsureRoot()
        {
            if (_root != null) return;
            var go = new GameObject("[Particles]");
            Object.DontDestroyOnLoad(go);
            _root = go.transform;
        }
    }
}
