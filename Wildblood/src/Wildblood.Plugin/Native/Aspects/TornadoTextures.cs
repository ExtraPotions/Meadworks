// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using System;
using UnityEngine;

namespace ExtraPotions.Wildblood.Adopted.Aspects
{
    /// <summary>
    /// The small textures a Nightfall tornado makes for itself, once, white or grey so the colour they are drawn with
    /// tints them: a seamless churning cloud for its funnel (<see cref="TornadoCone"/>), banded round it so its turning
    /// shows; a sheet of torn smoke wisps for its dust, four by four, each tile its own shape so no two look alike; and a
    /// hard-edged speck for the grit it flings about. Made here rather
    /// than borrowed: the game's own wispy-smoke sheet packs light into its colour for a lit shader, and drawn unlit it
    /// comes out green.
    /// </summary>
    internal static class TornadoTextures
    {
        /// <summary>The wisp sheet's tiles each way.</summary>
        public const int WispTiles = 4;

        private const int WispTexels = 64;
        private const int SpeckTexels = 16;

        private static Texture2D? _wisps;
        private static Texture2D? _cloud;
        private static Texture2D? _speck;

        public static Texture2D Wisps() => _wisps != null ? _wisps : _wisps = Make(WispTiles * WispTexels, WispAt);

        public static Texture2D Cloud() => _cloud != null ? _cloud : _cloud = TornadoCloud.Make();

        public static Texture2D Speck() => _speck != null ? _speck : _speck = Make(SpeckTexels, (x, y) =>
            SpeckAlpha(Across(x, SpeckTexels), Across(y, SpeckTexels)));

        // White everywhere, with the coverage the shape gives each texel.
        private static Texture2D Make(int texels, Func<int, int, float> alpha)
        {
            Texture2D texture = new Texture2D(texels, texels, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };
            Color32[] pixels = new Color32[texels * texels];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = new Color32(255, 255, 255, (byte)(255f * Mathf.Clamp01(alpha(i % texels, i / texels))));
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }

        // -1 to 1 across a square of the given texels.
        private static float Across(int texel, int texels) => (texel % texels + 0.5f) / texels * 2f - 1f;

        private static float WispAt(int x, int y) =>
            WispAlpha(Across(x, WispTexels), Across(y, WispTexels), x / WispTexels + y / WispTexels * WispTiles);

        // Thick in the middle, its edge torn by two octaves of noise, each tile seeded on its own.
        private static float WispAlpha(float u, float v, int tile)
        {
            float seed = 7f + tile * 13.7f;
            float noise = 0.65f * Mathf.PerlinNoise(u * 2.5f + seed, v * 2.5f + seed * 0.5f)
                + 0.35f * Mathf.PerlinNoise(u * 6f + seed * 1.7f, v * 6f + seed);
            float edge = Mathf.Clamp01(1f - Mathf.Sqrt(u * u + v * v) * (0.75f + 0.5f * noise));
            return Mathf.Clamp01(edge * 2.2f) * (0.6f + 0.45f * noise);
        }

        // Solid, with only a thin soft rim.
        private static float SpeckAlpha(float u, float v) => Mathf.Clamp01((1f - Mathf.Sqrt(u * u + v * v)) * 3f);
    }
}
