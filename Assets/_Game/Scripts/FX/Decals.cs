using System.Collections.Generic;
using DarkestBasement.Core;
using UnityEngine;

namespace DarkestBasement.FX
{
    // Pete de sânge care rămân pe podea toată lupta (ca în Isaac: camera „își amintește” bătaia).
    // Le culcăm pe podea (nu sunt billboard), așa că rămân la locul lor și când camera se rotește.
    public static class Decals
    {
        const int Max = 70;
        static readonly Queue<GameObject> Live = new Queue<GameObject>();
        static readonly Dictionary<Sprite, Sprite> Centered = new Dictionary<Sprite, Sprite>();
        static Transform _root, _parent;
        static int _count;

        // Petele stau în camera în care s-a dat lupta (dispar odată cu ea).
        public static void AttachTo(Transform room)
        {
            _parent = room;
            if (_root != null) _root.SetParent(room, true);
        }

        // right = direcția „spre inamici” a scenei; o folosim ca petele să fie aliniate cu camera laterală.
        public static void BloodSplat(Vector3 floorPoint, float size, Vector3 right, Color? tint = null)
        {
            var src = SpriteLibrary.Get("Sprites/Props/blood_decal_" + Random.Range(0, 5));
            var sprite = CenterPivot(src);
            EnsureRoot();
            var go = new GameObject("decal");
            go.transform.SetParent(_root, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = tint ?? Color.white;
            sr.sortingOrder = -45;
            sr.flipX = Random.value < 0.5f;
            if (right.sqrMagnitude < 1e-4f) right = Vector3.right;
            Vector3 depth = Vector3.Cross(right.normalized, Vector3.up);
            // Pozițiile ușor diferite pe Y evită „pâlpâirea” între pete suprapuse.
            _count = (_count + 1) % 50;
            go.transform.position = new Vector3(floorPoint.x, floorPoint.y + 0.01f + _count * 0.0004f, floorPoint.z);
            go.transform.rotation = Quaternion.LookRotation(Vector3.down, depth);
            // Pe adâncime o întindem puțin: camera laterală vede podeaua foarte oblic.
            go.transform.localScale = new Vector3(size, size * 1.7f, 1f);
            Keep(go);
        }

        // Orice obiect care trebuie să rămână în cameră (de ex. cadavrele) intră în aceeași limită.
        public static void Keep(GameObject go)
        {
            if (go == null) return;
            EnsureRoot();
            go.transform.SetParent(_root, true);
            Live.Enqueue(go);
            while (Live.Count > Max)
            {
                var old = Live.Dequeue();
                if (old != null) Object.Destroy(old);
            }
        }

        public static void Clear()
        {
            while (Live.Count > 0)
            {
                var go = Live.Dequeue();
                if (go != null) Object.Destroy(go);
            }
            if (_root != null) Object.Destroy(_root.gameObject);
            _root = null;
        }

        // Petele sunt desenate „de sus”, cu centrul în mijloc; refacem sprite-ul cu pivot central
        // ca să nu depindem de setările de import.
        static Sprite CenterPivot(Sprite s)
        {
            if (s == null || s.texture == null) return s;
            if (Centered.TryGetValue(s, out var c)) return c;
            c = Sprite.Create(s.texture, s.rect, new Vector2(0.5f, 0.5f), s.pixelsPerUnit, 0, SpriteMeshType.FullRect);
            Centered[s] = c;
            return c;
        }

        static void EnsureRoot()
        {
            if (_root != null) return;
            _root = new GameObject("[Decals]").transform;
            if (_parent != null) _root.SetParent(_parent, true);
        }
    }
}
