using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Workshop.Kraken
{
    /// <summary>
    /// Fails the build when a prefab breaks what the mod's code assumes: a bone or marker missing, misplaced or under the
    /// wrong parent, a rest rotation that is not identity, the tentacle not lying along +Z from 0 to 12.8 with its
    /// suckers and pale underside on -Y, the beak and kh_mouth not on +Z, a SkinnedMeshRenderer without bones or
    /// bindposes, or skinning that does not follow the bones (each prefab is posed and its mesh baked to see).
    /// </summary>
    public static class KrakenCheck
    {
        private static readonly List<string> Problems = new List<string>();

        public static void Run(GameObject tentacle, GameObject head, string folder)
        {
            Problems.Clear();
            Joints(tentacle, KrakenContract.TentacleJoints());
            Joints(head, KrakenContract.HeadJoints());
            Skins(tentacle, new[] { ("mesh_tentacle", "ecp_kraken_tentacle") }, "kt_00", 2);
            Skins(head, new[] { ("mesh_skin", "ecp_kraken_skin"), ("mesh_eyes", "ecp_kraken_eye") }, "kh_root", 4);
            KrakenTentacleCheck.Run(tentacle, folder, Fail);
            KrakenHeadCheck.Run(head, Fail);
            if (Problems.Count > 0)
                throw new InvalidOperationException("the kraken breaks its contract:\n  " + string.Join("\n  ", Problems));
            Log.Info("contract check: OK (bones, markers, rest positions and rotations, axes, suckers on -Y, skinning)");
        }

        public static void Fail(string problem)
        {
            Problems.Add(problem);
            Log.Error("check: " + problem);
        }

        public static Transform Find(GameObject root, string name)
        {
            var found = root.GetComponentsInChildren<Transform>(true).Where(t => t.name == name).ToArray();
            return found.Length == 1 ? found[0] : null;
        }

        private static void Joints(GameObject root, IEnumerable<(string name, string parent, Vector3 position, float tolerance)> joints)
        {
            foreach (var (name, parent, position, tolerance) in joints)
            {
                Transform t = Find(root, name);
                if (t == null)
                {
                    Fail($"{root.name}: no single transform named {name}");
                    continue;
                }
                string actual = t.parent == root.transform ? "" : t.parent.name;
                if (parent != null && actual != parent)
                    Fail($"{root.name}: {name} is under '{actual}', expected '{parent}'");
                Vector3 at = root.transform.InverseTransformPoint(t.position);
                if (tolerance >= 0 && Vector3.Distance(at, position) > tolerance)
                    Fail($"{root.name}: {name} at {at:F4}, expected {position:F4} within {tolerance} m");
                if (Quaternion.Angle(t.rotation, root.transform.rotation) > 0.01f)
                    Fail($"{root.name}: {name} rest rotation is {t.rotation.eulerAngles:F2}, expected identity");
            }
        }

        private static void Skins(GameObject root, (string name, string material)[] expected, string rootBone, int maxInfluences)
        {
            var skins = root.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            if (skins.Length != expected.Length)
                Fail($"{root.name}: {skins.Length} SkinnedMeshRenderers, expected {expected.Length}");
            foreach (var (name, material) in expected)
            {
                var skin = skins.FirstOrDefault(s => s.name == name);
                if (skin == null)
                {
                    Fail($"{root.name}: no SkinnedMeshRenderer on {name}");
                    continue;
                }
                Skin(root, skin, material, rootBone, maxInfluences);
            }
        }

        private static void Skin(GameObject root, SkinnedMeshRenderer skin, string material, string rootBone, int maxInfluences)
        {
            Mesh mesh = skin.sharedMesh;
            string where = $"{root.name}/{skin.name}";
            if (mesh == null || skin.bones.Length == 0 || mesh.bindposes.Length != skin.bones.Length || skin.bones.Any(b => b == null))
                Fail($"{where}: missing mesh, bones or bindposes ({skin.bones.Length} bones, {mesh?.bindposes.Length} bindposes)");
            if (skin.rootBone == null || skin.rootBone.name != rootBone)
                Fail($"{where}: root bone {skin.rootBone?.name}, expected {rootBone}");
            if (skin.sharedMaterial == null || skin.sharedMaterial.name != material)
                Fail($"{where}: material {skin.sharedMaterial?.name}, expected {material}");
            if (mesh == null)
                return;
            Bounds cull = skin.localBounds, rest = mesh.bounds;   // both about the root bone, which rests at the origin
            if (!cull.Contains(rest.min) || !cull.Contains(rest.max))
                Fail($"{where}: culling bounds {cull.min:F1}..{cull.max:F1} do not hold the mesh {rest.min:F1}..{rest.max:F1}");
            foreach (var w in mesh.boneWeights)
            {
                float[] ws = { w.weight0, w.weight1, w.weight2, w.weight3 };
                int[] ids = { w.boneIndex0, w.boneIndex1, w.boneIndex2, w.boneIndex3 };
                if (Mathf.Abs(ws.Sum() - 1f) > 0.01f || ids.Any(i => i < 0 || i >= skin.bones.Length) || ws.Count(x => x > 0) > maxInfluences)
                {
                    Fail($"{where}: a vertex has bad weights ({string.Join(",", ids)} / {string.Join(",", ws)})");
                    return;
                }
            }
        }

        /// <summary>The skinned mesh as it stands now, in the prefab root's space (the mesh holders sit at the root).</summary>
        public static Vector3[] Baked(SkinnedMeshRenderer skin)
        {
            skin.forceMatrixRecalculationPerRender = true;
            var baked = new Mesh();
            skin.BakeMesh(baked, true);
            Vector3[] vertices = baked.vertices;
            UnityEngine.Object.DestroyImmediate(baked);
            return vertices;
        }

        public static Texture2D ReadTexture(string path)
        {
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            texture.LoadImage(File.ReadAllBytes(path));
            return texture;
        }
    }
}
