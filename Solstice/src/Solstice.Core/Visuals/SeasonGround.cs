// Solstice - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original. Terrain snow is the game's mountain vertex mask. Grass frost is a copy of the texture, not a white _Color.
using System;

namespace ExtraPotions.Solstice.Visuals
{
    /// <summary>
    /// How much winter snow to put on the ground and the grass. The biome numbers are the game's
    /// <c>Heightmap.Biome</c> values. Mountain snow is the green channel at 255.
    /// </summary>
    public static class SeasonGround
    {
        public const int Meadows = 1;
        public const int Swamp = 2;
        public const int BlackForest = 8;
        public const int Plains = 16;
        public const int SnowBiomes = Meadows | Swamp | BlackForest | Plains;

        public static bool Paints(int biome) =>
            biome == Meadows || biome == Swamp || biome == BlackForest || biome == Plains;

        /// <summary>Full cover stops here, under the mountain mask, so open snow is not pure white.</summary>
        public const float MaskCap = 0.8f;

        /// <summary>Cached frost strengths. The source texture is read once; a day only changes which step is shown.</summary>
        public const int FrostSteps = 4;

        /// <summary>Main-thread budget for one texture read or upload. Pixel blending itself runs off that thread.</summary>
        public const double PixelBudgetMs = 2;

        /// <summary>Loaded chunks rebuilt on one frame, nearest first, and not after this many milliseconds.</summary>
        public const int ChunksPerFrame = 3;

        public const double ChunkSliceMs = 12;

        /// <summary>Main-thread milliseconds for the material scan on one frame; the rest continues on the next.</summary>
        public const double ScanSliceMs = 4;

        /// <summary>0 leaves the mask alone. The cap is the mountain mask at 0.8. Other biomes are not changed.</summary>
        public static void Paint(int biome, ref byte r, ref byte g, ref byte b, ref byte a, float amount)
        {
            if (!Paints(biome) || float.IsNaN(amount) || amount <= 0f) return;
            if (amount > MaskCap) amount = MaskCap;
            float keep = 1f - amount;
            r = Unit(r * keep);
            g = Unit(g * keep + 255f * amount);
            b = Unit(b * keep);
            a = Unit(a * keep);
        }

        public static bool IsGrass(string name) =>
            !string.IsNullOrEmpty(name) && name.IndexOf("grass", StringComparison.OrdinalIgnoreCase) >= 0;

        /// <summary>Flower and bloom clutter drops out once cover reaches 0.2.</summary>
        public static bool HideBloom(string name, float amount)
        {
            if (string.IsNullOrEmpty(name) || float.IsNaN(amount) || amount < 0.2f) return false;
            return name.IndexOf("flower", StringComparison.OrdinalIgnoreCase) >= 0
                || name.IndexOf("bloom", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        /// <summary>Full cover keeps about half the blades. A count of 0 stays 0.</summary>
        public static int GrassCount(int original, float amount)
        {
            if (original <= 0 || float.IsNaN(amount) || amount <= 0f) return original;
            if (amount > 1f) amount = 1f;
            int next = (int)Math.Round(original * (1.0 - 0.55 * amount));
            if (next < 1) next = 1;
            return next > original ? original : next;
        }

        /// <summary>Full cover shortens grass to a bit over half.</summary>
        public static float GrassScale(float original, float amount)
        {
            if (float.IsNaN(original) || float.IsNaN(amount) || amount <= 0f) return original;
            if (amount > 1f) amount = 1f;
            return original * (1f - 0.45f * amount);
        }

        /// <summary>0 is no frost. 1 through <see cref="FrostSteps"/> are the cached strengths, and 1 is the lightest.</summary>
        public static int FrostStep(float amount)
        {
            if (float.IsNaN(amount) || amount <= 0f) return 0;
            if (amount > 1f) amount = 1f;
            int step = (int)Math.Ceiling(amount * FrostSteps - 0.0001);
            if (step < 1) step = 1;
            if (step > FrostSteps) step = FrostSteps;
            return step;
        }

        public static float FrostAmount(int step)
        {
            if (step <= 0) return 0f;
            if (step > FrostSteps) step = FrostSteps;
            return step / (float)FrostSteps;
        }

        /// <summary>1 when another chunk fits this frame. The first chunk is allowed; later ones stop at the slice.</summary>
        public static int NextChunk(int doneThisFrame, double elapsedMs, int remaining)
        {
            if (remaining <= 0 || doneThisFrame >= ChunksPerFrame) return 0;
            if (doneThisFrame > 0 && elapsedMs >= ChunkSliceMs) return 0;
            return 1;
        }

        /// <summary>True when another material fits this frame. The first one is allowed so a slow frame still advances.</summary>
        public static bool NextScan(int doneThisFrame, double elapsedMs, int remaining) =>
            remaining > 0 && (doneThisFrame == 0 || elapsedMs < ScanSliceMs);

        /// <summary>
        /// RGBA8 bytes. Every source alpha byte is copied through, including partial cutout values.
        /// Grass moves toward white. Leaves, and ground clutter that uses this path, lift bright green and leave brown pixels near the original.
        /// </summary>
        public static void Frost(byte[] rgba, float amount, bool leaves)
        {
            if (rgba == null || rgba.Length < 4 || float.IsNaN(amount) || amount <= 0f) return;
            if (amount > 1f) amount = 1f;
            int last = rgba.Length - (rgba.Length % 4);
            for (int i = 0; i < last; i += 4)
            {
                byte alpha = rgba[i + 3];
                if (alpha == 0) continue;
                float r = rgba[i] / 255f;
                float g = rgba[i + 1] / 255f;
                float b = rgba[i + 2] / 255f;
                if (leaves) DustLeaf(ref r, ref g, ref b, amount);
                else Whiten(ref r, ref g, ref b, amount * 0.9f, 0.91f, 0.95f, 0.98f);
                rgba[i] = Unit(r * 255f);
                rgba[i + 1] = Unit(g * 255f);
                rgba[i + 2] = Unit(b * 255f);
                rgba[i + 3] = alpha;
            }
        }

        static void Whiten(ref float r, ref float g, ref float b, float blend, float targetR, float targetG, float targetB)
        {
            r += (targetR - r) * blend;
            g += (targetG - g) * blend;
            b += (targetB - b) * blend;
        }

        static void DustLeaf(ref float r, ref float g, ref float b, float amount)
        {
            float max = r > g ? r : g;
            if (b > max) max = b;
            float min = r < g ? r : g;
            if (b < min) min = b;
            float sat = max <= 0.001f ? 0f : (max - min) / max;
            float luma = 0.2126f * r + 0.7152f * g + 0.0722f * b;
            bool green = g >= r + 0.05f && g >= b + 0.02f && sat >= 0.28f && luma >= 0.22f && g >= 0.25f;
            if (green)
            {
                float lift = amount * 0.48f;
                r += (0.93f - r) * lift;
                g += (0.96f - g) * lift;
                b += (0.90f - b) * lift;
                return;
            }
            float warm = amount * 0.10f;
            if (luma < 0.18f) warm *= 0.35f;
            r += (0.78f - r) * warm;
            g += (0.74f - g) * warm;
            b += (0.68f - b) * warm;
        }

        static byte Unit(float value)
        {
            if (value <= 0f) return 0;
            if (value >= 255f) return 255;
            return (byte)Math.Round(value);
        }
    }
}
