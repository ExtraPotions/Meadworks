using System.Collections.Generic;
using System.Globalization;
using UnityEditor;
using UnityEngine;

namespace Workshop.Vfx
{
    /// <summary>
    /// For previews only: a material drawn the way the game would draw it, in the workshop's preview shaders (linear
    /// light, the game's blends and fades). The source is either one of our placeholders (its VfxShader tag names the
    /// game shader, VfxFloats and VfxColours hold the settings) or a game material staged from the reference export
    /// (its exported dummy shader keeps the game shader's name and every property).
    /// </summary>
    public static class VfxEmulation
    {
        private static readonly string[] Floats = { "_SrcBlend", "_DstBlend", "_ZWrite", "_Cutoff", "_GradientChannel",
            "_GradientAsAlpha", "_SoftParticles", "_SoftFadeFactor", "_SoftNearFade", "_CameraFadeFactor", "_AlphaChannel",
            "_InvFade", "_ZFadeDistance", "_WrappedDiffuse", "_Translucency", "_SoftParticlesEnabled",
            "_SoftParticlesFarFadeDistance", "_CameraFadingEnabled", "_CameraNearFadeDistance", "_CameraFarFadeDistance",
            "_Mode", "_DistortionEnabled" };
        private static readonly string[] Colours = { "_Color", "_TintColor", "_EmissionColor" };
        private static readonly Dictionary<Material, Material> made = new Dictionary<Material, Material>();

        /// <summary>Drops the preview copies made for the last scene (a new scene unloads them).</summary>
        public static void Forget() => made.Clear();

        public static Material For(Material source)
        {
            if (source == null)
                return null;
            if (made.TryGetValue(source, out Material done))
                return done;
            string game = source.GetTag(VfxMaterials.ShaderTag, false, "");
            bool ours = game != "";
            var settings = ours ? FromTags(source) : FromMaterial(source);
            Material preview = Make(ours ? game : source.shader.name, settings, source);
            made[source] = preview;
            return preview;
        }

        private static Material Make(string game, Dictionary<string, Vector4> settings, Material source)
        {
            var material = new Material(Shader.Find(Emulator(game))) { name = source.name + " (preview)" };
            material.mainTexture = source.mainTexture;
            foreach (var pair in settings)
            {
                if (pair.Key.StartsWith("_") && Colour(pair.Key))
                    material.SetColor(pair.Key, pair.Value);
                else
                    material.SetFloat(pair.Key, pair.Value.x);
            }
            Specials(material, game, settings);
            material.SetFloat("_VfxSRGB", SRGB(source.mainTexture) ? 1 : 0);
            material.renderQueue = Queue(material, source);
            return material;
        }

        /// <summary>The game's particle shaders draw in the transparent queue; an exported dummy shader has no queue tag,
        /// so a staged game material reports 2000 and would draw before the ground covers it.</summary>
        private static int Queue(Material preview, Material source)
        {
            bool opaque = preview.shader.name == "Workshop/Vfx/Opaque" || preview.GetFloat("_ZWrite") > 0.5f;
            if (opaque)
                return Mathf.Min(source.renderQueue, 2450);
            return source.renderQueue >= 2450 ? source.renderQueue : 3000;
        }

        private static bool Colour(string key) => System.Array.IndexOf(Colours, key) >= 0 || key == "_CamFadeDistance";

        /// <summary>Settings each game shader implies that the material does not carry: blends, legacy tint, lit mode.</summary>
        private static void Specials(Material m, string game, Dictionary<string, Vector4> settings)
        {
            if (game.StartsWith("Legacy Shaders/Particles"))
            {
                m.SetFloat("_VfxLegacy", 1);
                bool additive = game.Contains("Additive");
                m.SetFloat("_SrcBlend", game.Contains("Premultiply") ? 1 : 5);
                m.SetFloat("_DstBlend", additive ? 1 : 10);
            }
            if (game.StartsWith("Lux"))
                Blend(m, 5, 10, 0);
            if (game == "Custom/LitParticles" || game == "Custom/ShadowBlob")
                Blend(m, 5, 10, 0);
            if (game == "Custom/AlphaParticle")
                Blend(m, 1, 0, 1);
            m.SetFloat("_VfxLitMode", game.StartsWith("Lux") ? 0 : 1);
            bool hidden = game.Contains("Decal") || game.Contains("Distortion")
                || (settings.TryGetValue("_DistortionEnabled", out var distortion) && distortion.x > 0.5f);
            m.SetFloat("_VfxHide", hidden ? 1 : 0);
        }

        private static void Blend(Material m, float src, float dst, float zwrite)
        {
            m.SetFloat("_SrcBlend", src);
            m.SetFloat("_DstBlend", dst);
            m.SetFloat("_ZWrite", zwrite);
        }

        public static string Emulator(string game)
        {
            if (game == "Custom/Gradient Mapped Particle (Unlit)")
                return "Workshop/Vfx/Gradient Mapped";
            if (game == "Custom/Particle (Unlit)")
                return "Workshop/Vfx/Particle Unlit";
            if (game.StartsWith("Lux") || game == "Custom/LitParticles" || game.StartsWith("Particles/Standard Surface")
                || game == "Custom/AlphaParticle" || game == "Custom/ShadowBlob")
                return "Workshop/Vfx/Lit";
            if (game.StartsWith("Standard") || game == "Custom/StaticRock" || game == "Custom/Piece" || game == "Custom/Creature"
                || game == "Custom/Blob" || game == "Custom/Vegetation" || game == "Custom/StandardTwosided")
                return "Workshop/Vfx/Opaque";
            return "Workshop/Vfx/Standard Unlit";
        }

        private static Dictionary<string, Vector4> FromTags(Material source)
        {
            var settings = new Dictionary<string, Vector4>();
            foreach (string pair in source.GetTag(VfxMaterials.FloatsTag, false, "").Split(';'))
            {
                string[] kv = pair.Split('=');
                if (kv.Length == 2)
                    settings[kv[0]] = new Vector4(Parse(kv[1]), 0, 0, 0);
            }
            foreach (string pair in source.GetTag(VfxMaterials.ColoursTag, false, "").Split(';'))
            {
                string[] kv = pair.Split('=');
                if (kv.Length == 2)
                    settings[kv[0]] = Vector(kv[1]);
            }
            return settings;
        }

        private static Dictionary<string, Vector4> FromMaterial(Material source)
        {
            var settings = new Dictionary<string, Vector4>();
            foreach (string key in Floats)
                if (source.HasProperty(key))
                    settings[key] = new Vector4(source.GetFloat(key), 0, 0, 0);
            foreach (string key in Colours)
                if (source.HasProperty(key))
                    settings[key] = source.GetColor(key);
            return settings;
        }

        private static Vector4 Vector(string text)
        {
            string[] parts = text.Split(',');
            var v = new Vector4(1, 1, 1, 1);
            for (int i = 0; i < parts.Length && i < 4; i++)
                v[i] = Parse(parts[i]);
            return v;
        }

        private static float Parse(string text) => float.Parse(text, CultureInfo.InvariantCulture);

        private static bool SRGB(Texture texture)
        {
            if (texture == null)
                return true;
            var importer = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(texture)) as TextureImporter;
            return importer == null || importer.sRGBTexture;
        }
    }
}
