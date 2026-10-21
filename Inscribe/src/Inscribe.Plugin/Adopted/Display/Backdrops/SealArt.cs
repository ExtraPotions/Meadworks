// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.10.0 at d3b57606cb24775d61b7bda244b22eb89e7e7717.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
using UnityEngine;

namespace ExtraPotions.Inscribe.Adopted.Display.Backdrops
{
    /// <summary>
    /// The seal mark's art (user request 2026-10-06: "when an item is sealed we need a like sealed symbol on the icon",
    /// then "can it just be some infinity symbol?"): an infinity sign in red ("it should be red") with a thin dark outline and a soft
    /// shadow, so it reads on any icon. Painted once in code (no image file), colours baked in: the Image showing it stays
    /// white. Twice as wide as tall.
    /// </summary>
    internal static class SealArt
    {
        private const int Width = 96;
        private const int Height = 48;
        private const int Samples = 3;
        private const int CurvePoints = 72;

        // In units where the texture spans x -1..1 and y -0.5..0.5.
        private const float Reach = 0.86f;
        private const float Stroke = 0.085f;
        private const float Outline = 0.05f;
        private static readonly Vector2 ShadowOffset = new Vector2(0.03f, -0.04f);

        private static readonly Color Red = new Color(0.86f, 0.1f, 0.08f);
        private static readonly Color Shine = new Color(1f, 0.45f, 0.38f);
        private static readonly Color Rim = new Color(0.16f, 0.02f, 0.02f);

        private static Vector2[]? _curve;
        private static Sprite? _sprite;

        public static Sprite Sprite => _sprite != null ? _sprite : _sprite = Paint();

        private static Sprite Paint()
        {
            _curve = Curve();
            Texture2D texture = new Texture2D(Width, Height, TextureFormat.RGBA32, false)
            {
                name = "ecf_seal",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontUnloadUnusedAsset,
            };
            Color[] pixels = new Color[Width * Height];
            for (int y = 0; y < Height; y++)
                for (int x = 0; x < Width; x++)
                    pixels[y * Width + x] = Pixel(x, y);
            texture.SetPixels(pixels);
            texture.Apply(false, true);
            Sprite made = Sprite.Create(texture, new Rect(0f, 0f, Width, Height), new Vector2(0.5f, 0.5f), Width);
            made.name = "ecf_seal";
            made.hideFlags = HideFlags.DontUnloadUnusedAsset;
            return made;
        }

        /// <summary>The lemniscate of Bernoulli as a closed polyline.</summary>
        private static Vector2[] Curve()
        {
            var points = new Vector2[CurvePoints + 1];
            for (int i = 0; i <= CurvePoints; i++)
            {
                float t = i * Mathf.PI * 2f / CurvePoints;
                float s = Mathf.Sin(t);
                float d = 1f + s * s;
                points[i] = new Vector2(Reach * Mathf.Cos(t) / d, Reach * s * Mathf.Cos(t) / d);
            }
            return points;
        }

        /// <summary>One pixel, averaged over <see cref="Samples"/> squared points (straight alpha).</summary>
        private static Color Pixel(int px, int py)
        {
            Color sum = Color.clear;
            for (int sy = 0; sy < Samples; sy++)
                for (int sx = 0; sx < Samples; sx++)
                {
                    float x = (px + (sx + 0.5f) / Samples) / Width * 2f - 1f;
                    float y = ((py + (sy + 0.5f) / Samples) / Height * 2f - 1f) * 0.5f;
                    Color c = Point(new Vector2(x, y));
                    sum += new Color(c.r * c.a, c.g * c.a, c.b * c.a, c.a);
                }
            sum /= Samples * Samples;
            return sum.a > 0f ? new Color(sum.r / sum.a, sum.g / sum.a, sum.b / sum.a, sum.a) : Color.clear;
        }

        /// <summary>The red stroke, lighter along its middle; its dark outline; the shadow under both.</summary>
        private static Color Point(Vector2 p)
        {
            float d = Distance(p);
            if (d < Stroke)
            {
                return Color.Lerp(Red, Shine, Mathf.Clamp01(0.6f - d / Stroke * 0.6f));
            }
            if (d < Stroke + Outline)
            {
                return Rim;
            }
            float shadow = Distance(p - ShadowOffset) - Stroke - Outline;
            return shadow < 0.06f ? new Color(0f, 0f, 0f, 0.5f * Mathf.Clamp01(1f - shadow / 0.06f)) : Color.clear;
        }

        private static float Distance(Vector2 p)
        {
            Vector2[] curve = _curve!;
            float best = float.MaxValue;
            for (int i = 0; i + 1 < curve.Length; i++)
            {
                Vector2 ab = curve[i + 1] - curve[i];
                float t = Mathf.Clamp01(Vector2.Dot(p - curve[i], ab) / ab.sqrMagnitude);
                best = Mathf.Min(best, (p - (curve[i] + ab * t)).sqrMagnitude);
            }
            return Mathf.Sqrt(best);
        }
    }
}
