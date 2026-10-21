// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
using UnityEngine;

namespace ExtraPotions.Inscribe.Adopted.Display.Backdrops
{
    /// <summary>
    /// The rarity backdrop's art (the user's pick "H2" of the 2026-10-05 mockups, without its corner blooms, nothing
    /// animated; moved here from PackPanel, unchanged): a rounded square with a bright rim, a faint inlaid line
    /// <see cref="InnerInset"/> units inside it and a soft glow from the centre that fades out before the edge, painted
    /// once in code (no image file). Each layer bakes in, as a grey, how much of the Image's tint it shows; they are
    /// stacked in linear colour, as the game blends its UI. Drawn as a Simple sprite stretched over the backdrop's rect,
    /// <see cref="Span"/> units across in the game's 64-unit icon.
    /// </summary>
    internal static class BackdropArt
    {
        public const float Span = 58f;
        private const float Corner = 4f;

        private const float GlowGrey = 0.68f;
        private const float GlowAlpha = 0.9f;
        private const float GlowFull = 0.05f;
        private const float GlowReach = Span / 2f + 9f;

        private const float RimWidth = 1f;
        private const float InnerInset = 3f;
        private const float InnerGrey = 0.85f;
        private const float InnerAlpha = 0.45f;

        private const int Size = 128;
        private const int Samples = 4;
        private const float PixelsPerUnit = Size / Span;
        private static Sprite? _sprite;

        public static Sprite Sprite => _sprite != null ? _sprite : _sprite = Paint();

        private static Sprite Paint()
        {
            Texture2D texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false)
            {
                name = "ecf_backdrop",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontUnloadUnusedAsset,
            };
            Color32[] pixels = new Color32[Size * Size];
            for (int y = 0; y < Size; y++)
                for (int x = 0; x < Size; x++)
                    pixels[y * Size + x] = Pixel(x, y);
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            Sprite made = Sprite.Create(texture, new Rect(0f, 0f, Size, Size), new Vector2(0.5f, 0.5f), PixelsPerUnit);
            made.name = "ecf_backdrop";
            made.hideFlags = HideFlags.DontUnloadUnusedAsset;
            return made;
        }

        /// <summary>One texture pixel, averaged over <see cref="Samples"/> squared points: premultiplied linear grey and alpha.</summary>
        private static Color32 Pixel(int px, int py)
        {
            float grey = 0f, alpha = 0f;
            for (int sy = 0; sy < Samples; sy++)
                for (int sx = 0; sx < Samples; sx++)
                {
                    float x = (px + (sx + 0.5f) / Samples) / PixelsPerUnit - Span / 2f;
                    float y = (py + (sy + 0.5f) / Samples) / PixelsPerUnit - Span / 2f;
                    Vector2 point = Layers(x, y);
                    grey += point.x;
                    alpha += point.y;
                }
            grey /= Samples * Samples;
            alpha /= Samples * Samples;
            byte g = (byte)Mathf.RoundToInt(255f * Mathf.LinearToGammaSpace(alpha > 0f ? grey / alpha : 1f));
            return new Color32(g, g, g, (byte)Mathf.RoundToInt(255f * alpha));
        }

        /// <summary>The glow, then the rim, then the inner line, each laid over the last: (premultiplied linear grey, alpha).</summary>
        private static Vector2 Layers(float x, float y)
        {
            float edge = Distance(x, y);
            float reach = Mathf.Sqrt(x * x + y * y) / GlowReach;
            float glow = edge <= -RimWidth ? (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(GlowFull, 1f, reach))) * GlowAlpha : 0f;
            Vector2 stack = Over(Vector2.zero, GlowGrey, glow);
            stack = Over(stack, 1f, edge <= 0f && edge > -RimWidth ? 1f : 0f);
            return Over(stack, InnerGrey, edge <= -InnerInset && edge > -InnerInset - RimWidth ? InnerAlpha : 0f);
        }

        private static Vector2 Over(Vector2 under, float grey, float alpha) =>
            new Vector2(under.x * (1f - alpha) + Mathf.GammaToLinearSpace(grey) * alpha, under.y * (1f - alpha) + alpha);

        /// <summary>Signed distance from (x, y) to the rounded square's outline in units, negative inside.</summary>
        private static float Distance(float x, float y)
        {
            float qx = Mathf.Abs(x) - (Span / 2f - Corner);
            float qy = Mathf.Abs(y) - (Span / 2f - Corner);
            float outside = new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude;
            return outside + Mathf.Min(Mathf.Max(qx, qy), 0f) - Corner;
        }
    }
}
