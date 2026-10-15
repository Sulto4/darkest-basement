using DarkestBasement.Core;
using DarkestBasement.UI;
using TMPro;
using UnityEngine;

namespace DarkestBasement.Exploration
{
    // Numere și cuvinte care sar deasupra unui erou în explorare (damage de capcană, vindecare, "DODGE").
    // Textul e în UI, dar își urmărește punctul din lume în fiecare frame, ca să stea lipit de erou chiar dacă camera se mișcă.
    // Culorile urmează GDD-ul: alb = damage, verde = vindecare, galben = special.
    public static class FloatingText
    {
        public static void Spawn(Vector3 worldPos, string text, Color color, bool big = false, float delay = 0f)
        {
            var ui = UIRoot.Instance;
            var rig = CameraRig.Instance;
            if (ui == null || rig == null) return;

            int mult = big ? 3 : 2;
            // Lat și fără împărțire pe rânduri: "A SOOTHING WARMTH" trebuie să rămână o singură linie.
            var label = UiKit.Label(ui.World, text, new Vector2(-1000, -1000), new Vector2(400, 30), UiFont.LabelBold, color,
                TextAlignmentOptions.Center, mult);
            label.Front.textWrappingMode = TextWrappingModes.NoWrap;
            label.Back.textWrappingMode = TextWrappingModes.NoWrap;
            var rt = label.Rect;
            rt.localScale = Vector3.zero;
            float age = -delay;
            const float life = 1.15f;

            Ticker.Attach(rt.gameObject, () =>
            {
                age += Time.unscaledDeltaTime;
                if (age < 0f) return;
                // Urcă 22 px încetinind, "pop" la apariție, apoi se stinge în ultima treime.
                float rise = Easing.Evaluate(Ease.OutCubic, age / life) * 22f;
                Vector2 p = rig.WorldToUI(worldPos);
                rt.anchoredPosition = new Vector2(Mathf.Round(p.x), Mathf.Round(p.y + 6f + rise));
                float pop = Easing.Evaluate(Ease.OutBack, Mathf.Clamp01(age / 0.22f));
                rt.localScale = Vector3.one * pop;
                label.Alpha = 1f - Mathf.Clamp01((age - life * 0.66f) / (life * 0.34f));
                if (age >= life) Object.Destroy(rt.gameObject);
            });
        }
    }
}
