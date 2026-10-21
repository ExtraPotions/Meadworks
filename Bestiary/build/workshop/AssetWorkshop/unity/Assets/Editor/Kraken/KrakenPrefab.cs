using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Workshop.Kraken
{
    /// <summary>
    /// Builds a kraken prefab from its model: the root named after the asset, the bones and markers as plain transforms
    /// at their rest positions with identity rotations (so a bone's own axes are the prefab's), and one child per mesh
    /// with a SkinnedMeshRenderer whose root bone is the first bone. Nothing else: no animator, colliders or scripts;
    /// the mod moves the bones itself.
    /// </summary>
    public static class KrakenPrefab
    {
        public static string Build(string folder, KrakenModel model, Bounds localBounds)
        {
            var root = new GameObject(model.asset);
            var joints = new Dictionary<string, Transform>();
            foreach (var joint in model.bones.Concat(model.markers))
                joints[joint.name] = Joint(root.transform, joints, joint);
            Transform[] bones = model.bones.Select(b => joints[b.name]).ToArray();
            foreach (var part in model.parts)
                Skin(folder, model, part, root.transform, bones, localBounds);
            string path = folder + "/" + model.asset + ".prefab";
            PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            Log.Info($"prefab {path}: {model.bones.Length} bones, {model.markers.Length} markers, {model.parts.Length} meshes");
            return path;
        }

        private static Transform Joint(Transform root, Dictionary<string, Transform> joints, KrakenJoint joint)
        {
            if (joints.ContainsKey(joint.name))
                throw new System.InvalidOperationException("two joints named " + joint.name);
            var t = new GameObject(joint.name).transform;
            t.SetParent(string.IsNullOrEmpty(joint.parent) ? root : joints[joint.parent], false);
            t.SetPositionAndRotation(root.TransformPoint(joint.Position), root.rotation);
            t.localScale = Vector3.one;
            return t;
        }

        private static void Skin(string folder, KrakenModel model, KrakenPart part, Transform root, Transform[] bones, Bounds bounds)
        {
            var holder = new GameObject(part.name).transform;
            holder.SetParent(root, false);
            var skin = holder.gameObject.AddComponent<SkinnedMeshRenderer>();
            skin.sharedMesh = KrakenAssets.Mesh(folder, model.asset, part, bones, holder);
            skin.bones = bones;
            skin.rootBone = bones[0];
            skin.sharedMaterial = KrakenAssets.Material(folder, part);
            skin.localBounds = bounds;
            skin.updateWhenOffscreen = false;
            skin.quality = SkinQuality.Auto;
            skin.skinnedMotionVectors = false;
        }
    }
}
