using System.Collections;
using UnityEngine;

namespace DarkestBasement.FX
{
    // Un singur obiect ascuns care „ține în viață” efectele globale.
    // HitStop, ScreenShake și Particles sunt clase statice (ușor de chemat de oriunde: ScreenShake.Add(0.3f)),
    // dar o clasă statică nu primește Update() de la Unity. FxHost le dă pe rând câte un pas, în fiecare frame.
    public sealed class FxHost : MonoBehaviour
    {
        static FxHost _instance;

        public static FxHost Instance
        {
            get
            {
                if (_instance == null)
                {
                    var go = new GameObject("[FX]");
                    DontDestroyOnLoad(go);
                    _instance = go.AddComponent<FxHost>();
                }
                return _instance;
            }
        }

        public static void Ensure() { var _ = Instance; }

        // Părintele obiectelor de efect de scurtă durată (proiectile, raze, umbre). Controller-ul de luptă
        // îl setează la el însuși: dacă lupta e distrusă brusc (Abandon), dispar și efectele rămase la jumătate.
        public static Transform Stage;

        public static GameObject Spawn(string name)
        {
            var go = new GameObject(name);
            if (Stage != null) go.transform.SetParent(Stage, true);
            return go;
        }

        // Pentru clasele statice care au nevoie de o corutină (de ex. RewardScreen).
        public static Coroutine Run(IEnumerator routine) => Instance.StartCoroutine(routine);

        void Update()
        {
            // Hit-stop-ul se verifică primul: el decide cât de repede curge timpul în frame-ul ăsta.
            HitStop.Tick();
            Particles.Tick();
        }

        void LateUpdate()
        {
            // Shake-ul se aplică după ce camera și-a calculat poziția, ca offset-ul să nu fie suprascris.
            ScreenShake.Tick();
            FxVolume.Tick();
        }

        void OnDestroy()
        {
            if (_instance == this) { _instance = null; Time.timeScale = 1f; }
        }
    }
}
