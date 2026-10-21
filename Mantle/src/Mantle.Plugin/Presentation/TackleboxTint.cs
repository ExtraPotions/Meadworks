// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Mantle.Tackle
{
    /// <summary>
    /// The frost tint of a tacklebox that wears another box's art (<see cref="TackleboxCatalog.Tinted"/>). The model gets
    /// copied materials and the icon a copied texture, so the box the art belongs to keeps its own look.
    /// </summary>
    public static class TackleboxTint
    {
        /// <summary>How far a surface color moves toward the frost tint; glow moves all the way.</summary>
        public const float Strength = 0.65f;

        /// <summary>A color pulled toward the frost tint at its own brightness; alpha is kept.</summary>
        public static Color Wash(Color color, Color frost, float strength = Strength)
        {
            float luma = color.r * 0.299f + color.g * 0.587f + color.b * 0.114f;
            var cold = new Color(luma * frost.r, luma * frost.g, luma * frost.b, color.a);
            return Color.Lerp(color, cold, strength);
        }

        /// <summary>Gives every renderer of <paramref name="model"/> (a copy) tinted copies of its materials.</summary>
        public static void Model(GameObject model, Color frost)
        {
            var copies = new Dictionary<Material, Material>();
            foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>(true))
            {
                Material[] materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++)
                {
                    Material source = materials[i];
                    if (source == null)
                        continue;
                    if (!copies.TryGetValue(source, out Material copy))
                    {
                        copy = new Material(source) { name = source.name + "_frost" };
                        if (copy.HasProperty("_Color"))
                            copy.SetColor("_Color", Wash(copy.GetColor("_Color"), frost));
                        if (copy.HasProperty("_EmissionColor") && copy.IsKeywordEnabled("_EMISSION"))
                            copy.SetColor("_EmissionColor", Wash(copy.GetColor("_EmissionColor"), frost, 1f));
                        copies.Add(source, copy);
                    }
                    materials[i] = copy;
                }
                renderer.sharedMaterials = materials;
            }
        }

        /// <summary>
        /// A tinted copy of <paramref name="source"/>, read back through a render texture (the art bundle's textures are not
        /// readable). Without a graphics device (a dedicated server) or on any failure the source icon is returned.
        /// </summary>
        public static Sprite Icon(Sprite source, Color frost)
        {
            if (source == null || SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                return source;
            RenderTexture previous = RenderTexture.active;
            RenderTexture target = null;
            try
            {
                Rect rect = source.textureRect;
                int width = Mathf.RoundToInt(rect.width);
                int height = Mathf.RoundToInt(rect.height);
                Texture2D texture = source.texture;
                target = RenderTexture.GetTemporary(texture.width, texture.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                Graphics.Blit(texture, target);
                RenderTexture.active = target;
                var copy = new Texture2D(width, height, TextureFormat.RGBA32, false) { name = source.name + "_frost" };
                copy.ReadPixels(new Rect(rect.x, rect.y, width, height), 0, 0);
                Color[] pixels = copy.GetPixels();
                for (int i = 0; i < pixels.Length; i++)
                    pixels[i] = Wash(pixels[i], frost);
                copy.SetPixels(pixels);
                copy.Apply(false, true);
                var pivot = new Vector2(source.pivot.x / rect.width, source.pivot.y / rect.height);
                Sprite sprite = Sprite.Create(copy, new Rect(0f, 0f, width, height), pivot, source.pixelsPerUnit);
                sprite.name = source.name + "_frost";
                return sprite;
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"tacklebox icon tint failed, the untinted icon is used: {e.Message}");
                return source;
            }
            finally
            {
                RenderTexture.active = previous;
                if (target != null)
                    RenderTexture.ReleaseTemporary(target);
            }
        }
    }
}
