// Portions Copyright (C) 2026 Gerald Glasgow. EliteCreaturesPack 0.7.2 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Changed by expDARE, 2026: Bestiary namespace, owned Crucible settings, rebuilt-only bundles and lifecycle adapters.
// SPDX-License-Identifier: GPL-3.0-or-later
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ExtraPotions.Bestiary.Runtime.Native.Headsman
{
    /// <summary>
    /// A skinned body split into its separate bones (the game's Skeleton is one mesh of 49 of them), found once per mesh:
    /// each bone's triangles, the rig bone that carries it and its middle in that rig bone's space. Triangles are joined
    /// where they share a corner's position, not its index (the mesh splits corners along its UV seams). The order a
    /// body's bones rise in, feet up, is read from the pose it stands in (<see cref="FeetUp"/>).
    /// </summary>
    public sealed class HeadsmanBones
    {
        public sealed class Bone
        {
            public int[] Triangles = null!, Corners = null!;
            public int Carrier;
            public Vector3 Middle;
        }

        private static readonly Dictionary<Mesh, HeadsmanBones> split = new Dictionary<Mesh, HeadsmanBones>();

        public readonly Bone[] Bones;
        private readonly Vector3[] corners;
        private readonly Matrix4x4[] binds;

        private HeadsmanBones(Mesh mesh)
        {
            (corners, binds) = (mesh.vertices, mesh.bindposes);
            int[] triangles = mesh.triangles;
            BoneWeight[] weights = mesh.boneWeights;
            Bones = Islands(triangles).Select(faces => Make(faces, triangles, weights)).ToArray();
        }

        public static HeadsmanBones Of(Mesh mesh)
        {
            if (!split.TryGetValue(mesh, out HeadsmanBones bones))
            {
                bones = split[mesh] = new HeadsmanBones(mesh);
            }
            return bones;
        }

        /// <summary>The mesh's triangles grouped into separate parts (union-find over shared corner positions).</summary>
        private IEnumerable<List<int>> Islands(int[] triangles)
        {
            int[] parent = Enumerable.Range(0, triangles.Length / 3).ToArray();
            int Root(int i) => parent[i] == i ? i : parent[i] = Root(parent[i]);
            var owner = new Dictionary<Vector3, int>();
            for (int t = 0; t < parent.Length; t++)
            {
                for (int k = 0; k < 3; k++)
                {
                    Vector3 at = corners[triangles[3 * t + k]];
                    parent[Root(owner.TryGetValue(at, out int other) ? other : owner[at] = t)] = Root(t);
                }
            }
            return Enumerable.Range(0, parent.Length).GroupBy(Root).Select(g => g.ToList());
        }

        /// <summary>One bone: its triangles, its corners, the rig bone weighing most on them and its middle there.</summary>
        private Bone Make(List<int> faces, int[] triangles, BoneWeight[] weights)
        {
            int[] indices = faces.SelectMany(t => new[] { triangles[3 * t], triangles[3 * t + 1], triangles[3 * t + 2] }).ToArray();
            int[] used = indices.Distinct().ToArray();
            int carrier = weights.Length == 0 ? 0 : used.GroupBy(v => weights[v].boneIndex0).OrderByDescending(g => g.Count()).First().Key;
            Vector3 middle = used.Aggregate(Vector3.zero, (sum, v) => sum + corners[v]) / used.Length;
            return new Bone { Triangles = indices, Corners = used, Carrier = carrier, Middle = binds.Length > carrier ? binds[carrier].MultiplyPoint3x4(middle) : middle };
        }

        /// <summary>The bones' places in the rising, lowest first, as the body stands posed on `rig` now.</summary>
        public int[] FeetUp(Transform[] rig)
        {
            float Lowest(Bone bone)
            {
                Matrix4x4 pose = Pose(rig, bone.Carrier);
                return bone.Corners.Min(v => pose.MultiplyPoint3x4(corners[v]).y);
            }
            return Enumerable.Range(0, Bones.Length).OrderBy(i => Lowest(Bones[i])).ToArray();
        }

        /// <summary>A bone's middle in the world, where its rig bone is now.</summary>
        public Vector3 Where(Transform[] rig, int bone)
        {
            int carrier = Bones[bone].Carrier;
            return carrier < rig.Length && rig[carrier] != null ? rig[carrier].TransformPoint(Bones[bone].Middle) : Vector3.zero;
        }

        private Matrix4x4 Pose(Transform[] rig, int carrier) =>
            carrier < rig.Length && rig[carrier] != null && carrier < binds.Length ? rig[carrier].localToWorldMatrix * binds[carrier] : Matrix4x4.identity;
    }
}
