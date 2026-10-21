using System.Globalization;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Workshop.Vfx
{
    /// <summary>
    /// The effect's textures (imported the way the game imports its particle textures) and its placeholder materials.
    /// A placeholder is Unity's own standard particle shader wearing our texture, so an undressed effect still draws;
    /// its override tags say what the mod dresses it into at runtime (BundlePrefabs' BundleEffects): VfxShader (the
    /// game shader's name), VfxBorrow (a game prefab whose material to copy), VfxFloats, VfxColours, VfxKeywords.
    /// </summary>
    public static class VfxMaterials
    {
        public const string ShaderTag = "VfxShader", BorrowTag = "VfxBorrow", FloatsTag = "VfxFloats";
        public const string ColoursTag = "VfxColours", KeywordsTag = "VfxKeywords", BlendTag = "VfxBlend";
        private static readonly string[] LitShaders =
            { "Lux Lit Particles/ Bumped", "Custom/LitParticles", "Particles/Standard Surface2", "Custom/ParticleDecal", "Standard" };

        public static Texture2D Texture(string folder, TextureSpec t)
        {
            string path = folder + "/" + t.file;
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = t.srgb;
            importer.alphaIsTransparency = true;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.mipmapEnabled = t.mips;
            importer.filterMode = t.point ? FilterMode.Point : FilterMode.Bilinear;
            importer.wrapMode = t.wrap == "repeat" ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
            importer.maxTextureSize = t.max_size;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        public static Material Placeholder(string folder, MaterialSpec m, Texture2D texture)
        {
            bool lit = LitShaders.Contains(m.game_shader);
            var material = new Material(Shader.Find(lit ? "Particles/Standard Surface" : "Particles/Standard Unlit")) { name = m.name };
            material.mainTexture = texture;
            StandardMode(material, m);
            material.SetOverrideTag(ShaderTag, m.game_shader);
            material.SetOverrideTag(BlendTag, m.blend);
            if (!string.IsNullOrEmpty(m.borrow))
                material.SetOverrideTag(BorrowTag, m.borrow);
            material.SetOverrideTag(FloatsTag, string.Join(";", m.floats.Select(f => f.k + "=" + Number(f.v))));
            material.SetOverrideTag(ColoursTag, string.Join(";", m.colours.Select(c => c.k + "=" + string.Join(",", c.v.Select(Number)))));
            material.SetOverrideTag(KeywordsTag, string.Join(" ", m.keywords));
            AssetDatabase.CreateAsset(material, folder + "/" + m.name + ".mat");
            return material;
        }

        /// <summary>The standard particle shader's blend for the placeholder: the nearest of its modes to the game blend.</summary>
        private static void StandardMode(Material material, MaterialSpec m)
        {
            float src = Float(m, "_SrcBlend", 5), dst = Float(m, "_DstBlend", 10);
            material.SetFloat("_Mode", m.placeholder_mode);
            material.SetFloat("_SrcBlend", src == 3 ? 5 : src);   // the standard shader has no SrcColor mode: SrcAlpha
            material.SetFloat("_DstBlend", dst);
            material.SetFloat("_ZWrite", m.blend == "opaque" ? 1 : 0);
            material.SetFloat("_Cull", 0);
            foreach (string keyword in m.placeholder_keywords)
                material.EnableKeyword(keyword);
            material.renderQueue = m.queue;
        }

        public static float Float(MaterialSpec m, string key, float fallback)
        {
            FloatProp found = m.floats.FirstOrDefault(f => f.k == key);
            return found == null ? fallback : found.v;
        }

        private static string Number(float v) => v.ToString("0.#####", CultureInfo.InvariantCulture);
    }
}
