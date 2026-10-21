// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.10.0 at d3b57606cb24775d61b7bda244b22eb89e7e7717.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
using UnityEngine;

namespace ExtraPotions.Inscribe.Adopted.Tables.Window
{
    /// <summary>
    /// The pill's art, painted once in code: a rounded rectangle with a bright rim and a darker, slightly see-through
    /// middle, grey so the Image's colour (the essence's) tints both. Sliced at its rounded corners, so a tag of any width keeps them.
    /// </summary>
    internal static class PillArt
    {
        private const int Width = 48;
        private const int Height = 24;
        private const float Radius = 5f; // Meadworks: rounded rectangle, not a capsule (user rule: no pill shapes)
        private const float Rim = 1.6f;
        private const int Samples = 3;

        /// <summary>The sprite's pixels per unit; with <see cref="Height"/> pixels tall it is a pill of that many pixels.</summary>
        public const float PixelsPerUnit = 100f;

        /// <summary>The texture's height in pixels: its round ends are half of it wide.</summary>
        public const float TextureHeight = Height;

        private static readonly Color Middle = new Color(0.42f, 0.42f, 0.42f, 0.85f);
        private static readonly Color Edge = new Color(1f, 1f, 1f, 1f);

        private static Sprite? _sprite;

        public static Sprite Sprite => _sprite != null ? _sprite : _sprite = Paint();

        private static Sprite Paint()
        {
            var texture = new Texture2D(Width, Height, TextureFormat.RGBA32, false)
            {
                name = "ecf_pill",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontUnloadUnusedAsset,
            };
            var pixels = new Color[Width * Height];
            for (int y = 0; y < Height; y++)
                for (int x = 0; x < Width; x++)
                    pixels[y * Width + x] = Pixel(x, y);
            texture.SetPixels(pixels);
            texture.Apply(false, true);
            // The round ends (Radius pixels) are the slice borders; the Image sizes them (PillView.EndScale), since a sliced
            // border is measured against the canvas's reference pixels per unit, not this sprite's.
            Sprite made = Sprite.Create(texture, new Rect(0f, 0f, Width, Height), new Vector2(0.5f, 0.5f),
                PixelsPerUnit, 0, SpriteMeshType.FullRect, new Vector4(Radius, 0f, Radius, 0f));
            made.name = "ecf_pill";
            made.hideFlags = HideFlags.DontUnloadUnusedAsset;
            return made;
        }

        private static Color Pixel(int px, int py)
        {
            Color sum = Color.clear;
            for (int sy = 0; sy < Samples; sy++)
                for (int sx = 0; sx < Samples; sx++)
                {
                    Color c = Point(px + (sx + 0.5f) / Samples, py + (sy + 0.5f) / Samples);
                    sum += new Color(c.r * c.a, c.g * c.a, c.b * c.a, c.a);
                }
            sum /= Samples * Samples;
            return sum.a > 0f ? new Color(sum.r / sum.a, sum.g / sum.a, sum.b / sum.a, sum.a) : Color.clear;
        }

        // Signed distance to the rounded rectangle's edge: negative inside.
        private static Color Point(float x, float y)
        {
            float qx = Mathf.Abs(x - Width / 2f) - (Width / 2f - Radius);
            float qy = Mathf.Abs(y - Height / 2f) - (Height / 2f - Radius);
            float d = new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude + Mathf.Min(Mathf.Max(qx, qy), 0f) - (Radius - 0.5f);
            return d > 0f ? Color.clear : d > -Rim ? Edge : Middle;
        }
    }
}
