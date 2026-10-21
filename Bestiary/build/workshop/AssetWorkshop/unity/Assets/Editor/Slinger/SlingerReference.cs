using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Workshop.Slinger
{
    /// <summary>
    /// The game's Greydwarf from the reference export, for authoring the slinger's clip on its own skeleton and for
    /// previews: its model prefab (the same meshes and avatar as the game's Greydwarf), its textures on a Standard
    /// material, and its clips. Everything lands in Assets/Reference, which never goes into a bundle; the mod builds the
    /// slinger from the running game's Greydwarf instead.
    /// </summary>
    public static class SlingerReference
    {
        private const string Model = "Characters/GreyDwarf/newmodel/";
        private const string Textures = "Characters/GreyDwarf/Materials/";
        private static readonly string[] ModelParts =
            { "Cube.001.asset", "Cube_6.asset", "Plane.001_8.asset", "Plane_4.asset", "greydwarfAvatar.asset", "Material.mat", "No Name_1.mat" };

        /// <summary>A fresh Greydwarf at the origin facing +Z, textured, with its humanoid Animator.</summary>
        public static GameObject Greydwarf()
        {
            foreach (string part in ModelParts)
                ReferenceAssets.Import(Model + part);
            string prefab = ReferenceAssets.Import(Model + "greydwarf.prefab");
            var greydwarf = (GameObject)Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(prefab));
            greydwarf.name = "Greydwarf";
            Dress(greydwarf);
            return greydwarf;
        }

        /// <summary>One of the Greydwarf's clips (Idle, Dwarf Walk, Throw ...), for the base pose and the preview.</summary>
        public static AnimationClip Clip(string name) =>
            AssetDatabase.LoadAssetAtPath<AnimationClip>(ReferenceAssets.Import(Model + name + ".anim"));

        public static Transform Bone(GameObject greydwarf, string name) =>
            greydwarf.GetComponentsInChildren<Transform>(true).First(t => t.name == name);

        private static void Dress(GameObject greydwarf)
        {
            Material skin = SkinMaterial();
            foreach (var renderer in greydwarf.GetComponentsInChildren<Renderer>(true))
                renderer.sharedMaterials = renderer.sharedMaterials.Select(m => IsEye(m) ? EyeMaterial() : skin).ToArray();
        }

        private static bool IsEye(Material material) => material != null && material.name.StartsWith("No Name");

        private static Material SkinMaterial()
        {
            string path = ReferenceAssets.Folder + "/greydwarf_preview.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
                return existing;
            var material = new Material(Shader.Find("Standard")) { name = "greydwarf_preview" };
            material.SetTexture("_MainTex", ReferenceAssets.Texture(Textures + "greydrawrf_diffuse.png", false));
            material.SetTexture("_BumpMap", ReferenceAssets.Texture(Textures + "greydrawrf_diffuse_nrm.png", true));
            material.EnableKeyword("_NORMALMAP");
            material.SetFloat("_Glossiness", 0.1f);
            material.SetFloat("_Mode", 1f);   // cutout: the moss fringes are alpha tested in the game
            material.SetFloat("_Cutoff", 0.5f);
            material.EnableKeyword("_ALPHATEST_ON");
            material.renderQueue = 2450;
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static Material EyeMaterial()
        {
            string path = ReferenceAssets.Folder + "/greydwarf_eye_preview.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
                return existing;
            var glow = new Color(0.35f, 0.75f, 1f);
            var material = new Material(Shader.Find("Standard")) { name = "greydwarf_eye_preview", color = glow };
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", glow * 2f);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }
    }
}
