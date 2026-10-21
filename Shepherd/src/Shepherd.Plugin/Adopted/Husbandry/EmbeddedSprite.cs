// Copyright (C) 2026 Gerald Glasgow.
// Pin: ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: namespace only; decoded textures owned by Shepherd skill lifecycle.
// Adapted from ValheimModLibs/PlateColumn/EmbeddedSprite.cs
// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace ExtraPotions.Shepherd.Runtime.Adopted.Husbandry
{
    /// <summary>
    /// A sprite from a PNG embedded in a mod's DLL, for a column box's icon or any other UI icon: decoded with mipmaps
    /// so it stays smooth at any interface scale, keeping its own colours. Null when the resource is missing or will not
    /// decode; the mod logs it.
    /// </summary>
    public static class EmbeddedSprite
    {
        public static Sprite? Load(Assembly assembly, string resource, string spriteName)
        {
            byte[]? bytes = Read(assembly, resource);
            Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, true)
            {
                filterMode = FilterMode.Trilinear,
                wrapMode = TextureWrapMode.Clamp,
            };
            if (bytes == null || !Decode(texture, bytes))
            {
                UnityEngine.Object.Destroy(texture);
                return null;
            }
            Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
            sprite.name = spriteName;
            return sprite;
        }

        /// <summary>
        /// The game's PNG decoder, <c>ImageConversion.LoadImage(Texture2D, byte[], bool)</c>, which also builds the
        /// mipmaps. Its module is built against netstandard 2.1 (for a <c>ReadOnlySpan</c> overload), which a net48
        /// project cannot reference (CS1705), so the method is found at runtime; the texture is marked non-readable.
        /// </summary>
        private static bool Decode(Texture2D texture, byte[] png)
        {
            MethodInfo? loadImage = Type.GetType("UnityEngine.ImageConversion, UnityEngine.ImageConversionModule")
                ?.GetMethod("LoadImage", new[] { typeof(Texture2D), typeof(byte[]), typeof(bool) });
            return loadImage != null && (bool)loadImage.Invoke(null, new object[] { texture, png, true });
        }

        private static byte[]? Read(Assembly assembly, string resource)
        {
            using (Stream? stream = assembly.GetManifestResourceStream(resource))
            {
                if (stream == null)
                {
                    return null;
                }
                using (MemoryStream memory = new MemoryStream())
                {
                    stream.CopyTo(memory);
                    return memory.ToArray();
                }
            }
        }
    }
}
