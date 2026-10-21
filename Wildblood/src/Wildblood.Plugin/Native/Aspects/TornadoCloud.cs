// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using UnityEngine;

namespace ExtraPotions.Wildblood.Adopted.Aspects
{
    /// <summary>
    /// The seamless cloud a Nightfall tornado's funnel wears (<see cref="TornadoCone"/>): three octaves of value noise on
    /// a lattice that wraps both ways, so it tiles with no seam round the cone and up it. Its features are drawn out round
    /// the funnel more than up it, so as the cone turns and twists them they read as the spiralling bands of a funnel
    /// cloud. Brighter where it is thicker; never fully clear, so the column stays whole.
    /// </summary>
    internal static class TornadoCloud
    {
        private const int Texels = 128;

        /// <summary>The coarsest octave's lattice cells round and up; each finer octave doubles both.</summary>
        private const int Round = 3;
        private const int Up = 8;

        public static Texture2D Make()
        {
            Texture2D texture = new Texture2D(Texels, Texels, TextureFormat.RGBA32, true)
            {
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear,
            };
            Color32[] pixels = new Color32[Texels * Texels];
            for (int i = 0; i < pixels.Length; i++)
            {
                float density = Density((i % Texels + 0.5f) / Texels, (i / Texels + 0.5f) / Texels);
                byte shade = (byte)(255f * (0.55f + 0.45f * density));
                pixels[i] = new Color32(shade, shade, shade, (byte)(255f * (0.3f + 0.7f * density)));
            }
            texture.SetPixels32(pixels);
            texture.Apply(true, true);
            return texture;
        }

        private static float Density(float u, float v)
        {
            float sum = 0.55f * Noise(u, v, Round, Up, 1) + 0.3f * Noise(u, v, Round * 2, Up * 2, 2)
                + 0.15f * Noise(u, v, Round * 4, Up * 4, 3);
            return Mathf.Clamp01((sum - 0.2f) / 0.6f);
        }

        // Value noise on a lattice of `round` by `up` cells that wraps both ways; u and v run 0 to 1.
        private static float Noise(float u, float v, int round, int up, int seed)
        {
            float x = u * round;
            float y = v * up;
            int x0 = Mathf.FloorToInt(x);
            int y0 = Mathf.FloorToInt(y);
            float sx = Smooth(x - x0);
            float sy = Smooth(y - y0);
            float low = Mathf.Lerp(Lattice(x0, y0, round, up, seed), Lattice(x0 + 1, y0, round, up, seed), sx);
            float high = Mathf.Lerp(Lattice(x0, y0 + 1, round, up, seed), Lattice(x0 + 1, y0 + 1, round, up, seed), sx);
            return Mathf.Lerp(low, high, sy);
        }

        private static float Smooth(float t) => t * t * (3f - 2f * t);

        // A steady 0..1 for one lattice point, the lattice wrapped so the texture tiles.
        private static float Lattice(int x, int y, int round, int up, int seed)
        {
            uint wx = (uint)(((x % round) + round) % round);
            uint wy = (uint)(((y % up) + up) % up);
            uint hash = unchecked(wx * 73856093u ^ wy * 19349663u ^ (uint)seed * 83492791u);
            hash = unchecked((hash ^ (hash >> 13)) * 0x5BD1E995u);
            hash ^= hash >> 15;
            return (hash & 0xFFFFu) / 65535f;
        }
    }
}
