using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace DarkestBasement.UI
{
    // Prinde intrarea/ieșirea mouse-ului pe un element UI (pentru tooltip-uri și hover).
    // Elementul are nevoie de un Graphic cu raycastTarget = true (de ex. un Image).
    public sealed class CombatHoverTrigger : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        public Action Enter;
        public Action Exit;
        public Action Click;
        public bool IsHovered { get; private set; }

        public static CombatHoverTrigger On(GameObject go, Action enter, Action exit, Action click = null)
        {
            var t = go.GetComponent<CombatHoverTrigger>();
            if (t == null) t = go.AddComponent<CombatHoverTrigger>();
            t.Enter = enter; t.Exit = exit; t.Click = click;
            return t;
        }

        public void OnPointerEnter(PointerEventData eventData) { IsHovered = true; Enter?.Invoke(); }
        public void OnPointerExit(PointerEventData eventData) { IsHovered = false; Exit?.Invoke(); }
        public void OnPointerClick(PointerEventData eventData) { if (eventData.button == PointerEventData.InputButton.Left) Click?.Invoke(); }

        void OnDisable()
        {
            // Dacă elementul dispare sub mouse, nu lăsăm tooltip-ul agățat pe ecran.
            if (IsHovered) { IsHovered = false; Exit?.Invoke(); }
        }
    }
}
