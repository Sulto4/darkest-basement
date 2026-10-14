using System;
using UnityEngine;

namespace DarkestBasement.Core
{
    // Un "Update" pe care îl poți lipi de orice obiect construit din cod.
    // Ecranele UI sunt clase simple (nu MonoBehaviour); când au nevoie de ceva în fiecare frame
    // (puls, urmărirea mouse-ului) pun un Ticker pe obiectul lor. Moare odată cu obiectul.
    public sealed class Ticker : MonoBehaviour
    {
        public Action OnTick;

        public static Ticker Attach(GameObject go, Action onTick)
        {
            var t = go.AddComponent<Ticker>();
            t.OnTick = onTick;
            return t;
        }

        void Update() => OnTick?.Invoke();
    }
}
