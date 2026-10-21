// Adapted from assets/references/source/ValheimMods-openkeep-3.0.0/GrindstoneSkills/GrindstoneSkills/Pickaxes/Seams/SeamLook.cs at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Copyright (C) 2026 Gerald Glasgow. SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Saga namespace, configuration, CS1 and ownership boundaries.
using UnityEngine;
using UnityEngine.Rendering;

namespace ExtraPotions.Saga.Runtime.Adopted.Grindstone
{
    /// <summary>
    /// The pieces a seam's glow (<see cref="SeamGlow"/>) is drawn with, made once and shared by every glow on this client:
    /// <list type="bullet">
    /// <item><see cref="Star"/>: a copy of the game's own item glint material (item_particle, the star-shaped spark that
    /// makes dropped food twinkle), taken from the fx_ItemSparkles child of a vanilla food item. Unlit and
    /// alpha-blended, tinted per glow through the material's _Color.</item>
    /// <item><see cref="Halo"/>: the same material with a soft round texture made here. Without the game's material
    /// (a game update renamed it) the halo falls back to the built-in Sprites/Default shader and the star is left out.</item>
    /// <item>A camera-facing quad drawn from both sides, so the shader's culling never hides it.</item>
    /// </list>
    /// </summary>
    internal static class SeamLook
    {
        private const string SparkleChild = "fx_ItemSparkles";
        private const int HaloPixels = 64;

        /// <summary>Vanilla items that carry the glint (every dropped food does); the first one found is used.</summary>
        private static readonly string[] SparkleItems = { "Honey", "Blueberries", "Raspberry", "Mushroom", "Bread" };

        /// <summary>The tint both materials multiply by; each glow sets it per sprite through a property block.</summary>
        public static readonly int ColorId = Shader.PropertyToID("_Color");

        private static Material star;
        private static Material halo;
        private static Mesh quad;

        public static Material Star
        {
            get
            {
                Prepare();
                return star;
            }
        }

        public static Material Halo
        {
            get
            {
                Prepare();
                return halo;
            }
        }

        /// <summary>A camera-facing sprite under <paramref name="parent"/>; null without a material.</summary>
        public static Renderer Sprite(Transform parent, Material material, string name)
        {
            if (material == null)
                return null;
            GameObject holder = new GameObject(name);
            holder.transform.SetParent(parent, false);
            holder.AddComponent<MeshFilter>().sharedMesh = Quad();
            MeshRenderer renderer = holder.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            return renderer;
        }

        private static void Prepare()
        {
            if (halo != null)
                return;
            Material template = SparkleMaterial();
            star = template != null ? new Material(template) { name = "saga.seam_star" } : null;
            halo = template != null ? new Material(template) : Fallback();
            if (halo == null)
                return;
            halo.name = "saga.seam_halo";
            halo.mainTexture = HaloTexture();
        }

        private static Material SparkleMaterial()
        {
            if (ObjectDB.instance == null)
                return null;
            foreach (string name in SparkleItems)
            {
                GameObject item = ObjectDB.instance.GetItemPrefab(name);
                Transform sparkles = item != null ? item.transform.Find(SparkleChild) : null;
                Renderer renderer = sparkles != null ? sparkles.GetComponent<Renderer>() : null;
                if (renderer != null && renderer.sharedMaterial != null)
                    return renderer.sharedMaterial;
            }
            return null;
        }

        private static Material Fallback()
        {
            Shader shader = Shader.Find("Sprites/Default");
            return shader != null ? new Material(shader) : null;
        }

        /// <summary>White, with alpha falling off from the middle to nothing at the edge.</summary>
        private static Texture2D HaloTexture()
        {
            Texture2D texture = new Texture2D(HaloPixels, HaloPixels, TextureFormat.RGBA32, false);
            texture.name = "saga.seam_halo";
            texture.wrapMode = TextureWrapMode.Clamp;
            Color32[] pixels = new Color32[HaloPixels * HaloPixels];
            float half = (HaloPixels - 1) / 2f;
            for (int y = 0; y < HaloPixels; y++)
            {
                for (int x = 0; x < HaloPixels; x++)
                {
                    float edge = 1f - Mathf.Clamp01(new Vector2(x - half, y - half).magnitude / half);
                    pixels[y * HaloPixels + x] = new Color32(255, 255, 255, (byte)(edge * edge * 255f));
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }

        /// <summary>A unit quad in the XY plane, both faces drawn, white vertex colours (particle shaders multiply them).</summary>
        private static Mesh Quad()
        {
            if (quad != null)
                return quad;
            quad = new Mesh { name = "saga.seam_quad" };
            quad.vertices = new[]
            {
                new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f), new Vector3(-0.5f, 0.5f, 0f), new Vector3(0.5f, 0.5f, 0f),
            };
            quad.uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f) };
            Color32 white = new Color32(255, 255, 255, 255);
            quad.colors32 = new[] { white, white, white, white };
            quad.triangles = new[] { 0, 2, 1, 2, 3, 1, 0, 1, 2, 2, 1, 3 };
            quad.RecalculateBounds();
            return quad;
        }
    }
}
