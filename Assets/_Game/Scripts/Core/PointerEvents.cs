using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace DarkestBasement.Core
{
    // Hover și click pe un element UI, ca acțiuni simple (fără să scrii o clasă nouă pentru fiecare buton).
    // Elementul are nevoie de un Graphic cu raycastTarget = true (de ex. un Image).
    public sealed class PointerEvents : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        public Action OnEnter;
        public Action OnExit;
        public Action OnClick;

        public static PointerEvents Of(GameObject go)
        {
            var p = go.GetComponent<PointerEvents>();
            return p != null ? p : go.AddComponent<PointerEvents>();
        }

        public void OnPointerEnter(PointerEventData eventData) => OnEnter?.Invoke();
        public void OnPointerExit(PointerEventData eventData) => OnExit?.Invoke();

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left) OnClick?.Invoke();
        }
    }
}
