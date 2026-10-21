using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Workshop.GameRig
{
    /// <summary>
    /// Builds the body's prefab from the contract: the game's own transforms from Visual down, each at exactly the local
    /// position, rotation and scale the game prefab has (so the game's avatar and clips drive it as they drive the
    /// game creature), the new sockets under their game bones, and a SkinnedMeshRenderer at the game body renderer's
    /// place, bound to the bones by name in the contract's bone order (the game body's own when boneOrder is "game").
    /// Plain transforms and a renderer only: no Animator, no scripts; the mod puts the mesh on the game creature.
    /// </summary>
    public static class GameRigPrefab
    {
        public static string Build(string folder, GameRigModel model)
        {
            var root = new GameObject(model.asset);
            try
            {
                Dictionary<string, Transform> byPath = Hierarchy(root.transform, model);
                Transform skin = byPath[model.renderer];
                Transform[] bones = Bones(root.transform, model);
                var renderer = skin.gameObject.AddComponent<SkinnedMeshRenderer>();
                renderer.sharedMesh = GameRigAssets.Mesh(folder, model, bones, skin);
                renderer.bones = bones;
                renderer.rootBone = Named(root.transform, model.rootBone);
                renderer.sharedMaterials = new[] { GameRigAssets.Material(folder, model.material) };
                renderer.localBounds = RestBounds(renderer);
                string path = folder + "/" + model.asset + ".prefab";
                PrefabUtility.SaveAsPrefabAsset(root, path);
                return path;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        /// <summary>Every transform of the contract under the root, parents first, at its exact local values.</summary>
        public static Dictionary<string, Transform> Hierarchy(Transform root, GameRigModel model)
        {
            var byPath = new Dictionary<string, Transform>();
            foreach (GameRigTransform entry in model.transforms)
            {
                Transform parent = entry.parent == "" ? root : byPath[entry.parent];
                var node = new GameObject(entry.name).transform;
                node.SetParent(parent, false);
                node.localPosition = entry.Position;
                node.localRotation = entry.Rotation;
                node.localScale = entry.Scale;
                byPath.Add(entry.path, node);
            }
            return byPath;
        }

        /// <summary>The transforms the mesh's bone indices name, in the contract's order.</summary>
        public static Transform[] Bones(Transform root, GameRigModel model) =>
            model.bones.Select(name => Named(root, name)).ToArray();

        /// <summary>The transform with this name below root; the names the game looks bones up by must be unique.</summary>
        public static Transform Named(Transform root, string name)
        {
            Transform[] found = root.GetComponentsInChildren<Transform>(true).Where(t => t.name == name).ToArray();
            if (found.Length != 1)
                throw new InvalidOperationException($"{found.Length} transforms named {name} under {root.name}");
            return found[0];
        }

        /// <summary>The rest pose's box round the mesh in the root bone's space, grown by half for the poses clips reach.</summary>
        public static Bounds RestBounds(SkinnedMeshRenderer renderer)
        {
            Matrix4x4 into = renderer.rootBone.worldToLocalMatrix * renderer.transform.localToWorldMatrix;
            Vector3[] vertices = renderer.sharedMesh.vertices;
            var bounds = new Bounds(into.MultiplyPoint3x4(vertices[0]), Vector3.zero);
            foreach (Vector3 v in vertices)
                bounds.Encapsulate(into.MultiplyPoint3x4(v));
            bounds.Expand(bounds.size.magnitude * 0.5f);
            return bounds;
        }
    }
}
