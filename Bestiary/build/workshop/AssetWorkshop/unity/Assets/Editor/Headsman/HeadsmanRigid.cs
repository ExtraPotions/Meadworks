using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Workshop.Headsman
{
    /// <summary>
    /// Records rigid renderers for Blender (the axes, shards, rocks, dummies): each one's mesh once in its own frame and
    /// its world matrix and visibility every frame, written as rigid.json. Everything in Blender's axes: vertices C v,
    /// matrices C M C^-1, with C the Unity-to-Blender swap (x, y, z) -> (-x, -z, y), triangles rewound for the mirror.
    /// Skinned meshes go through <see cref="Crossbow.XbowCache"/> instead.
    /// </summary>
    public sealed class HeadsmanRigid
    {
        [Serializable]
        public sealed class PartData
        {
            public string name, texture;
            public float[] vertices, uvs, color, matrices;
            public int[] triangles;
            public bool glow;
            public bool[] active;
        }

        [Serializable]
        public sealed class RigidData
        {
            public PartData[] parts;
        }

        private static readonly Matrix4x4 ToBlender = new Matrix4x4(
            new Vector4(-1f, 0f, 0f, 0f), new Vector4(0f, 0f, 1f, 0f), new Vector4(0f, -1f, 0f, 0f), new Vector4(0f, 0f, 0f, 1f));

        private readonly List<(Renderer renderer, string name, List<float> matrices, List<bool> active)> parts =
            new List<(Renderer, string, List<float>, List<bool>)>();

        public void Add(Renderer renderer, string name) => parts.Add((renderer, name, new List<float>(), new List<bool>()));

        public void Capture()
        {
            foreach (var (renderer, _, matrices, active) in parts)
            {
                Matrix4x4 m = ToBlender * renderer.transform.localToWorldMatrix * ToBlender.inverse;
                for (int row = 0; row < 4; row++)
                    for (int column = 0; column < 4; column++)
                        matrices.Add(m[row, column]);
                active.Add(renderer.enabled && renderer.gameObject.activeInHierarchy);
            }
        }

        public void Write(string folder)
        {
            var data = new RigidData { parts = parts.Select(Part).ToArray() };
            File.WriteAllText(Path.Combine(folder, "rigid.json"), JsonUtility.ToJson(data));
            Log.Info($"rigid parts: {parts.Count}, {(parts.Count == 0 ? 0 : parts[0].active.Count)} frames");
        }

        private static PartData Part((Renderer renderer, string name, List<float> matrices, List<bool> active) part)
        {
            Mesh mesh = part.renderer.GetComponent<MeshFilter>().sharedMesh;
            int[] tris = mesh.triangles;
            for (int i = 0; i < tris.Length; i += 3)
                (tris[i + 1], tris[i + 2]) = (tris[i + 2], tris[i + 1]);
            Material material = part.renderer.sharedMaterial;
            string texture = material.mainTexture == null ? "" : AssetDatabase.GetAssetPath(material.mainTexture);
            return new PartData
            {
                name = part.name, triangles = tris, matrices = part.matrices.ToArray(), active = part.active.ToArray(),
                vertices = mesh.vertices.SelectMany(v => new[] { -v.x, -v.z, v.y }).ToArray(),
                uvs = mesh.uv.SelectMany(uv => new[] { uv.x, uv.y }).ToArray(),
                texture = string.IsNullOrEmpty(texture) ? "" : Path.GetFullPath(texture).Replace('\\', '/'),
                color = new[] { material.color.r, material.color.g, material.color.b },
                glow = material.IsKeywordEnabled("_EMISSION"),
            };
        }
    }
}
