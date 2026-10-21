// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
using System;
using System.Collections.Generic;
using ExtraPotions.Inscribe.Adopted.Core;
using UnityEngine;

namespace ExtraPotions.Inscribe.Adopted.Items
{
    /// <summary>
    /// Runtime-tinted copies of the base items' icons (prefabs.md section 6). Game textures are usually not
    /// CPU-readable, so the sprite's region is copied through the GPU (a blit into a render texture, read back into a
    /// new texture) once per base sprite; each stone's icon is then colorized from those pixels. Any failure falls
    /// back to the untinted base sprite and never throws. Clients only: a dedicated server has no graphics device.
    /// </summary>
    internal static class StoneIcons
    {
        // How much of the tint replaces the original color (the rest keeps the base's texture detail). Judgement call.
        private const float Strength = 0.8f;

        private static readonly Dictionary<Sprite, IconPixels?> PixelCache = new Dictionary<Sprite, IconPixels?>();

        /// <summary>A new sprite in the tint, or null when the source cannot be read (use the untinted sprite then).</summary>
        public static Sprite? Tinted(Sprite? source, Color tint, string name)
        {
            if (source == null)
            {
                return null;
            }
            IconPixels? pixels = PixelsOf(source);
            if (pixels == null)
            {
                return null;
            }
            try
            {
                return Build(source, pixels, tint, name);
            }
            catch (Exception e)
            {
                Log.Warn($"could not tint the icon of {name}: {e.Message}");
                return null;
            }
        }

        /// <summary>Destroys a generated sprite and its texture (the base sprites are never passed here).</summary>
        public static void Release(Sprite? generated)
        {
            if (generated == null)
            {
                return;
            }
            UnityEngine.Object.Destroy(generated.texture);
            UnityEngine.Object.Destroy(generated);
        }

        private static IconPixels? PixelsOf(Sprite source)
        {
            if (!PixelCache.TryGetValue(source, out IconPixels? pixels))
            {
                pixels = ReadBack(source);
                PixelCache[source] = pixels;
            }
            return pixels;
        }

        private static IconPixels? ReadBack(Sprite source)
        {
            RenderTexture? previous = RenderTexture.active;
            RenderTexture? target = null;
            try
            {
                target = RenderTexture.GetTemporary((int)source.textureRect.width, (int)source.textureRect.height, 0);
                BlitRegion(source, target);
                return Read(target.width, target.height);
            }
            catch (Exception e)
            {
                Log.Warn($"icon {source.name} is not readable, runes on it keep the untinted icon: {e.Message}");
                return null;
            }
            finally
            {
                RenderTexture.active = previous;
                if (target != null)
                {
                    RenderTexture.ReleaseTemporary(target);
                }
            }
        }

        // Copies only the sprite's region of its (possibly atlas) texture, and makes the target the one ReadPixels reads.
        private static void BlitRegion(Sprite source, RenderTexture target)
        {
            Rect rect = source.textureRect;
            Texture texture = source.texture;
            Vector2 scale = new Vector2(rect.width / texture.width, rect.height / texture.height);
            Vector2 offset = new Vector2(rect.x / texture.width, rect.y / texture.height);
            Graphics.Blit(texture, target, scale, offset);
            RenderTexture.active = target;
        }

        private static IconPixels Read(int width, int height)
        {
            Texture2D readable = new Texture2D(width, height, TextureFormat.RGBA32, false);
            try
            {
                readable.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                readable.Apply(false);
                return new IconPixels(readable.GetPixels32(), width, height);
            }
            finally
            {
                UnityEngine.Object.Destroy(readable);
            }
        }

        private static Sprite Build(Sprite source, IconPixels pixels, Color tint, string name)
        {
            Color32[] tinted = new Color32[pixels.Data.Length];
            for (int i = 0; i < tinted.Length; i++)
            {
                tinted[i] = Colorize(pixels.Data[i], tint);
            }
            Texture2D texture = new Texture2D(pixels.Width, pixels.Height, TextureFormat.RGBA32, false)
            {
                name = name + "_icon",
                wrapMode = TextureWrapMode.Clamp,
            };
            texture.SetPixels32(tinted);
            texture.Apply(false, true);
            Rect full = new Rect(0, 0, pixels.Width, pixels.Height);
            Vector2 pivot = new Vector2(source.pivot.x / source.rect.width, source.pivot.y / source.rect.height);
            Sprite sprite = Sprite.Create(texture, full, pivot, source.pixelsPerUnit);
            sprite.name = name + "_icon";
            return sprite;
        }

        // Keeps each pixel's brightness (its highest channel) and alpha, and moves its hue toward the tint.
        private static Color32 Colorize(Color32 pixel, Color tint)
        {
            float value = Math.Max(pixel.r, Math.Max(pixel.g, pixel.b));
            return new Color32(
                Mix(pixel.r, value * tint.r),
                Mix(pixel.g, value * tint.g),
                Mix(pixel.b, value * tint.b),
                pixel.a);
        }

        private static byte Mix(byte original, float target)
        {
            float mixed = original + (target - original) * Strength;
            return (byte)Mathf.Clamp(Mathf.RoundToInt(mixed), 0, 255);
        }

        private sealed class IconPixels
        {
            public IconPixels(Color32[] data, int width, int height)
            {
                Data = data;
                Width = width;
                Height = height;
            }

            public Color32[] Data { get; }
            public int Width { get; }
            public int Height { get; }
        }
    }
}
