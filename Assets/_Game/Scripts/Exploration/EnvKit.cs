using DarkestBasement.Core;
using UnityEngine;

namespace DarkestBasement.Exploration
{
    // Piese comune pentru hol și cameră: recuzită, pete de sânge, decoruri lipite de perete.
    public static class EnvKit
    {
        // Recuzită în picioare (caca, pietre, oase), ca billboard pe podea.
        public static Billboard Prop(Transform parent, string name, Vector3 localPos, float scale = 1f, bool flip = false,
            MaterialKind kind = MaterialKind.UnlitCutout)
        {
            var b = Billboard.Create(parent, name, SpriteLibrary.Get("Sprites/Props/" + name), localPos, kind);
            if (scale != 1f) b.SetScale(scale);
            if (flip) b.SetFlip(true);
            return b;
        }

        // Pată văzută de sus, culcată pe podea, luminată de torțe ca și podeaua (blood_decal_*, trapa).
        public static MeshRenderer FloorDecal(Transform parent, MaterialSet materials, string sprite, Vector3 localPos, float scale, float yaw, Color? tint = null)
        {
            var s = SpriteLibrary.Get("Sprites/Props/" + sprite);
            var mat = materials.Get(MaterialKind.LitCutout, s.texture, tint);
            var size = new Vector2(s.rect.width / 32f * scale, s.rect.height / 32f * scale);
            return MeshKit.FlatDecal(parent, sprite, mat, localPos + Vector3.up * 0.01f, size, yaw);
        }

        // Decor plat lipit de un perete lateral (pânze, lanțuri). leftWall = peretele de la -X, care privește spre +X.
        // Pânza e desenată ca un colț stânga-sus; pe peretele din dreapta o oglindim, ca firul să pornească tot din colțul dinspre noi.
        public static GameObject WallDecal(Transform parent, MaterialSet materials, string sprite, Vector3 localPos, bool leftWall, float heightUnits = 0f)
        {
            var s = SpriteLibrary.Get("Sprites/Props/" + sprite);
            float w = s.rect.width / 32f, h = heightUnits > 0f ? heightUnits : s.rect.height / 32f;
            Material mat;
            if (heightUnits > 0f)
            {
                // Lanțul se repetă pe verticală: textura se întinde de (h / înălțimea ei) ori.
                mat = materials.Get(MaterialKind.LitCutout, s.texture, new Color(1f, 1f, 1f, 0.999f));
                mat.SetTextureScale(Materials.BaseMap, new Vector2(1f, h / (s.rect.height / 32f)));
            }
            else mat = materials.Get(MaterialKind.LitCutout, s.texture);

            var go = new GameObject(sprite);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.Euler(0f, leftWall ? -90f : 90f, 0f);
            go.transform.localScale = new Vector3(leftWall ? w : -w, h, 1f);
            go.AddComponent<MeshFilter>().sharedMesh = MeshKit.BillboardQuad;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go;
        }

        // Umbra de colț de-a lungul unui perete: o bandă întunecată pe perete (jos) și una pe podea (lângă perete).
        // inward = direcția dinspre perete spre interiorul încăperii. Banda de pe perete se vede doar dinspre interior,
        // ca în vederea laterală să dispară odată cu peretele.
        public static void AoAlongWall(Transform parent, MaterialSet materials, Vector3 start, Vector3 end, Vector3 inward, float strength = 0.7f)
        {
            var wallMat = materials.Get(MaterialKind.UnlitTransparent, ProceduralTextures.Fade, new Color(0f, 0f, 0f, strength));
            Materials.SetCull(wallMat, true);
            var floorMat = materials.Get(MaterialKind.UnlitTransparent, ProceduralTextures.Fade, new Color(0f, 0f, 0f, strength * 0.85f));
            Vector3 mid = (start + end) * 0.5f;
            float length = Vector3.Distance(start, end);

            var wall = new GameObject("AoWall");
            wall.transform.SetParent(parent, false);
            wall.transform.localPosition = mid + inward * 0.01f;
            wall.transform.localRotation = Quaternion.LookRotation(-inward, Vector3.up);
            wall.transform.localScale = new Vector3(length, 0.95f, 1f);
            wall.AddComponent<MeshFilter>().sharedMesh = MeshKit.BillboardQuad;
            var wr = wall.AddComponent<MeshRenderer>();
            wr.sharedMaterial = wallMat;
            wr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            var floor = new GameObject("AoFloor");
            floor.transform.SetParent(parent, false);
            floor.transform.localPosition = mid + inward * 0.4f + Vector3.up * 0.006f;
            floor.transform.localRotation = Quaternion.LookRotation(inward, Vector3.up);
            floor.transform.localScale = new Vector3(length, 1f, 0.8f);
            floor.AddComponent<MeshFilter>().sharedMesh = MeshKit.FlatQuad;
            var fr = floor.AddComponent<MeshRenderer>();
            fr.sharedMaterial = floorMat;
            fr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        public static float Range(System.Random rng, float min, float max) => min + (float)rng.NextDouble() * (max - min);

        public static T Pick<T>(System.Random rng, T[] items) => items[rng.Next(items.Length)];
    }
}
