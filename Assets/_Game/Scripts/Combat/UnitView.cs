using System.Collections;
using DarkestBasement.Core;
using DarkestBasement.FX;
using DarkestBasement.UI;
using UnityEngine;

namespace DarkestBasement.Combat
{
    // O unitate pe scena de luptă: sprite billboard, cadre, respirație, clipire, lunge, squash & stretch, moarte.
    // Nu știe nimic despre reguli: CombatController îi spune ce să joace, pe baza evenimentelor din model.
    // Metodele care întorc IEnumerator sunt „bătăi” de animație; controller-ul le așteaptă cu yield return.
    public sealed class UnitView : MonoBehaviour
    {
        public string Id { get; private set; }
        public string DefId { get; private set; }
        public string DisplayName { get; private set; }
        public bool IsHero { get; private set; }
        public bool IsBoss { get; private set; }
        public UnitFrames Frames { get; private set; }
        public bool Dead { get; private set; }
        public IncubusView Incubus { get; set; }

        // Poziția slotului (picioarele) și direcția spre adversari.
        public Vector3 Home { get; set; }
        public Vector3 FrontDir { get; private set; }
        public Vector3 Offset => _offset;

        Transform _visual;
        SpriteRenderer _body, _flash, _shadow, _ring, _ext;
        readonly SpriteRenderer[] _outline = new SpriteRenderer[4];
        Sprite[] _loop;
        float _loopFps, _loopTime;
        Sprite _held;
        float _heldUntil;
        Vector3 _offset;
        Vector2 _squash = Vector2.one;
        float _phase;
        float _flashTime, _flashDuration;
        Color _flashColor = Color.white;
        bool _targetable, _hovered, _active;
        Color _glow = Color.white;
        float _alpha = 1f;
        int _order = 10;
        float _breath = 1f;
        Color _tint = Color.white;
        bool _fury;

        // Mom în faza 2: corpul pulsează roșu (furia se vede, nu doar se citește în banner).
        public void SetFury(bool on) { _fury = on; }

        // ---------- creare ----------

        public static UnitView Create(string id, string defId, string name, bool hero, bool boss, UnitFrames frames, Vector3 home, Vector3 stageRight)
        {
            var go = new GameObject((hero ? "Hero " : "Enemy ") + id);
            var v = go.AddComponent<UnitView>();
            v.Id = id; v.DefId = defId; v.DisplayName = name; v.IsHero = hero; v.IsBoss = boss; v.Frames = frames;
            v.Home = home;
            v.FrontDir = (hero ? 1f : -1f) * (stageRight.sqrMagnitude > 1e-4f ? stageRight.normalized : Vector3.right);
            v._order = boss ? 6 : 10;
            v.Build();
            return v;
        }

        void Build()
        {
            transform.position = Home;
            // Fază diferită pentru fiecare personaj: dacă ar respira toți deodată, ar arăta ca un metronom.
            _phase = (Mathf.Abs(Id.GetHashCode()) % 1000) / 1000f * Mathf.PI * 2f;

            _shadow = NewRenderer("Shadow", transform, PixelSprites.Shadow(Mathf.Clamp(Mathf.RoundToInt(Frames.Width * 18f), 16, 80), 6), _order - 5);
            _shadow.transform.localPosition = new Vector3(0f, 0.01f, 0.04f);
            _ring = NewRenderer("ActiveRing", transform, PixelSprites.RingEllipse(Mathf.Clamp(Mathf.RoundToInt(Frames.Width * 22f), 24, 90), 8), _order - 4);
            _ring.transform.localPosition = new Vector3(0f, 0.02f, 0.03f);
            _ring.enabled = false;

            _visual = new GameObject("Visual").transform;
            _visual.SetParent(transform, false);
            // Contur de 1 px de artă: 4 copii ale siluetei, decalate cu 1/32 unități (sus, jos, stânga, dreapta).
            float px = 1f / PixelSprites.Ppu;
            Vector3[] offsets = { new Vector3(px, 0, 0), new Vector3(-px, 0, 0), new Vector3(0, px, 0), new Vector3(0, -px, 0) };
            for (int i = 0; i < 4; i++)
            {
                _outline[i] = NewRenderer("Outline", _visual, null, _order - 1);
                _outline[i].transform.localPosition = offsets[i] + new Vector3(0f, 0f, 0.002f);
                _outline[i].enabled = false;
            }
            _body = NewRenderer("Body", _visual, Frames.Idle[0], _order);
            _flash = NewRenderer("Flash", _visual, null, _order + 1);
            _flash.transform.localPosition = new Vector3(0f, 0f, -0.002f);
            _flash.enabled = false;
            if (Frames.ExtendTop)
            {
                _ext = NewRenderer("TopExtension", _visual, null, _order);
            }
            PlayLoop(Frames.Idle, 2.2f);
            // Siluetele albe se calculează acum (citire înapoi de pe GPU), nu în mijlocul unei lovituri.
            foreach (var set in new[] { Frames.Idle, Frames.Attack, Frames.Hurt, Frames.Dead, Frames.Extra("open"), Frames.Extra("closed"), Frames.Extra("regen") })
                if (set != null) foreach (var f in set) SpriteSilhouette.Of(f);
        }

        static SpriteRenderer NewRenderer(string name, Transform parent, Sprite sprite, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = order;
            return sr;
        }

        // ---------- poziții utile ----------

        public Vector3 Feet => Home + _offset;
        Sprite Current => _held != null ? _held : (_loop != null && _loop.Length > 0 ? _loop[0] : Frames.Idle[0]);

        // Partea de sus a pixelilor vizibili (nu a pânzei, care are spațiu gol deasupra).
        public float TopHeight
        {
            get
            {
                var s = Frames.Idle[0];
                var info = SpriteSilhouette.Of(s);
                float top = info != null ? info.Opaque.yMax : s.rect.height;
                return (top - s.pivot.y) / s.pixelsPerUnit;
            }
        }

        public float HalfWidth
        {
            get
            {
                var s = Frames.Idle[0];
                var info = SpriteSilhouette.Of(s);
                float w = info != null ? info.Opaque.width : s.rect.width;
                return w * 0.5f / s.pixelsPerUnit;
            }
        }

        public Vector3 Center => Feet + Vector3.up * TopHeight * 0.5f;
        public Vector3 Head => Feet + Vector3.up * TopHeight;
        // De unde pleacă lacrimile: la nivelul ochilor, puțin în față. Gurdy și Monstro trag din gură, mai jos.
        public Vector3 Muzzle => Feet + Vector3.up * TopHeight * MuzzleHeight + FrontDir * HalfWidth * 0.6f;
        float MuzzleHeight => DefId == "gurdy" || DefId == "monstro" ? 0.45f : 0.62f;

        // Dreptunghiul unității în UI (pentru hover și drop de cărți).
        public Rect ScreenRectUI()
        {
            Vector2 a = CombatCamera.WorldToUI(Feet - FrontDir * HalfWidth);
            Vector2 b = CombatCamera.WorldToUI(Feet + FrontDir * HalfWidth + Vector3.up * TopHeight);
            var r = Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
            // O margine de câțiva pixeli: țintele mici (Fly) rămân ușor de nimerit.
            float pad = Mathf.Max(0f, 20f - r.width) * 0.5f;
            r.xMin -= pad + 3f; r.xMax += pad + 3f; r.yMax += 4f; r.yMin -= 6f;
            return r;
        }

        // ---------- cadre ----------

        public void PlayLoop(Sprite[] frames, float fps)
        {
            if (frames == null || frames.Length == 0) return;
            _loop = frames; _loopFps = fps;
        }

        // Ține un cadru anume pe ecran câteva secunde (lovitură, atac), apoi revine la bucla de idle.
        public void Hold(Sprite frame, float seconds)
        {
            if (frame == null) return;
            _held = frame;
            _heldUntil = Time.time + seconds;
        }

        public void ReleaseHold() { _held = null; }

        public Sprite[] IdleSet()
        {
            if (DefId == "host")
            {
                var open = Frames.Extra("open");
                var closed = Frames.Extra("closed");
                if (_hostOpen && open != null) return open;
                if (!_hostOpen && closed != null) return closed;
            }
            return Frames.Idle;
        }

        bool _hostOpen;
        public void SetHostOpen(bool open)
        {
            if (_hostOpen == open && _loop == IdleSet()) return;
            _hostOpen = open;
            PlayLoop(IdleSet(), 2.2f);
            Squash(1.15f, 0.85f);
        }

        // ---------- efecte instant ----------

        // Albul clipirii e puțin sub 1: post-procesarea lui W (expunere +0,35) l-ar împinge peste pragul de Bloom,
        // iar silueta albă a unui boss uriaș ar umple tot ecranul de ceață luminoasă.
        static readonly Color HitWhite = new Color(0.82f, 0.8f, 0.78f);

        public void Flash(Color color, float seconds)
        {
            _flashColor = color;
            _flashDuration = Mathf.Max(0.01f, seconds);
            _flashTime = _flashDuration;
        }

        // Squash & stretch: x>1,y<1 = turtit; x<1,y>1 = întins. Revine singur, ca un arc.
        public void Squash(float x, float y) { _squash = new Vector2(x, y); }

        public void SetTargetable(bool on, Color glow)
        {
            _targetable = on; _glow = glow;
        }

        public void SetHovered(bool on) { _hovered = on; }
        public bool IsHovered => _hovered;

        public void SetActiveTurn(bool on)
        {
            _active = on;
            _ring.enabled = on && !Dead;
            if (on) Squash(0.92f, 1.1f);
        }

        public void SetOrderBoost(int boost)
        {
            int baseOrder = (IsBoss ? 6 : 10) + boost;
            _order = baseOrder;
            _body.sortingOrder = baseOrder;
            _flash.sortingOrder = baseOrder + 1;
            foreach (var o in _outline) o.sortingOrder = baseOrder - 1;
            if (_ext != null) _ext.sortingOrder = baseOrder;
        }

        public float Alpha { get => _alpha; set => _alpha = Mathf.Clamp01(value); }

        // ---------- bătăi de animație (timp scalat: îngheață în hit-stop, merg ×2 la viteză rapidă) ----------

        // Anticipare: atacatorul se trage puțin înapoi și se turtește, ca un arc care se încarcă.
        public IEnumerator Anticipate(float duration = 0.16f, float distance = 0.22f)
        {
            Hold(Frames.Attack[0], duration + 0.3f);
            Squash(1.12f, 0.88f);
            Vector3 from = _offset, to = _offset - FrontDir * distance;
            yield return TweenOffset(from, to, duration, Ease.OutQuad);
        }

        // Lunge: sare spre țintă și se oprește la `stop` unități de ea. Se termină în momentul impactului.
        public IEnumerator Lunge(Vector3 targetFeet, float duration = 0.1f, float stop = 1.0f)
        {
            Vector3 home = Home;
            Vector3 dest = targetFeet - FrontDir * stop;
            Vector3 to = dest - home;
            // Chiar și la țintele foarte apropiate, saltul are măcar o jumătate de unitate: altfel nu se „simte”.
            if (Vector3.Dot(to, FrontDir) < 0.45f) to = FrontDir * 0.45f;
            Hold(Frames.Attack[Frames.Attack.Length - 1], duration + 0.35f);
            Squash(0.85f, 1.14f);
            SetOrderBoost(8);
            yield return TweenOffset(_offset, to, duration, Ease.OutCubic);
            Squash(1.12f, 0.9f);
        }

        // Atac pe loc (lacrimi, vrăji): doar cadrele de atac și puțin recul.
        public IEnumerator Recoil(float distance = 0.12f)
        {
            Hold(Frames.Attack[Frames.Attack.Length - 1], 0.3f);
            Squash(0.9f, 1.1f);
            Vector3 start = _offset;
            yield return TweenOffset(start, start - FrontDir * distance, 0.05f, Ease.OutQuad);
            StartCoroutine(TweenOffset(_offset, start, 0.18f, Ease.InOutQuad));
        }

        public IEnumerator ReturnHome(float duration = 0.22f)
        {
            if (_offset.sqrMagnitude > 1e-6f) yield return TweenOffset(_offset, Vector3.zero, duration, Ease.InOutQuad);
            _offset = Vector3.zero;
            SetOrderBoost(0);
            ReleaseHold();
        }

        // Săritură în arc până lângă țintă (Hopper, Leaper, Big Leap al lui Monstro).
        public IEnumerator Leap(Vector3 targetFeet, float height, float duration, float stop = 0.8f)
        {
            Hold(Frames.Attack[0], duration + 0.3f);
            Squash(1.2f, 0.8f);
            yield return Wait(0.08f);
            Vector3 from = _offset;
            Vector3 to = targetFeet - FrontDir * stop - Home;
            SetOrderBoost(8);
            Squash(0.8f, 1.25f);
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / duration);
                _offset = Vector3.Lerp(from, to, k) + Vector3.up * height * 4f * k * (1f - k);
                yield return null;
            }
            _offset = to;
            Hold(Frames.Attack[Frames.Attack.Length - 1], 0.4f);
            Squash(1.35f, 0.7f);
        }

        // Ce se vede când primește o lovitură: cadrul de durere, clipire albă, recul, turtire.
        public void Hurt(Vector3 fromDir, bool big)
        {
            if (Dead) return;
            Hold(Frames.Hurt[0], big ? 0.38f : 0.28f);
            Flash(HitWhite, big ? 0.11f : 0.08f);
            Squash(1.18f, 0.84f);
            Vector3 push = fromDir.sqrMagnitude > 1e-4f ? fromDir.normalized : -FrontDir;
            push.y = 0f;
            StartCoroutine(Knockback(push * (big ? 0.26f : 0.14f)));
        }

        IEnumerator Knockback(Vector3 delta)
        {
            Vector3 start = _offset;
            yield return TweenOffset(start, start + delta, 0.05f, Ease.OutQuad);
            yield return TweenOffset(_offset, start, 0.2f, Ease.InOutQuad);
        }

        public IEnumerator Die()
        {
            Dead = true;
            _ring.enabled = false;
            Flash(HitWhite, 0.12f);
            Hold(Frames.Hurt[0], 0.12f);
            Squash(1.3f, 0.7f);
            yield return Wait(0.12f);
            ReleaseHold();
            if (Frames.Dead != null) PlayLoop(Frames.Dead, 1f);
            else { PlayLoop(Frames.Hurt, 1f); _alpha = 0.6f; }
            Squash(1.25f, 0.75f);
            _shadow.enabled = false;
            if (Incubus != null) Incubus.Vanish();
        }

        // Globin se reface: cadrul de regenerare, clipire verde, se ridică.
        public IEnumerator Revive()
        {
            var regen = Frames.Extra("regen");
            Flash(new Color(0.5f, 1f, 0.4f), 0.25f);
            if (regen != null) Hold(regen[0], 0.6f);
            Squash(0.8f, 1.25f);
            yield return Wait(0.5f);
            Dead = false;
            _alpha = 1f;
            _shadow.enabled = true;
            ReleaseHold();
            PlayLoop(IdleSet(), 2.2f);
        }

        // Alunecare spre un slot nou (schimb de ranguri, avansare după o moarte), cu un mic salt.
        public IEnumerator SlideTo(Vector3 newHome, float duration = 0.26f)
        {
            Vector3 from = Home;
            Squash(0.9f, 1.1f);
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float k = Easing.Evaluate(Ease.InOutQuad, Mathf.Clamp01(t / duration));
                Home = Vector3.Lerp(from, newHome, k);
                _offset.y = Mathf.Sin(k * Mathf.PI) * 0.18f;
                yield return null;
            }
            Home = newHome;
            _offset.y = 0f;
            Squash(1.12f, 0.9f);
        }

        // Apariție (invocări): zboară din sursă în slot și „crește” cu depășire.
        public IEnumerator PopIn(Vector3 fromWorld, float duration = 0.32f)
        {
            Vector3 start = fromWorld - Home;
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / duration);
                _offset = Vector3.Lerp(start, Vector3.zero, Easing.Evaluate(Ease.OutCubic, k)) + Vector3.up * Mathf.Sin(k * Mathf.PI) * 0.8f;
                _breath = Easing.Evaluate(Ease.OutBack, k);
                yield return null;
            }
            _offset = Vector3.zero;
            _breath = 1f;
            Squash(1.3f, 0.75f);
            Particles.Burst(Feet, ParticleKind.Dust, 8, Vector3.up, 1f, 1f);
        }

        public IEnumerator TweenOffset(Vector3 from, Vector3 to, float duration, Ease ease)
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

        // Pentru mișcări speciale (piciorul lui Mom, mâna) controller-ul mută direct offset-ul.
        public void SetOffset(Vector3 offset) { _offset = offset; }

        public static IEnumerator Wait(float seconds) { if (seconds > 0f) yield return new WaitForSeconds(seconds); }

        // ---------- fiecare frame ----------

        void LateUpdate()
        {
            float dt = Time.deltaTime;
            transform.position = Home + _offset;
            transform.rotation = CombatCamera.Billboard;

            if (_held != null && Time.time >= _heldUntil) _held = null;
            Sprite frame;
            if (_held != null) frame = _held;
            else if (_loop != null && _loop.Length > 0)
            {
                _loopTime += dt;
                frame = _loop[(int)(_loopTime * _loopFps) % _loop.Length];
            }
            else frame = Frames.Idle[0];
            _body.sprite = frame;

            // Respirație: scara pe Y urcă și coboară după un sinus (pivotul e la picioare, deci crește în sus).
            float breath = Dead ? 0f : Mathf.Sin(Time.time * 2f * Mathf.PI * 0.55f + _phase) * 0.028f;
            // Arcul de squash revine spre (1,1); exp(−k·dt) face revenirea independentă de frame rate.
            _squash = Vector2.Lerp(_squash, Vector2.one, 1f - Mathf.Exp(-14f * dt));
            _visual.localScale = new Vector3(_squash.x, _squash.y * (1f + breath), 1f) * _breath;

            // Lumina locală (torțele din cameră), atenuată ca personajele să rămână ușor de citit.
            _tint = Color.Lerp(LocalLight.Sample(Center), Color.white, 0.55f);
            if (_fury && !Dead) _tint *= Color.Lerp(Color.white, new Color(1f, 0.55f, 0.5f), 0.5f + 0.5f * Mathf.Sin(Time.time * 5f));
            var c = _tint; c.a = _alpha;
            _body.color = c;

            if (_ext != null) UpdateExtension(frame);

            // Clipirea: silueta albă peste sprite, care se stinge.
            var white = SpriteSilhouette.Of(frame)?.White;
            if (_flashTime > 0f && white != null)
            {
                _flashTime -= Time.unscaledDeltaTime;
                _flash.enabled = true;
                _flash.sprite = white;
                // La boss-i clipirea e mai slabă: suprafața albă e de 4–6 ori mai mare decât la un personaj obișnuit.
                var fc = _flashColor; fc.a = Mathf.Clamp01(_flashTime / _flashDuration) * _alpha * (IsBoss ? 0.6f : 1f);
                _flash.color = fc;
            }
            else _flash.enabled = false;

            // Conturul de țintă: pulsează (sinus pe timp real, ca să pulseze și în pauza de input).
            bool outline = (_targetable || _hovered) && !Dead && white != null;
            for (int i = 0; i < 4; i++)
            {
                _outline[i].enabled = outline;
                if (!outline) continue;
                _outline[i].sprite = white;
                float pulse = _hovered ? 1f : 0.55f + 0.45f * Mathf.Sin(Time.unscaledTime * 7f);
                var gc = _targetable ? _glow : Color.white; gc.a = pulse * _alpha;
                _outline[i].color = gc;
            }

            if (_ring.enabled)
            {
                float p = 0.6f + 0.4f * Mathf.Sin(Time.unscaledTime * 5f);
                _ring.color = new Color(Palette.Gold.r, Palette.Gold.g, Palette.Gold.b, p);
            }
            var sc = _shadow.color; sc.a = _alpha; _shadow.color = sc;
        }

        // Rochia lui Mom continuă în sus până iese din cadru: întindem ultimul rând de pixeli al cadrului.
        static readonly System.Collections.Generic.Dictionary<Sprite, Sprite> TopRows = new System.Collections.Generic.Dictionary<Sprite, Sprite>();
        void UpdateExtension(Sprite frame)
        {
            if (!TopRows.TryGetValue(frame, out var row))
            {
                Rect r = frame.textureRect;
                row = Sprite.Create(frame.texture, new Rect(r.x, r.y + r.height - 1, r.width, 1), new Vector2(frame.pivot.x / r.width, 0f), frame.pixelsPerUnit, 0, SpriteMeshType.FullRect);
                TopRows[frame] = row;
            }
            _ext.sprite = row;
            _ext.transform.localPosition = new Vector3(0f, (frame.rect.height - frame.pivot.y) / frame.pixelsPerUnit, 0f);
            _ext.transform.localScale = new Vector3(1f, 400f, 1f);
            _ext.color = _body.color;
        }
    }
}
