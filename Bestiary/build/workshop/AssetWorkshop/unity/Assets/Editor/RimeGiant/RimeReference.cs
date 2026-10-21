using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Workshop.RimeGiant
{
    /// <summary>
    /// The game's forest Troll from the reference export, for measuring the Rime Giant's kit on its own skeleton and for
    /// previews: the game prefab Troll.prefab itself, so its Visual is exactly the game's (the Animator with the troll's
    /// avatar, the armature at the game's scale of 130, the body and hair meshes the game uses), dressed on a Standard
    /// material tinted the pale icy blue-grey the mod gives it, and the clips its controller plays. Everything lands in
    /// Assets/Reference/Troll, which never goes into a bundle; the mod builds the giant from the running game's Troll.
    /// </summary>
    public static class RimeReference
    {
        public const string Subfolder = "Troll";
        private const string Game = "Characters/Troll/";
        private const string Model = Game + "model/";
        private const string Textures = Model + "material/";
        private static readonly string[] Parts =
            { "troll_base1/_troll_baseAvatar.asset", "troll_base2/Body.asset", "troll_base2/Hair_0.asset" };

        /// <summary>The controller's states (troll_animator.controller) and the clips they play.</summary>
        private static readonly Dictionary<string, string> Clips = new Dictionary<string, string>
        {
            { "Idle", "troll_base2/Idle_Breathing" }, { "Walk", "New_Walk/Walk" }, { "Run", "New_Walk/Walk Angry" },
            { "Punch", "troll_base1/Zombie Attack2" }, { "Slam", "troll_base1/Zombie Attack" }, { "Throw", "troll_base1/Throw" },
            { "Sleeping", "New_Walk/Sleeping" }, { "Wakeup", "New_Walk/Wakeup" },
        };

        /// <summary>The Troll's Visual at the origin facing +Z, tinted, with its humanoid Animator and nothing else of the game's.</summary>
        public static GameObject Troll()
        {
            foreach (string part in Parts)
                ReferenceAssets.Import(Model + part, Subfolder);
            string prefab = ReferenceAssets.Import(Game + "Troll.prefab", Subfolder);
            var game = (GameObject)Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(prefab));
            Transform visual = game.transform.Find("Visual");
            visual.SetParent(null, false);
            Object.DestroyImmediate(game);
            visual.name = "Troll";
            visual.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            Strip(visual.gameObject);
            Dress(visual.gameObject);
            return visual.gameObject;
        }

        /// <summary>
        /// The developers' unused ice troll (Characters/FrostTroll), for a look at how they dressed a troll in rock and
        /// ice: its model prefab with the armature at the game Troll's 130, on its own textures. Preview only.
        /// </summary>
        public static GameObject FrostTroll()
        {
            const string model = "Characters/FrostTroll/model/";
            foreach (string part in new[] { "Body.asset", "Hair.asset", "TreeBundle.asset", "FrostTrollAvatar.asset" })
                ReferenceAssets.Import(model + part, "FrostTroll");
            string prefab = ReferenceAssets.Import(model + "FrostTroll.prefab", "FrostTroll");
            var frost = (GameObject)Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(prefab));
            Bone(frost, "Armature").localScale = Vector3.one * 130f;
            frost.GetComponent<Animator>().Rebind();
            var material = new Material(Shader.Find("Standard")) { name = "frosttroll_preview" };
            material.mainTexture = ReferenceAssets.Texture(model + "frosttroll_d.png", false);
            material.SetFloat("_Mode", 1f);
            material.SetFloat("_Cutoff", 0.4f);
            material.EnableKeyword("_ALPHATEST_ON");
            foreach (var renderer in frost.GetComponentsInChildren<Renderer>(true))
            {
                renderer.sharedMaterials = renderer.sharedMaterials.Select(_ => material).ToArray();
                if (renderer is SkinnedMeshRenderer skin)
                    skin.forceMatrixRecalculationPerRender = true;
            }
            return frost;
        }

        /// <summary>One of the controller's clips by state (Idle, Walk, Run, Punch, Slam, Throw, Sleeping, Wakeup).</summary>
        public static AnimationClip Clip(string state) =>
            AssetDatabase.LoadAssetAtPath<AnimationClip>(ReferenceAssets.Import(Model + Clips[state] + ".anim", Subfolder));

        public static Transform Bone(GameObject troll, string name) =>
            troll.GetComponentsInChildren<Transform>(true).First(t => t.name == name);

        /// <summary>The game's scripts are missing here and its colliders would only get in the way.</summary>
        private static void Strip(GameObject troll)
        {
            foreach (var child in troll.GetComponentsInChildren<Transform>(true))
                GameObjectUtility.RemoveMonoBehavioursWithMissingScript(child.gameObject);
            foreach (var collider in troll.GetComponentsInChildren<Collider>(true))
                Object.DestroyImmediate(collider);
            Object.DestroyImmediate(troll.GetComponent<LODGroup>());
            foreach (var skin in troll.GetComponentsInChildren<SkinnedMeshRenderer>())
                skin.forceMatrixRecalculationPerRender = true;   // batch mode skins once and keeps it otherwise
        }

        private static void Dress(GameObject troll)
        {
            var material = new Material(Shader.Find("Standard")) { name = "rime_troll_preview", mainTexture = Tinted() };
            material.SetTexture("_BumpMap", ReferenceAssets.Texture(Textures + "troll_n.png", true));
            material.EnableKeyword("_NORMALMAP");
            material.SetFloat("_Glossiness", 0.12f);
            material.SetFloat("_Mode", 1f);   // cutout: the hair is alpha tested in the game (troll.mat cuts at 0.29)
            material.SetFloat("_Cutoff", 0.29f);
            material.EnableKeyword("_ALPHATEST_ON");
            material.renderQueue = 2450;
            foreach (var renderer in troll.GetComponentsInChildren<Renderer>(true))
                renderer.sharedMaterials = renderer.sharedMaterials.Select(_ => material).ToArray();
        }

        /// <summary>
        /// The troll's diffuse as the mod tints it: saturation down by 0.55, value up by 0.25, a touch bluer. Built in
        /// memory from the reference file, so it is readable and never becomes an asset.
        /// </summary>
        private static Texture2D Tinted()
        {
            string path = ReferenceAssets.Import(Textures + "troll_diffuse.png", Subfolder);
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, name = "rime_troll_tinted" };
            texture.LoadImage(File.ReadAllBytes(path));
            Color[] pixels = texture.GetPixels();
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = Tint(pixels[i]);
            texture.SetPixels(pixels);
            texture.Apply();
            return texture;
        }

        public static Color Tint(Color colour)
        {
            Color.RGBToHSV(colour, out float h, out float s, out float v);
            float hue = Mathf.Repeat(Mathf.LerpAngle(h * 360f, 210f, 0.35f), 360f) / 360f;
            Color tinted = Color.HSVToRGB(hue, s * 0.45f, Mathf.Min(1f, v + 0.25f));
            tinted.a = colour.a;
            return tinted;
        }
    }
}
