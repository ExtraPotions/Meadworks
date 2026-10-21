using System;
using UnityEngine;

namespace Burrow.Stow
{
    /// <summary>The Stow module's sprites. The slot marks are small white shapes drawn in code, coloured by the Image
    /// that shows them: a star, a cross and a hollow border (nine-sliced). The trash can's icon is drawn in code too
    /// (Burrow ships no image of its own for it): an iron bin with a lid, in its own colours.</summary>
    public static class StowSprites
    {
        private const int MarkSize = 16;
        private const int BinSize = 64;

        private static Sprite star;
        private static Sprite cross;
        private static Sprite border;
        private static Sprite bin;

        public static Sprite Star => star != null ? star : (star = Make(MarkSize, StarPixel, Vector4.zero));

        public static Sprite Cross => cross != null ? cross : (cross = Make(MarkSize, CrossPixel, Vector4.zero));

        public static Sprite Border => border != null ? border : (border = Make(MarkSize, BorderPixel, new Vector4(3f, 3f, 3f, 3f)));

        /// <summary>The trash can's icon.</summary>
        public static Sprite Bin => bin != null ? bin : (bin = MakeBin(BinSize));

        private static Sprite Make(int size, Func<int, int, int, bool> inside, Vector4 slices)
        {
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                    texture.SetPixel(x, y, inside(x, y, size) ? Color.white : Color.clear);
            }
            texture.Apply();
            Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, slices);
            sprite.name = "Burrow_sprite";
            return sprite;
        }

        /// <summary>The bin: a tapered iron body with ribs, a lid with a handle, a dark rim around both.</summary>
        private static Sprite MakeBin(int size)
        {
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, true)
            {
                filterMode = FilterMode.Trilinear,
                wrapMode = TextureWrapMode.Clamp,
            };
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                    texture.SetPixel(x, y, BinPixel(x, y, size));
            }
            texture.Apply(true);
            Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
            sprite.name = "Burrow_bin";
            return sprite;
        }

        private static readonly Color Iron = new Color(0.62f, 0.60f, 0.56f, 1f);
        private static readonly Color IronDark = new Color(0.40f, 0.38f, 0.35f, 1f);
        private static readonly Color Rim = new Color(0.12f, 0.10f, 0.08f, 1f);

        private static Color BinPixel(int x, int y, int size)
        {
            float u = (x + 0.5f) / size, v = (y + 0.5f) / size;   // v = 0 at the bottom
            bool body = BinBody(u, v), lid = BinLid(u, v), handle = BinHandle(u, v);
            if (!body && !lid && !handle)
            {
                float o = 1.5f / size;
                bool near = BinBody(u + o, v) || BinBody(u - o, v) || BinBody(u, v + o) || BinBody(u, v - o)
                    || BinLid(u + o, v) || BinLid(u - o, v) || BinLid(u, v + o) || BinLid(u, v - o)
                    || BinHandle(u + o, v) || BinHandle(u - o, v) || BinHandle(u, v + o) || BinHandle(u, v - o);
                return near ? Rim : Color.clear;
            }
            if (body)
            {
                float half = Mathf.Lerp(0.30f, 0.36f, (v - 0.08f) / 0.62f);
                float rib = Mathf.Abs(Mathf.Repeat((u - 0.5f) / half * 2f + 1f, 2f / 3f) - 1f / 3f);
                float shade = 0.85f + 0.15f * (1f - Mathf.Abs(u - 0.45f) / 0.36f);
                return (rib < 0.05f ? IronDark : Iron) * shade + new Color(0f, 0f, 0f, 1f - shade);
            }
            return lid ? Iron : IronDark;
        }

        private static bool BinBody(float u, float v)
        {
            if (v < 0.08f || v > 0.70f) return false;
            float half = Mathf.Lerp(0.30f, 0.36f, (v - 0.08f) / 0.62f);
            return Mathf.Abs(u - 0.5f) <= half;
        }

        private static bool BinLid(float u, float v) => v >= 0.73f && v <= 0.81f && Mathf.Abs(u - 0.5f) <= 0.41f;

        private static bool BinHandle(float u, float v) => v > 0.81f && v <= 0.90f && Mathf.Abs(u - 0.5f) <= 0.11f && !(v < 0.86f && Mathf.Abs(u - 0.5f) < 0.06f);

        /// <summary>A five pointed star: the radius at an angle swings between the inner and the outer radius.</summary>
        private static bool StarPixel(int x, int y, int size)
        {
            float half = size / 2f;
            float dx = x + 0.5f - half;
            float dy = y + 0.5f - half;
            float distance = Mathf.Sqrt(dx * dx + dy * dy) / half;
            float turns = (Mathf.Atan2(dy, dx) / (2f * Mathf.PI) + 0.25f) * 5f;
            float toPoint = 1f - Mathf.Abs(turns - Mathf.Floor(turns) - 0.5f) * 2f;
            float radius = 0.42f + (0.98f - 0.42f) * (1f - toPoint);
            return distance <= radius;
        }

        private static bool CrossPixel(int x, int y, int size)
        {
            const float inset = 2f;
            const float thickness = 1.6f;
            if (x < inset || y < inset || x >= size - inset || y >= size - inset)
                return false;
            return Mathf.Abs(x - y) <= thickness || Mathf.Abs(x + y - (size - 1)) <= thickness;
        }

        private static bool BorderPixel(int x, int y, int size)
        {
            const int width = 2;
            return x < width || y < width || x >= size - width || y >= size - width;
        }
    }
}
