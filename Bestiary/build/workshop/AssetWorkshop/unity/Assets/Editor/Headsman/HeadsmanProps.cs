using System.Collections.Generic;
using UnityEngine;

namespace Workshop.Headsman
{
    /// <summary>Simple stand-ins for the preview bake: plain materials, primitives without colliders, faceted chunks.</summary>
    public static class HeadsmanProps
    {
        private static readonly float T = (1f + Mathf.Sqrt(5f)) / 2f;

        private static readonly Vector3[] Ico =
        {
            new Vector3(-1, T, 0), new Vector3(1, T, 0), new Vector3(-1, -T, 0), new Vector3(1, -T, 0),
            new Vector3(0, -1, T), new Vector3(0, 1, T), new Vector3(0, -1, -T), new Vector3(0, 1, -T),
            new Vector3(T, 0, -1), new Vector3(T, 0, 1), new Vector3(-T, 0, -1), new Vector3(-T, 0, 1),
        };

        private static readonly int[] Faces =
        {
            0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11, 1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8,
            3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9, 4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1,
        };

        public static Material Plain(string name, Color color, bool glow = false)
        {
            var material = new Material(Shader.Find("Standard")) { name = name, color = color };
            material.SetFloat("_Glossiness", 0.08f);
            if (glow)
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", color * 2f);
            }
            return material;
        }

        public static GameObject Primitive(PrimitiveType type, string name, Material material)
        {
            GameObject made = GameObject.CreatePrimitive(type);
            made.name = name;
            Object.DestroyImmediate(made.GetComponent<Collider>());
            made.GetComponent<Renderer>().sharedMaterial = material;
            return made;
        }

        /// <summary>A faceted chunk (a jittered icosahedron) `size` across, for rocks and bone shards.</summary>
        public static GameObject Chunk(string name, Material material, Vector3 size, int seed)
        {
            var random = new System.Random(seed);
            var vertices = new Vector3[Ico.Length];
            for (int i = 0; i < Ico.Length; i++)
                vertices[i] = Vector3.Scale(Ico[i].normalized * (0.75f + 0.4f * (float)random.NextDouble()), size * 0.5f);
            var mesh = new Mesh { name = name, vertices = vertices, triangles = Faces };
            mesh.RecalculateNormals();
            var chunk = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            chunk.GetComponent<MeshFilter>().sharedMesh = mesh;
            chunk.GetComponent<MeshRenderer>().sharedMaterial = material;
            return chunk;
        }

        /// <summary>A flat ring on the ground, `radius` out and `width` wide, seen from both sides.</summary>
        public static GameObject Ring(string name, Material material, float radius, float width)
        {
            const int segments = 48;
            var vertices = new Vector3[segments * 2];
            var triangles = new List<int>();
            for (int i = 0; i < segments; i++)
            {
                float a = i * Mathf.PI * 2f / segments;
                var along = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                (vertices[2 * i], vertices[2 * i + 1]) = (along * (radius - width), along * radius);
                int j = (i + 1) % segments;
                triangles.AddRange(new[] { 2 * i, 2 * j, 2 * i + 1, 2 * i + 1, 2 * j, 2 * j + 1 });
                triangles.AddRange(new[] { 2 * i, 2 * i + 1, 2 * j, 2 * i + 1, 2 * j + 1, 2 * j });
            }
            var mesh = new Mesh { name = name, vertices = vertices, triangles = triangles.ToArray() };
            mesh.RecalculateNormals();
            var ring = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            ring.GetComponent<MeshFilter>().sharedMesh = mesh;
            ring.GetComponent<MeshRenderer>().sharedMaterial = material;
            return ring;
        }

        /// <summary>A stand-in for a player: a post, a sacking body and head, 1.8 m tall, at `at` facing `facing`.</summary>
        public static GameObject Dummy(string name, Vector3 at, float facing)
        {
            var root = new GameObject(name).transform;
            root.SetPositionAndRotation(at, Quaternion.Euler(0f, facing, 0f));
            Material sacking = Plain(name + "_sacking", new Color(0.2f, 0.14f, 0.075f)), wood = Plain(name + "_wood", new Color(0.075f, 0.05f, 0.028f));
            Place(Primitive(PrimitiveType.Cylinder, name + "_post", wood), root, new Vector3(0f, 0.6f, 0f), new Vector3(0.09f, 0.6f, 0.09f));
            Place(Primitive(PrimitiveType.Capsule, name + "_body", sacking), root, new Vector3(0f, 1.15f, 0f), new Vector3(0.5f, 0.42f, 0.34f));
            Place(Primitive(PrimitiveType.Sphere, name + "_head", sacking), root, new Vector3(0f, 1.62f, 0f), new Vector3(0.26f, 0.28f, 0.26f));
            Place(Primitive(PrimitiveType.Cylinder, name + "_arms", wood), root, new Vector3(0f, 1.3f, 0f), new Vector3(0.06f, 0.4f, 0.06f)).localRotation = Quaternion.Euler(0f, 0f, 90f);
            return root.gameObject;
        }

        private static Transform Place(GameObject part, Transform parent, Vector3 at, Vector3 size)
        {
            part.transform.SetParent(parent, false);
            part.transform.localPosition = at;
            part.transform.localScale = size;
            return part.transform;
        }
    }
}
