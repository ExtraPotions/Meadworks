// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using UnityEngine;

namespace ExtraPotions.Wildblood.Adopted.Mutations
{
    /// <summary>
    /// The shape a patch is drawn in, made here once per kind rather than shipped: a solid pool with a ragged, soft
    /// edge, white so the kind's colour tints it. Mud is lumpy - darker clots and thinner film across it, so it reads
    /// as thick, uneven ground; ice is smooth with a pale sheen across it, so it reads as a glossy sheet. The game's
    /// own decal textures are scatters of droplets, which would make a trail of splashes rather than of ground.
    /// </summary>
    internal static class PatchTexture
    {
        private const int Size = 128;

        /// <summary>Where the pool's edge starts to thin and where it is gone, as a share of the half-width.</summary>
        private const float EdgeIn = 0.7f;
        private const float EdgeOut = 0.95f;

        private static readonly Texture2D?[] Made = new Texture2D?[TrailKind.All.Length];

        /// <summary>The kind's patch texture, made on first use.</summary>
        public static Texture2D For(TrailKind kind)
        {
            Texture2D? texture = Made[kind.Index];
            if (texture == null)
            {
                texture = Make(kind);
                Made[kind.Index] = texture;
            }
            return texture;
        }

        private static Texture2D Make(TrailKind kind)
        {
            Texture2D texture = new Texture2D(Size, Size, TextureFormat.RGBA32, true)
            {
                name = $"ecr_{kind.Name}_patch",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.DontUnloadUnusedAsset,
            };
            Color32[] texels = new Color32[Size * Size];
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    texels[y * Size + x] = Texel(kind, x / (Size - 1f), y / (Size - 1f));
                }
            }
            texture.SetPixels32(texels);
            texture.Apply(true, true);
            return texture;
        }

        // One texel at (u, v), each 0 to 1: how much pool is there (alpha) and how bright it is (grey).
        private static Color32 Texel(TrailKind kind, float u, float v)
        {
            float seed = kind.Index * 17.3f;
            float dx = u * 2f - 1f;
            float dy = v * 2f - 1f;
            float ragged = (Mathf.PerlinNoise(u * 4f + seed, v * 4f + seed) - 0.5f) * 0.35f;
            float reach = Mathf.Sqrt(dx * dx + dy * dy) + ragged;
            float alpha = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(EdgeIn, EdgeOut, reach));
            float grain = Mathf.PerlinNoise(u * 11f + seed, v * 11f - seed);
            float grey = kind.Slips ? Sheen(dx, dy, grain) : 0.7f + 0.3f * grain;
            alpha *= kind.Slips ? 0.9f + 0.1f * grain : 0.8f + 0.2f * grain;
            byte g = (byte)(255f * Mathf.Clamp01(grey));
            return new Color32(g, g, g, (byte)(255f * Mathf.Clamp01(alpha)));
        }

        // Ice: nearly even, with a soft bright band across it where it catches the light.
        private static float Sheen(float dx, float dy, float grain)
        {
            float band = (dx + dy) * 0.7f - 0.15f;
            return 0.78f + 0.07f * grain + 0.2f * Mathf.Exp(-band * band * 18f);
        }
    }
}
