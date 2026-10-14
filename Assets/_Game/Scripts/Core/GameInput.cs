using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace DarkestBasement.Core
{
    // Toate tastele explorării într-un singur loc (Input System: Keyboard.current / Mouse.current).
    // QA-ul automat poate "ține apăsat" mersul prin AutoWalk, fără să simuleze taste.
    public static class GameInput
    {
        public static bool AutoWalk;

        // Ține W / D / → / ↑ sau click stânga (pe lume, nu pe un buton) ca să mergi.
        public static bool WalkHeld
        {
            get
            {
                if (AutoWalk) return true;
                var kb = Keyboard.current;
                if (kb != null && (kb.wKey.isPressed || kb.dKey.isPressed || kb.rightArrowKey.isPressed || kb.upArrowKey.isPressed))
                    return true;
                var mouse = Mouse.current;
                return mouse != null && mouse.leftButton.isPressed && !PointerOverUI();
            }
        }

        public static bool MapPressed => Pressed(Key.M);
        public static bool PausePressed => Pressed(Key.Escape);

        public static bool Pressed(Key key)
        {
            var kb = Keyboard.current;
            return kb != null && kb[key].wasPressedThisFrame;
        }

        public static bool ClickedThisFrame
        {
            get { var m = Mouse.current; return m != null && m.leftButton.wasPressedThisFrame; }
        }

        public static Vector2 PointerPosition
        {
            get { var m = Mouse.current; return m != null ? m.position.ReadValue() : Vector2.zero; }
        }

        // Click-ul pe un buton UI nu trebuie să pornească și mersul.
        public static bool PointerOverUI()
        {
            var es = EventSystem.current;
            return es != null && es.IsPointerOverGameObject();
        }
    }
}
