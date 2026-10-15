using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace DarkestBasement.QA
{
    // Un mouse și o tastatură virtuale (Input System), pentru scriptul QA "input".
    // Nu chemăm funcțiile jocului: punem evenimente în coada Input System-ului, exact ca driverul unui mouse adevărat.
    // De acolo le citesc aceleași părți ca la un om: modulul UI (InputSystemUIInputModule → hover, click, drag pe uGUI)
    // și codul care citește Mouse.current / Keyboard.current (mersul, tastele 1–9, Space, F, M, Esc).
    //
    // Evenimentele din coadă se aplică la următorul update al Input System-ului (începutul frame-ului următor),
    // deci după fiecare schimbare lăsăm să treacă cel puțin un frame.
    public sealed class VirtualInput
    {
        public Mouse Mouse { get; private set; }
        public Keyboard Keyboard { get; private set; }
        public Vector2 Position { get; private set; }

        ushort _buttons;
        readonly HashSet<Key> _held = new HashSet<Key>();
        readonly List<InputDevice> _disabled = new List<InputDevice>();

        public static VirtualInput Create()
        {
            var v = new VirtualInput();
            // Fereastra de sub Xvfb poate pierde focusul; nu vrem ca Input System să ne dezactiveze dispozitivele atunci.
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            v.Mouse = InputSystem.AddDevice<Mouse>("QaVirtualMouse");
            v.Keyboard = InputSystem.AddDevice<Keyboard>("QaVirtualKeyboard");
            // Mouse-ul și tastatura reale (X11 sub Xvfb) ar putea muta cursorul pe neașteptate: le oprim cât rulează testul.
            foreach (var device in new List<InputDevice>(InputSystem.devices))
            {
                if (device == v.Mouse || device == v.Keyboard) continue;
                if (!(device is Pointer) && !(device is Keyboard)) continue;
                if (!device.enabled) continue;
                InputSystem.DisableDevice(device);
                v._disabled.Add(device);
            }
            v.Mouse.MakeCurrent();
            v.Keyboard.MakeCurrent();
            return v;
        }

        public string Describe()
        {
            var names = new List<string>();
            foreach (var d in _disabled) names.Add(d.name);
            return "mouse=" + Mouse.name + " keyboard=" + Keyboard.name + " real devices disabled=[" + string.Join(", ", names) + "]";
        }

        // ---- Mouse ----

        void QueueMouse(Vector2 delta)
        {
            InputSystem.QueueStateEvent(Mouse, new MouseState { position = Position, delta = delta, buttons = _buttons });
        }

        public void Warp(Vector2 screen)
        {
            var delta = screen - Position;
            Position = screen;
            QueueMouse(delta);
        }

        // Mișcare lină, câte un pas pe frame (uGUI vede fiecare poziție intermediară: hover-uri, drag-ul care pornește).
        public IEnumerator Glide(Vector2 to, float seconds)
        {
            Vector2 from = Position;
            float t = 0f;
            while (t < seconds)
            {
                t += Mathf.Max(Time.unscaledDeltaTime, 1f / 120f);
                float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / seconds));
                Warp(Vector2.Lerp(from, to, k));
                yield return null;
            }
            Warp(to);
            yield return null;
        }

        public void SetButton(MouseButton button, bool down)
        {
            int bit = 1 << (int)button;
            _buttons = (ushort)(down ? _buttons | bit : _buttons & ~bit);
            QueueMouse(Vector2.zero);
        }

        public bool LeftHeld => (_buttons & 1) != 0;

        // Click ca un om: ajunge acolo, se oprește o clipă (hover), apasă ~0,1 s, ridică.
        public IEnumerator Click(Vector2 at, float glide = 0.15f, MouseButton button = MouseButton.Left)
        {
            if ((at - Position).sqrMagnitude > 0.25f) yield return Glide(at, glide);
            yield return Wait(0.06f);
            SetButton(button, true);
            yield return Wait(0.09f);
            SetButton(button, false);
            yield return null;
            yield return null;
        }

        // ---- Tastatura ----

        void QueueKeys()
        {
            var keys = new Key[_held.Count];
            _held.CopyTo(keys);
            InputSystem.QueueStateEvent(Keyboard, new KeyboardState(keys));
        }

        public void SetKey(Key key, bool down)
        {
            if (down ? !_held.Add(key) : !_held.Remove(key)) return;
            QueueKeys();
        }

        public bool IsHeld(Key key) => _held.Contains(key);

        // Apăsare scurtă (wasPressedThisFrame se vede în frame-ul de după).
        public IEnumerator Tap(Key key, float hold = 0.07f)
        {
            SetKey(key, true);
            yield return null;
            yield return Wait(hold);
            SetKey(key, false);
            yield return null;
        }

        // Textul tastat ajunge și în Input System (Keyboard.onTextInput), pentru cine îl ascultă.
        public void Text(char c) => InputSystem.QueueTextEvent(Keyboard, c);

        public void ReleaseAll()
        {
            _held.Clear();
            QueueKeys();
            _buttons = 0;
            QueueMouse(Vector2.zero);
        }

        // La final: scoatem dispozitivele virtuale și le pornim la loc pe cele reale.
        public void Dispose()
        {
            if (Mouse != null && Mouse.added) InputSystem.RemoveDevice(Mouse);
            if (Keyboard != null && Keyboard.added) InputSystem.RemoveDevice(Keyboard);
            foreach (var d in _disabled) if (d.added) InputSystem.EnableDevice(d);
            _disabled.Clear();
        }

        static IEnumerator Wait(float seconds)
        {
            float end = Time.unscaledTime + seconds;
            while (Time.unscaledTime < end) yield return null;
        }
    }
}
