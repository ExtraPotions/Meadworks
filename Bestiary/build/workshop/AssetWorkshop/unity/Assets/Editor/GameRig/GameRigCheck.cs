using System.Linq;
using UnityEngine;

namespace Workshop.GameRig
{
    /// <summary>
    /// The contract, checked on the built prefab (an instance of it) and against the game's own creature as Unity itself
    /// imports it from the reference export, independently of the Blender side:
    /// - every transform at its path with the contract's exact local values, and where Blender's armature had it (1 mm);
    /// - every game transform (Visual's chain, bones, sockets, ends, the body renderer) with the game prefab's own local
    ///   position, rotation and scale, and the same place in the root's space (0.1 mm);
    /// - the mesh's bone order: the game body's own when the contract says "game"; the root bone the game's;
    /// - the new sockets under their game bones, carrying no weight;
    /// - the skin: bind poses one per bone, at most four weights a vertex summing to 1, indices in range;
    /// - facing and placement: the body parts sit round the same bones as the game body's (per-bone centroids), and
    ///   the toes and jaw lie in front (+Z) of the foot and head, as the game body's do.
    /// </summary>
    public static class GameRigCheck
    {
        private const float Metres = 0.001f, Tight = 0.0001f;

        public static void Contract(GameObject ours, GameRigModel model)
        {
            float local = model.transforms.Max(t => LocalError(ours.transform.Find(t.path), t));
            GameRigReport.Check(local < 1e-6f, $"contract: {model.transforms.Length} transforms at their exact local values (worst {local:0.0e0})");
            float rest = model.rest.Max(r => Vector3.Distance(GameRigPrefab.Named(ours.transform, r.name).position, r.Position));
            GameRigReport.Check(rest < Metres, $"contract: every bone and socket where Blender's armature has it (worst {rest * 1000:0.000} mm)");
            var skin = ours.GetComponentInChildren<SkinnedMeshRenderer>();
            Skin(skin, model);
            foreach (string socket in model.sockets)
                GameRigReport.Check(!skin.bones.Any(b => b.name == socket), $"socket {socket}: under {GameRigPrefab.Named(ours.transform, socket).parent.name}, no weight");
        }

        public static void Game(GameObject ours, GameObject game, GameRigModel model)
        {
            GameRigTransform[] theirs = model.transforms.Where(t => t.kind != "new").ToArray();
            float local = 0f, world = 0f;
            int missing = 0;
            foreach (GameRigTransform t in theirs)
            {
                Transform g = game.transform.Find(t.path);
                if (g == null) { missing++; continue; }
                local = Mathf.Max(local, LocalError(g, t));
                world = Mathf.Max(world, Vector3.Distance(g.position, ours.transform.Find(t.path).position));
            }
            GameRigReport.Check(missing == 0, $"game prefab: all {theirs.Length} of the game's transforms found by path ({missing} missing)");
            GameRigReport.Check(local < 1e-5f, $"game prefab: local position, rotation and scale equal the game's (worst {local:0.0e0})");
            GameRigReport.Check(world < Tight, $"game prefab: every transform in the same place as the game's (worst {world * 1000:0.0000} mm)");
            Order(ours, game, model);
        }

        private static void Order(GameObject ours, GameObject game, GameRigModel model)
        {
            SkinnedMeshRenderer mine = ours.GetComponentInChildren<SkinnedMeshRenderer>();
            SkinnedMeshRenderer theirs = GameRigStaging.Body(game, model.renderer);
            string[] a = mine.bones.Select(b => b.name).ToArray(), b2 = theirs.bones.Select(b => b.name).ToArray();
            if (model.boneOrder == "game")
                GameRigReport.Check(a.SequenceEqual(b2), $"bone order: the game body's own ({a.Length} bones), so the game's attach_skin gear fits");
            else
                GameRigReport.Check(a.All(b2.Contains), $"bone order: own ({a.Length} bones), all of them the game body's");
            GameRigReport.Check(mine.rootBone.name == theirs.rootBone.name, $"root bone: {mine.rootBone.name} (the game's {theirs.rootBone.name})");
        }

        private static void Skin(SkinnedMeshRenderer skin, GameRigModel model)
        {
            Mesh mesh = skin.sharedMesh;
            BoneWeight[] weights = mesh.boneWeights;
            float worstSum = weights.Max(w => Mathf.Abs(w.weight0 + w.weight1 + w.weight2 + w.weight3 - 1f));
            int bad = weights.Count(w => new[] { w.boneIndex0, w.boneIndex1, w.boneIndex2, w.boneIndex3 }.Any(i => i < 0 || i >= skin.bones.Length));
            GameRigReport.Check(mesh.bindposes.Length == skin.bones.Length && skin.bones.Length == model.bones.Length,
                $"skin: {skin.bones.Length} bones, {mesh.bindposes.Length} bind poses");
            GameRigReport.Check(worstSum < 0.002f && bad == 0, $"skin: {mesh.vertexCount} vertices, weights sum to 1 (worst {worstSum:0.0000}), {bad} bad indices");
            GameRigReport.Check(mesh.triangles.Length / 3 == model.mesh.triangles.Length / 3,
                $"mesh: {mesh.triangles.Length / 3} triangles, {mesh.vertexCount} vertices, bounds {mesh.bounds.size}");
        }

        /// <summary>The worst difference between a transform's local values and the contract's (rotation in degrees).</summary>
        public static float LocalError(Transform t, GameRigTransform entry)
        {
            if (t == null)
                return float.PositiveInfinity;
            float position = (t.localPosition - entry.Position).magnitude;
            float rotation = Quaternion.Angle(t.localRotation, entry.Rotation.normalized);
            float scale = (t.localScale - entry.Scale).magnitude / Mathf.Max(1f, entry.Scale.magnitude);
            return Mathf.Max(position, rotation * 0.001f, scale);
        }
    }
}
