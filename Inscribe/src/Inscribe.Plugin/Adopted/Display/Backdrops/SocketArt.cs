// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.10.0 at d3b57606cb24775d61b7bda244b22eb89e7e7717.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
using UnityEngine;

namespace ExtraPotions.Inscribe.Adopted.Display.Backdrops
{
    /// <summary>
    /// The socket marks' art (<see cref="IconSockets"/>), painted once in code like <see cref="SealArt"/>: <see cref="Ring"/>,
    /// an empty socket (a dark hollow in a silver rim with a dark outline, lit from the upper left), and <see cref="Gem"/>,
    /// the stone set in it (a disc shaded from white to grey, so the Image's colour, the gem's tint, reads as a lit gem).
    /// </summary>
    internal static class SocketArt
    {
        private const int Size = 32;
        private const int Samples = 3;

        // In units where the texture spans -1..1 both ways.
        private const float Outer = 0.98f;
        private const float RimOut = 0.86f;
        private const float RimIn = 0.62f;
        private const float GemRadius = 0.6f;
        private static readonly Vector2 Light = new Vector2(-0.6f, 0.8f);

        private static readonly Color Outline = new Color(0.05f, 0.04f, 0.04f);
        private static readonly Color RimLit = new Color(0.86f, 0.84f, 0.8f);
        private static readonly Color RimDark = new Color(0.36f, 0.34f, 0.32f);
        private static readonly Color Hollow = new Color(0.08f, 0.07f, 0.07f, 0.92f);

        private static Sprite? _ring;
        private static Sprite? _gem;

        public static Sprite Ring => _ring != null ? _ring : _ring = Paint("ecf_socket", RingPoint);

        public static Sprite Gem => _gem != null ? _gem : _gem = Paint("ecf_socket_gem", GemPoint);

        private static Sprite Paint(string name, System.Func<Vector2, Color> point)
        {
            Texture2D texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false)
            {
                name = name,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontUnloadUnusedAsset,
            };
            Color[] pixels = new Color[Size * Size];
            for (int y = 0; y < Size; y++)
                for (int x = 0; x < Size; x++)
                    pixels[y * Size + x] = Pixel(x, y, point);
            texture.SetPixels(pixels);
            texture.Apply(false, true);
            Sprite made = Sprite.Create(texture, new Rect(0f, 0f, Size, Size), new Vector2(0.5f, 0.5f), Size);
            made.name = name;
            made.hideFlags = HideFlags.DontUnloadUnusedAsset;
            return made;
        }

        /// <summary>One pixel, averaged over <see cref="Samples"/> squared points (straight alpha).</summary>
        private static Color Pixel(int px, int py, System.Func<Vector2, Color> point)
        {
            Color sum = Color.clear;
            for (int sy = 0; sy < Samples; sy++)
                for (int sx = 0; sx < Samples; sx++)
                {
                    Vector2 p = new Vector2((px + (sx + 0.5f) / Samples) / Size * 2f - 1f, (py + (sy + 0.5f) / Samples) / Size * 2f - 1f);
                    Color c = point(p);
                    sum += new Color(c.r * c.a, c.g * c.a, c.b * c.a, c.a);
                }
            sum /= Samples * Samples;
            return sum.a > 0f ? new Color(sum.r / sum.a, sum.g / sum.a, sum.b / sum.a, sum.a) : Color.clear;
        }

        // How much a point faces the light: 0 (lower right) .. 1 (upper left).
        private static float Lit(Vector2 p) => Mathf.Clamp01(0.5f + 0.5f * Vector2.Dot(p.normalized, Light.normalized));

        private static Color RingPoint(Vector2 p)
        {
            float r = p.magnitude;
            if (r > Outer)
            {
                return Color.clear;
            }
            if (r > RimOut || (r < RimIn && r > RimIn - 0.06f))
            {
                return Outline;
            }
            // The hollow is shaded the other way round: its upper left wall is in shadow.
            return r >= RimIn ? Color.Lerp(RimDark, RimLit, Lit(p)) : Color.Lerp(Hollow, Hollow * 1.6f, Lit(-p) * 0.6f);
        }

        private static Color GemPoint(Vector2 p)
        {
            float r = p.magnitude;
            if (r > GemRadius)
            {
                return Color.clear;
            }
            float shade = Mathf.Lerp(0.55f, 1f, Lit(p) * (0.4f + 0.6f * r / GemRadius) + (1f - r / GemRadius) * 0.35f);
            return new Color(shade, shade, shade, 1f);
        }
    }
}
