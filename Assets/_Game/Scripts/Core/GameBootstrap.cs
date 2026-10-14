using DarkestBasement.Audio;
using DarkestBasement.QA;
using DarkestBasement.UI;
using UnityEngine;

namespace DarkestBasement.Core
{
    // Singurul obiect din scenă. Construiește tot jocul din cod, în ordinea în care piesele depind una de alta:
    // UI (canvas-ul pixel-perfect) → sunet → lumea (camera + imaginea de 640×360 + post-procesare) → mașina de stări.
    // Cu "-qa" în linia de comandă, adaugă și pilotul automat.
    public sealed class GameBootstrap : MonoBehaviour
    {
        void Awake()
        {
            Application.targetFrameRate = 60;
            Application.runInBackground = true;

            var ui = UIRoot.Create();
            _ = AudioManager.Instance;
            WorldView.Create(ui);

            // QA-ul se agață primul, ca să prindă și prima captură (meniul).
            if (CommandLine.Has("-qa")) gameObject.AddComponent<QaDirector>();
            gameObject.AddComponent<GameFlow>();
        }
    }
}
