using System.Collections.Generic;
using UnityEngine;

namespace DarkestBasement.Core
{
    // Sprite-urile (eroi, recuzită) nu primesc lumina URP: un sprite plat luminat din lateral ar ieși pe jumătate negru.
    // În schimb le colorăm noi după luminile din apropiere: lângă o torță sunt calde și clare,
    // între torțe se scufundă în întuneric. Asta face holul să "respire" când echipa trece pe lângă flăcări.
    public static class LocalLight
    {
        static readonly List<Light> Lights = new List<Light>();

        // Lumina minimă a unui sprite (setată de FloorTheme). Puțin peste ambientul pereților, ca eroii să rămână lizibili.
        public static Color Ambient = new Color(0.2f, 0.15f, 0.12f);

        public static void Register(Light light) { if (light != null && !Lights.Contains(light)) Lights.Add(light); }
        public static void Unregister(Light light) { Lights.Remove(light); }

        public static Color Sample(Vector3 position)
        {
            Color c = Ambient;
            for (int i = Lights.Count - 1; i >= 0; i--)
            {
                var light = Lights[i];
                if (light == null) { Lights.RemoveAt(i); continue; }
                if (!light.isActiveAndEnabled || light.intensity <= 0f) continue;
                float d = Vector3.Distance(position, light.transform.position);
                if (d >= light.range) continue;
                // Scădere pătratică spre marginea razei: aproape de sursă e mult mai luminos decât la jumătatea distanței.
                float k = 1f - d / light.range;
                c += light.color * (light.intensity * k * k * 0.45f);
            }
            // Nu lăsăm sprite-ul să devină alb ars; strălucirea adevărată vine din bloom, pe flăcări.
            c.r = Mathf.Min(c.r, 1.25f); c.g = Mathf.Min(c.g, 1.15f); c.b = Mathf.Min(c.b, 1.1f); c.a = 1f;
            return c;
        }
    }
}
