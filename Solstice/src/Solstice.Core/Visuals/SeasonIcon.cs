// Solstice - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original. Season marks drawn in the Meadworks palette. Row 0 is the bottom, matching a Texture2D upload.
using ExtraPotions.Solstice.Calendar;

namespace ExtraPotions.Solstice.Visuals
{
    /// <summary>A 32 by 32 season mark. Opaque pixels are only amber, brass, cream or charcoal.</summary>
    public static class SeasonIcon
    {
        public const int Size = 32;

        public const byte AmberR = 217, AmberG = 148, AmberB = 54;
        public const byte BrassR = 168, BrassG = 121, BrassB = 56;
        public const byte CreamR = 226, CreamG = 208, CreamB = 168;
        public const byte CharcoalR = 38, CharcoalG = 37, CharcoalB = 34;

        public static byte[] DrawHeat()
        {
            var pixels = new byte[Size * Size * 4];
            Flame(pixels);
            Outline(pixels);
            return pixels;
        }

        public static byte[] Draw(Season season)
        {
            var pixels = new byte[Size * Size * 4];
            switch (season)
            {
                case Season.Spring: Sprout(pixels); break;
                case Season.Summer: Sun(pixels); break;
                case Season.Fall: Leaf(pixels); break;
                default: Crystal(pixels); break;
            }
            Outline(pixels);
            return pixels;
        }

        public static bool IsPalette(byte r, byte g, byte b) =>
            (r == AmberR && g == AmberG && b == AmberB)
            || (r == BrassR && g == BrassG && b == BrassB)
            || (r == CreamR && g == CreamG && b == CreamB)
            || (r == CharcoalR && g == CharcoalG && b == CharcoalB);

        static void Flame(byte[] pixels)
        {
            for (int y = 4; y <= 27; y++)
            {
                int half = FlameHalf(y - 4);
                int cx = y >= 18 ? 17 : 16;
                for (int x = cx - half; x <= cx + half; x++)
                {
                    int dx = x - cx;
                    if (dx < 0) dx = -dx;
                    bool core = y >= 6 && y <= 16 && dx <= (y <= 12 ? 2 : 1);
                    if (core && y <= 11) Plot(pixels, x, y, CreamR, CreamG, CreamB);
                    else if (core) Plot(pixels, x, y, BrassR, BrassG, BrassB);
                    else Plot(pixels, x, y, AmberR, AmberG, AmberB);
                }
            }
            for (int y = 5; y <= 11; y++)
            {
                Plot(pixels, 11, y, AmberR, AmberG, AmberB);
                Plot(pixels, 10, y, BrassR, BrassG, BrassB);
            }
        }

        static int FlameHalf(int rise)
        {
            switch (rise)
            {
                case 0: return 3;
                case 1: return 4;
                case 2: return 5;
                case 3: return 6;
                case 4: return 7;
                case 5: return 7;
                case 6: return 6;
                case 7: return 6;
                case 8: return 5;
                case 9: return 5;
                case 10: return 4;
                case 11: return 4;
                case 12: return 3;
                case 13: return 3;
                case 14: return 3;
                case 15: return 2;
                case 16: return 2;
                case 17: return 2;
                case 18: return 2;
                case 19: return 1;
                case 20: return 1;
                case 21: return 1;
                case 22: return 1;
                default: return 0;
            }
        }

        static void Sun(byte[] pixels)
        {
            int[] dx = { 1, 1, 0, -1, -1, -1, 0, 1 };
            int[] dy = { 0, 1, 1, 1, 0, -1, -1, -1 };
            for (int i = 0; i < 8; i++)
                for (int step = 9; step <= 13; step++)
                    Plot(pixels, 16 + dx[i] * step, 16 + dy[i] * step, BrassR, BrassG, BrassB);
            Disc(pixels, 16, 16, 7, AmberR, AmberG, AmberB);
            Disc(pixels, 16, 16, 3, CreamR, CreamG, CreamB);
        }

        static void Sprout(byte[] pixels)
        {
            for (int y = 2; y <= 14; y++)
            {
                Plot(pixels, 15, y, CreamR, CreamG, CreamB);
                Plot(pixels, 16, y, CreamR, CreamG, CreamB);
            }
            Ellipse(pixels, 9, 16, 6, 4, AmberR, AmberG, AmberB);
            Ellipse(pixels, 22, 17, 6, 4, BrassR, BrassG, BrassB);
            Disc(pixels, 16, 21, 2, CreamR, CreamG, CreamB);
        }

        static void Leaf(byte[] pixels)
        {
            for (int y = 8; y <= 26; y++)
            {
                int half = 9 - Abs(y - 18) * 9 / 10;
                if (half < 1) half = 1;
                for (int x = 16 - half; x <= 16 + half; x++)
                    Plot(pixels, x, y, AmberR, AmberG, AmberB);
            }
            for (int y = 8; y <= 26; y++) Plot(pixels, 16, y, CreamR, CreamG, CreamB);
            for (int y = 2; y <= 8; y++) Plot(pixels, 16, y, BrassR, BrassG, BrassB);
        }

        static void Crystal(byte[] pixels)
        {
            for (int i = 4; i <= 28; i++)
            {
                Plot(pixels, i, 16, CreamR, CreamG, CreamB);
                Plot(pixels, 16, i, CreamR, CreamG, CreamB);
            }
            for (int i = 1; i <= 8; i++)
            {
                Plot(pixels, 16 + i, 16 + i, BrassR, BrassG, BrassB);
                Plot(pixels, 16 - i, 16 + i, BrassR, BrassG, BrassB);
                Plot(pixels, 16 + i, 16 - i, BrassR, BrassG, BrassB);
                Plot(pixels, 16 - i, 16 - i, BrassR, BrassG, BrassB);
            }
            Disc(pixels, 16, 16, 2, BrassR, BrassG, BrassB);
        }

        static void Outline(byte[] pixels)
        {
            var source = new byte[pixels.Length];
            System.Buffer.BlockCopy(pixels, 0, source, 0, pixels.Length);
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    if (Alpha(source, x, y) != 0) continue;
                    if (Alpha(source, x - 1, y) == 0 && Alpha(source, x + 1, y) == 0
                        && Alpha(source, x, y - 1) == 0 && Alpha(source, x, y + 1) == 0)
                        continue;
                    Plot(pixels, x, y, CharcoalR, CharcoalG, CharcoalB);
                }
            }
        }

        static int Alpha(byte[] pixels, int x, int y)
        {
            if ((uint)x >= Size || (uint)y >= Size) return 0;
            return pixels[(y * Size + x) * 4 + 3];
        }

        static void Disc(byte[] pixels, int cx, int cy, int radius, byte r, byte g, byte b)
        {
            int limit = radius * radius;
            for (int y = cy - radius; y <= cy + radius; y++)
                for (int x = cx - radius; x <= cx + radius; x++)
                {
                    int dx = x - cx, dy = y - cy;
                    if (dx * dx + dy * dy <= limit) Plot(pixels, x, y, r, g, b);
                }
        }

        static void Ellipse(byte[] pixels, int cx, int cy, int rx, int ry, byte r, byte g, byte b)
        {
            if (rx < 1 || ry < 1) return;
            int rx2 = rx * rx, ry2 = ry * ry;
            for (int y = cy - ry; y <= cy + ry; y++)
                for (int x = cx - rx; x <= cx + rx; x++)
                {
                    int dx = x - cx, dy = y - cy;
                    if (dx * dx * ry2 + dy * dy * rx2 <= rx2 * ry2) Plot(pixels, x, y, r, g, b);
                }
        }

        static void Plot(byte[] pixels, int x, int y, byte r, byte g, byte b)
        {
            if ((uint)x >= Size || (uint)y >= Size) return;
            int i = (y * Size + x) * 4;
            pixels[i] = r;
            pixels[i + 1] = g;
            pixels[i + 2] = b;
            pixels[i + 3] = 255;
        }

        static int Abs(int value) => value < 0 ? -value : value;
    }
}
