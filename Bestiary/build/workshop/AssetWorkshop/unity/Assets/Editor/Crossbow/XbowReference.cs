using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Workshop.Crossbow
{
    /// <summary>
    /// The game's Skeleton from the reference export, for authoring the crossbowman's clips on its own skeleton and for
    /// previews: the game prefab Skeleton.prefab itself, so its Visual is exactly the game's (the Animator on
    /// _skeleton_base with the skeleton's humanoid avatar, the 0.95 base and the armature at 100), dressed on a Standard
    /// material with the skeleton's own textures, and the clips its controller plays. Everything lands in
    /// Assets/Reference/Skeleton, which never goes into a bundle; the mod builds the crossbowman from the running game's
    /// Skeleton instead.
    /// </summary>
    public static class XbowReference
    {
        public const string Subfolder = "Skeleton";
        private const string Game = "Characters/Skeleton/";
        private const string Model = Game + "model/Model/";
        private const string Textures = Game + "model/Texture/";
        private static readonly string[] Parts = { Model + "_skeleton_baseAvatar.asset", Model + "Skeleton.asset" };

        /// <summary>The Skeleton controller's states (Skeleton_animator.controller) and the clips they play.</summary>
        private static readonly Dictionary<string, string> Clips = new Dictionary<string, string>
        {
            { "Idle", "Characters/Player/model/old_PlayerCharacter/Idle" },
            { "Walk", "3rd party/RPG Character Animation Pack/Animations/Shield/Shield-Walk-Injured" },
            { "Run", "3rd party/RPG Character Animation Pack/Animations/Shield/Shield-Run-Forward" },
            { "Aim", "Characters/Draugr/model/Bow Aim Idle 01" },        // the archer's bow_idle state
            { "Recoil", "Characters/Draugr/model/Bow Aim Recoil" },      // the archer's attack_bow state
        };

        /// <summary>The Skeleton's Visual at the origin facing +Z, dressed, with its humanoid Animator and nothing else of the game's.</summary>
        public static GameObject Skeleton()
        {
            foreach (string part in Parts)
                ReferenceAssets.Import(part, Subfolder);
            string prefab = ReferenceAssets.Import(Game + "Skeleton.prefab", Subfolder);
            var game = (GameObject)Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(prefab));
            Transform visual = game.transform.Find("Visual");
            visual.SetParent(null, false);
            Object.DestroyImmediate(game);
            visual.name = "Skeleton";
            visual.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            Strip(visual.gameObject);
            Dress(visual.gameObject);
            return visual.gameObject;
        }

        /// <summary>The GameObject with the Animator (the game's _skeleton_base).</summary>
        public static Animator Animator(GameObject skeleton) => skeleton.GetComponentInChildren<Animator>(true);

        /// <summary>One of the controller's clips by state (Idle, Walk, Run, Aim, Recoil).</summary>
        public static AnimationClip Clip(string state) =>
            AssetDatabase.LoadAssetAtPath<AnimationClip>(ReferenceAssets.Import(Clips[state] + ".anim", Subfolder));

        public static Transform Bone(GameObject skeleton, string name) =>
            skeleton.GetComponentsInChildren<Transform>(true).First(t => t.name == name);

        /// <summary>The game's scripts are missing here; its colliders, LODs and smoke would only get in the way.</summary>
        private static void Strip(GameObject skeleton)
        {
            foreach (var child in skeleton.GetComponentsInChildren<Transform>(true))
                GameObjectUtility.RemoveMonoBehavioursWithMissingScript(child.gameObject);
            foreach (var collider in skeleton.GetComponentsInChildren<Collider>(true))
                Object.DestroyImmediate(collider);
            foreach (var particles in skeleton.GetComponentsInChildren<ParticleSystem>(true))
                Object.DestroyImmediate(particles.gameObject);
            Object.DestroyImmediate(skeleton.GetComponent<LODGroup>());
            foreach (var skin in skeleton.GetComponentsInChildren<SkinnedMeshRenderer>())
                skin.forceMatrixRecalculationPerRender = true;   // batch mode skins once and keeps it otherwise
        }

        private static void Dress(GameObject skeleton)
        {
            var bone = new Material(Shader.Find("Standard")) { name = "skeleton_preview" };
            bone.mainTexture = ReferenceAssets.Texture(Textures + "Skeleton_d.tga", false);
            bone.SetTexture("_BumpMap", ReferenceAssets.Texture(Textures + "Skeleton_n.tga", true));
            bone.EnableKeyword("_NORMALMAP");
            bone.SetFloat("_Glossiness", 0.12f);
            var glow = new Color(1f, 0.35f, 0.1f);
            var eye = new Material(Shader.Find("Standard")) { name = "skeleton_eye_preview", color = glow };
            eye.EnableKeyword("_EMISSION");
            eye.SetColor("_EmissionColor", glow * 2f);
            foreach (var renderer in skeleton.GetComponentsInChildren<Renderer>(true))
                renderer.sharedMaterials = renderer.sharedMaterials.Select(_ => renderer is SkinnedMeshRenderer ? bone : eye).ToArray();
        }
    }
}
