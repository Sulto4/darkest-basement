using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace DarkestBasement.Core
{
    // Un tween = o valoare care merge de la A la B într-un timp dat, pe o curbă de easing.
    // Toate animațiile "de cod" ale jocului (cărți, numere de damage, lunge, UI) trec pe aici.
    public sealed class TweenHandle
    {
        internal float Elapsed;
        internal float Duration;
        internal float Delay;
        internal Ease Ease;
        internal Action<float> OnUpdate;
        internal Action OnComplete;
        internal bool Unscaled;
        internal UnityEngine.Object Owner;
        public bool IsDone { get; internal set; }

        // Oprește tween-ul fără să mai cheme OnComplete.
        public void Kill() { IsDone = true; OnComplete = null; }

        // Permite "yield return handle.Wait()" în corutine, ca animațiile să se poată înlănțui.
        public IEnumerator Wait() { while (!IsDone) yield return null; }
    }

    public sealed class Tween : MonoBehaviour
    {
        static Tween _instance;
        readonly List<TweenHandle> _active = new List<TweenHandle>();
        readonly List<TweenHandle> _toAdd = new List<TweenHandle>();

        static Tween Instance
        {
            get
            {
                if (_instance == null)
                {
                    var go = new GameObject("[Tween]");
                    DontDestroyOnLoad(go);
                    _instance = go.AddComponent<Tween>();
                }
                return _instance;
            }
        }

        // Varianta de bază: chemăm onUpdate cu progresul cu easing (0→1) în fiecare frame.
        // unscaled = true ignoră Time.timeScale (necesar pentru UI în timpul hit-stop-ului).
        public static TweenHandle Value(float duration, Action<float> onUpdate, Ease ease = Ease.OutCubic,
            Action onComplete = null, float delay = 0f, bool unscaled = false, UnityEngine.Object owner = null)
        {
            var h = new TweenHandle
            {
                Duration = Mathf.Max(0.0001f, duration), Ease = ease, OnUpdate = onUpdate,
                OnComplete = onComplete, Delay = delay, Unscaled = unscaled, Owner = owner,
            };
            Instance._toAdd.Add(h);
            return h;
        }

        public static TweenHandle Move(Transform t, Vector3 to, float duration, Ease ease = Ease.OutCubic, Action onComplete = null, float delay = 0f)
        {
            Vector3 from = t.position;
            return Value(duration, k => t.position = Vector3.LerpUnclamped(from, to, k), ease, onComplete, delay, false, t);
        }

        public static TweenHandle MoveLocal(Transform t, Vector3 to, float duration, Ease ease = Ease.OutCubic, Action onComplete = null, float delay = 0f, bool unscaled = false)
        {
            Vector3 from = t.localPosition;
            return Value(duration, k => t.localPosition = Vector3.LerpUnclamped(from, to, k), ease, onComplete, delay, unscaled, t);
        }

        public static TweenHandle Scale(Transform t, Vector3 to, float duration, Ease ease = Ease.OutBack, Action onComplete = null, float delay = 0f, bool unscaled = false)
        {
            Vector3 from = t.localScale;
            return Value(duration, k => t.localScale = Vector3.LerpUnclamped(from, to, k), ease, onComplete, delay, unscaled, t);
        }

        public static TweenHandle Rotate(Transform t, Quaternion to, float duration, Ease ease = Ease.InOutCubic, Action onComplete = null, float delay = 0f)
        {
            Quaternion from = t.rotation;
            return Value(duration, k => t.rotation = Quaternion.SlerpUnclamped(from, to, k), ease, onComplete, delay, false, t);
        }

        public static TweenHandle Color(SpriteRenderer r, UnityEngine.Color to, float duration, Ease ease = Ease.Linear, Action onComplete = null, float delay = 0f)
        {
            UnityEngine.Color from = r.color;
            return Value(duration, k => r.color = UnityEngine.Color.LerpUnclamped(from, to, k), ease, onComplete, delay, false, r);
        }

        // Așteptare simplă, utilă în secvențe.
        public static TweenHandle Delay(float seconds, Action then, bool unscaled = false)
        {
            return Value(seconds, null, Ease.Linear, then, 0f, unscaled);
        }

        // Oprește toate tween-urile legate de un obiect (de ex. când e distrus sau re-animat).
        public static void KillAll(UnityEngine.Object owner)
        {
            if (_instance == null) return;
            foreach (var h in _instance._active) if (h.Owner == owner) h.Kill();
            foreach (var h in _instance._toAdd) if (h.Owner == owner) h.Kill();
        }

        void Update()
        {
            if (_toAdd.Count > 0) { _active.AddRange(_toAdd); _toAdd.Clear(); }

            for (int i = 0; i < _active.Count; i++)
            {
                var h = _active[i];
                if (h.IsDone) continue;
                // Obiectul animat a fost distrus: tween-ul moare odată cu el.
                if (h.Owner == null && !ReferenceEquals(h.Owner, null)) { h.IsDone = true; continue; }

                float dt = h.Unscaled ? Time.unscaledDeltaTime : Time.deltaTime;
                if (h.Delay > 0f) { h.Delay -= dt; continue; }

                h.Elapsed += dt;
                float t = Mathf.Clamp01(h.Elapsed / h.Duration);
                h.OnUpdate?.Invoke(Easing.Evaluate(h.Ease, t));
                if (t >= 1f)
                {
                    h.IsDone = true;
                    h.OnComplete?.Invoke();
                }
            }
            _active.RemoveAll(h => h.IsDone);
        }
    }
}
