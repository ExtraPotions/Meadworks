using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Workshop
{
    /// <summary>
    /// Saves &lt;asset&gt;.prefab: the imported rig and parts with an Animator on the root running the creature's
    /// controller, root motion on (the game applies it during attacks).
    /// </summary>
    public static class CreaturePrefab
    {
        public static string Build(string folder, CreatureManifest info, AnimatorController controller)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(folder + "/" + info.fbx);
            var root = (GameObject)PrefabUtility.InstantiatePrefab(model);
            PrefabUtility.UnpackPrefabInstance(root, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            root.name = info.asset;
            var animator = root.GetComponent<Animator>();
            if (animator == null)
                animator = root.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = true;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            string summary = $"{root.GetComponentsInChildren<Renderer>().Length} renderers, avatar {animator.avatar != null}";
            CheckFacing(root.transform, "eyes");
            string path = folder + "/" + info.asset + ".prefab";
            PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            Log.Info($"prefab {path}: {summary}");
            return path;
        }

        /// <summary>
        /// A bone that sits at the creature's front in Blender must end up on the +Z side in Unity, where the game's
        /// creatures face; otherwise it walks and lunges backwards.
        /// </summary>
        private static void CheckFacing(Transform root, string frontBone)
        {
            var bone = Find(root, frontBone);
            float z = root.InverseTransformPoint(bone.position).z;
            Log.Info($"facing: {frontBone} at z {z:F2} ({(z > 0 ? "front is +Z, OK" : "BACKWARDS")})");
            if (z <= 0)
                throw new System.InvalidOperationException("the creature faces -Z; check the FBX export axes");
        }

        /// <summary>A transform anywhere below root, by name (bones keep their Blender names).</summary>
        public static Transform Find(Transform root, string name)
        {
            foreach (var child in root.GetComponentsInChildren<Transform>(true))
                if (child.name == name)
                    return child;
            return null;
        }
    }
}
