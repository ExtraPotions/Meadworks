// Portions Copyright (C) 2026 Gerald Glasgow. EliteCreaturesPack 0.7.2 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Changed by expDARE, 2026: Bestiary namespace, owned Crucible settings, rebuilt-only bundles and lifecycle adapters.
// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using UnityEngine;

namespace ExtraPotions.Bestiary.Runtime.Native.Kraken
{
    /// <summary>
    /// Ink splats drawn in code, for when the kraken's asset bundle brought none: a black-indigo blob with a ragged edge
    /// and a faint sheen, splash arms and a spray of droplets around it, and a few drips running down, all with soft
    /// edges. Drawn once, from fixed seeds, the first time a screen is inked.
    /// </summary>
    internal static class InkSplats
    {
        private const int Size = 256;
        private const int Count = 4;
        private const float Soft = 3f / Size;
        private static readonly Color Deep = new Color(0.02f, 0.015f, 0.05f);
        private static readonly Color Sheen = new Color(0.1f, 0.07f, 0.2f);

        public static Texture2D[] Build()
        {
            var made = new Texture2D[Count];
            for (int i = 0; i < Count; i++)
            {
                made[i] = Paint(i, new System.Random(7919 * (i + 1)));
            }
            return made;
        }

        private static Texture2D Paint(int index, System.Random rng)
        {
            var cover = new float[Size * Size];
            var centre = new Vector2(0.5f, 0.56f);
            float radius = Range(rng, 0.22f, 0.27f);
            Blob(cover, centre, radius, rng);
            Arms(cover, centre, radius, rng);
            Droplets(cover, centre, radius, rng);
            Drips(cover, centre, radius, rng);
            return Bake(index, cover, centre, radius);
        }

        /// <summary>The body: a disc whose edge wobbles with three waves of random phase.</summary>
        private static void Blob(float[] cover, Vector2 centre, float radius, System.Random rng)
        {
            float p1 = Range(rng, 0f, 6.3f), p2 = Range(rng, 0f, 6.3f), p3 = Range(rng, 0f, 6.3f);
            Stamp(cover, centre, radius * 1.3f + Soft, p =>
            {
                Vector2 d = p - centre;
                float a = Mathf.Atan2(d.y, d.x);
                float wobble = 0.12f * Mathf.Sin(3f * a + p1) + 0.08f * Mathf.Sin(5f * a + p2)
                    + 0.05f * Mathf.Sin(9f * a + p3);
                return (radius * (1f + wobble) - d.magnitude) / Soft;
            });
        }

        /// <summary>Splash arms: chains of shrinking discs thrown out from the body's edge.</summary>
        private static void Arms(float[] cover, Vector2 centre, float radius, System.Random rng)
        {
            int arms = rng.Next(6, 10);
            for (int i = 0; i < arms; i++)
            {
                float angle = Range(rng, 0f, Mathf.PI * 2f);
                var dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                float reach = Range(rng, 1.3f, 1.6f);
                for (int step = 0; step < 4; step++)
                {
                    float t = step / 3f;
                    Disc(cover, Inside(centre + dir * (radius * Mathf.Lerp(0.85f, reach, t))), Mathf.Lerp(0.07f, 0.018f, t));
                }
            }
        }

        /// <summary>Loose droplets sprayed around the body.</summary>
        private static void Droplets(float[] cover, Vector2 centre, float radius, System.Random rng)
        {
            int drops = rng.Next(10, 17);
            for (int i = 0; i < drops; i++)
            {
                float angle = Range(rng, 0f, Mathf.PI * 2f);
                var dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                Disc(cover, Inside(centre + dir * (radius * Range(rng, 1.2f, 1.75f))), Range(rng, 0.008f, 0.025f));
            }
        }

        /// <summary>Two or three drips running down from the body, each ending in a round drop.</summary>
        private static void Drips(float[] cover, Vector2 centre, float radius, System.Random rng)
        {
            int drips = rng.Next(2, 4);
            for (int i = 0; i < drips; i++)
            {
                float width = Range(rng, 0.012f, 0.022f);
                var top = new Vector2(centre.x + radius * Range(rng, -0.6f, 0.6f), centre.y - radius * 0.6f);
                var bottom = new Vector2(top.x, Mathf.Max(0.06f, top.y - Range(rng, 0.14f, 0.3f)));
                Stamp(cover, (top + bottom) * 0.5f, (top.y - bottom.y) * 0.5f + width + Soft,
                    p => (width - Segment(p, top, bottom)) / Soft);
                Disc(cover, bottom, width * 1.5f);
            }
        }

        private static void Disc(float[] cover, Vector2 at, float radius) =>
            Stamp(cover, at, radius + Soft, p => (radius - Vector2.Distance(p, at)) / Soft);

        /// <summary>Draws one shape into the coverage, over the square of half-width <paramref name="reach"/> around it.</summary>
        private static void Stamp(float[] cover, Vector2 at, float reach, Func<Vector2, float> shape)
        {
            int x0 = Pixel(at.x - reach), x1 = Pixel(at.x + reach);
            int y0 = Pixel(at.y - reach), y1 = Pixel(at.y + reach);
            for (int y = y0; y <= y1; y++)
            {
                for (int x = x0; x <= x1; x++)
                {
                    int i = y * Size + x;
                    cover[i] = Mathf.Max(cover[i], Mathf.Clamp01(shape(Point(x, y))));
                }
            }
        }

        /// <summary>Ink colour, darkest in the middle with a faint sheen up and to the left; coverage as alpha.</summary>
        private static Texture2D Bake(int index, float[] cover, Vector2 centre, float radius)
        {
            var pixels = new Color32[cover.Length];
            Vector2 shine = centre + new Vector2(-0.3f, 0.35f) * radius;
            for (int i = 0; i < cover.Length; i++)
            {
                float gloss = 0.6f * Mathf.Clamp01(1f - Vector2.Distance(Point(i % Size, i / Size), shine) / (0.7f * radius));
                Color colour = Color.Lerp(Deep, Sheen, gloss);
                colour.a = cover[i];
                pixels[i] = colour;
            }
            Texture2D texture = Blank(index);
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }

        private static Texture2D Blank(int index) => new Texture2D(Size, Size, TextureFormat.RGBA32, false)
        {
            name = "ecp_kraken_ink_drawn_" + index,
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
            hideFlags = HideFlags.DontUnloadUnusedAsset,
        };

        private static float Segment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(ab.sqrMagnitude, 1e-6f));
            return Vector2.Distance(p, a + ab * t);
        }

        private static Vector2 Point(int x, int y) => new Vector2((x + 0.5f) / Size, (y + 0.5f) / Size);

        private static int Pixel(float at) => Mathf.Clamp(Mathf.FloorToInt(at * Size), 0, Size - 1);

        private static Vector2 Inside(Vector2 at) => new Vector2(Mathf.Clamp(at.x, 0.05f, 0.95f), Mathf.Clamp(at.y, 0.05f, 0.95f));

        private static float Range(System.Random rng, float from, float to) => from + (float)rng.NextDouble() * (to - from);
    }
}
