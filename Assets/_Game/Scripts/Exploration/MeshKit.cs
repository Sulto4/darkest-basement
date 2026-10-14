using System.Collections.Generic;
using UnityEngine;

namespace DarkestBasement.Exploration
{
    // Construiește geometria holurilor și camerelor din dreptunghiuri (quad-uri).
    // Un quad are un colț, o latură "dreapta" și una "sus". Fața vizibilă e cea spre normala Cross(sus, dreapta);
    // din spate quad-ul e invizibil (Cull Back). Pe asta se bazează vederea laterală: camera iese prin perete
    // și peretele dispare singur, pentru că acum îl vede din spate.
    //
    // UV-urile se calculează din poziția în lume împărțită la mărimea plăcii de textură, ca
    // textura să continue perfect de la un quad la altul (o cărămidă nu se rupe la îmbinare).
    public sealed class MeshBatch
    {
        sealed class Part
        {
            public readonly List<Vector3> Vertices = new List<Vector3>();
            public readonly List<Vector3> Normals = new List<Vector3>();
            public readonly List<Vector2> Uvs = new List<Vector2>();
            public readonly List<int> Triangles = new List<int>();
        }

        // Geometria se împarte în bucăți pe Z, ca Unity să nu deseneze ce e în afara camerei.
        public float ChunkSize = 8f;

        readonly Dictionary<(Material, int), Part> _parts = new Dictionary<(Material, int), Part>();

        public void Quad(Material material, Vector3 corner, Vector3 right, Vector3 up, float tileSize)
        {
            Vector3 center = corner + (right + up) * 0.5f;
            int chunk = Mathf.FloorToInt(center.z / ChunkSize);
            var key = (material, chunk);
            if (!_parts.TryGetValue(key, out var part)) _parts[key] = part = new Part();

            Vector3 normal = Vector3.Cross(up, right).normalized;
            Vector3 ru = right.normalized, uu = up.normalized;
            int start = part.Vertices.Count;
            Vector3[] corners = { corner, corner + up, corner + up + right, corner + right };
            foreach (var p in corners)
            {
                part.Vertices.Add(p);
                part.Normals.Add(normal);
                part.Uvs.Add(new Vector2(Vector3.Dot(p, ru), Vector3.Dot(p, uu)) / tileSize);
            }
            part.Triangles.Add(start); part.Triangles.Add(start + 1); part.Triangles.Add(start + 2);
            part.Triangles.Add(start); part.Triangles.Add(start + 2); part.Triangles.Add(start + 3);
        }

        // Cutie cu fețele spre exterior (grinzi, stâlpi). skipFaces: fețele pe care nu le vezi niciodată.
        public void Box(Material material, Vector3 center, Vector3 size, float tileSize, bool top = true, bool bottom = true)
        {
            Vector3 h = size * 0.5f;
            float sx = size.x, sy = size.y, sz = size.z;
            if (top) Quad(material, center + new Vector3(-h.x, h.y, -h.z), new Vector3(sx, 0, 0), new Vector3(0, 0, sz), tileSize);
            if (bottom) Quad(material, center + new Vector3(-h.x, -h.y, h.z), new Vector3(sx, 0, 0), new Vector3(0, 0, -sz), tileSize);
            Quad(material, center + new Vector3(h.x, -h.y, -h.z), new Vector3(0, 0, sz), new Vector3(0, sy, 0), tileSize);   // +X
            Quad(material, center + new Vector3(-h.x, -h.y, h.z), new Vector3(0, 0, -sz), new Vector3(0, sy, 0), tileSize);  // -X
            Quad(material, center + new Vector3(h.x, -h.y, h.z), new Vector3(-sx, 0, 0), new Vector3(0, sy, 0), tileSize);   // +Z
            Quad(material, center + new Vector3(-h.x, -h.y, -h.z), new Vector3(sx, 0, 0), new Vector3(0, sy, 0), tileSize);  // -Z
        }

        // Transformă tot ce s-a adunat în obiecte cu MeshRenderer. Întoarce mesh-urile, ca proprietarul să le distrugă la final.
        public List<Mesh> Build(Transform parent, string name)
        {
            var meshes = new List<Mesh>();
            foreach (var pair in _parts)
            {
                var part = pair.Value;
                var mesh = new Mesh { name = name + "_" + pair.Key.Item1.name + "_" + pair.Key.Item2 };
                mesh.SetVertices(part.Vertices);
                mesh.SetNormals(part.Normals);
                mesh.SetUVs(0, part.Uvs);
                mesh.SetTriangles(part.Triangles, 0);
                mesh.RecalculateBounds();
                meshes.Add(mesh);

                var go = new GameObject(mesh.name);
                go.transform.SetParent(parent, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var r = go.AddComponent<MeshRenderer>();
                r.sharedMaterial = pair.Key.Item1;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
            }
            _parts.Clear();
            return meshes;
        }
    }

    public static class MeshKit
    {
        static Mesh _billboard, _flat;

        // Quad pentru sprite-uri în picioare: x ∈ [-0.5, 0.5], y ∈ [0, 1] (pivot jos-centru), fața spre -Z.
        public static Mesh BillboardQuad => _billboard != null ? _billboard : (_billboard = MakeQuad("BillboardQuad", false));

        // Quad culcat pe podea: x ∈ [-0.5, 0.5], z ∈ [-0.5, 0.5], fața în sus (pete, umbre, trapa).
        public static Mesh FlatQuad => _flat != null ? _flat : (_flat = MakeQuad("FlatQuad", true));

        static Mesh MakeQuad(string name, bool flat)
        {
            var mesh = new Mesh { name = name };
            if (flat)
            {
                mesh.vertices = new[] { new Vector3(-0.5f, 0, -0.5f), new Vector3(-0.5f, 0, 0.5f), new Vector3(0.5f, 0, 0.5f), new Vector3(0.5f, 0, -0.5f) };
                mesh.normals = new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up };
            }
            else
            {
                mesh.vertices = new[] { new Vector3(-0.5f, 0, 0), new Vector3(-0.5f, 1, 0), new Vector3(0.5f, 1, 0), new Vector3(0.5f, 0, 0) };
                mesh.normals = new[] { Vector3.back, Vector3.back, Vector3.back, Vector3.back };
            }
            mesh.uv = new[] { new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0) };
            mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            mesh.RecalculateBounds();
            return mesh;
        }

        // Un obiect cu quad culcat pe podea (pete de sânge, umbre).
        public static MeshRenderer FlatDecal(Transform parent, string name, Material material, Vector3 localPos, Vector2 size, float yaw)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.Euler(0, yaw, 0);
            go.transform.localScale = new Vector3(size.x, 1f, size.y);
            go.AddComponent<MeshFilter>().sharedMesh = FlatQuad;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return r;
        }
    }
}
