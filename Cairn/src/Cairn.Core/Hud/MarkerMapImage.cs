// Cairn - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: the Markers tab's map picture (CA07a), composed from the game's own runtime map data. No game art.
using System;
using System.Collections.Generic;

namespace ExtraPotions.Cairn.Hud
{
    /// <summary>
    /// The picture of the Markers map, one RGBA pixel per game map pixel. The game's map texture holds a flat tint per biome
    /// (white for both ocean and mountain) that its large-map shader turns into terrain; drawing the game's shader outside its
    /// own map gave one flat brown square, so the picture is composed here: parchment where the player has not explored, water
    /// below the water level, and a painted color per biome.
    /// </summary>
    public static class MarkerMapImage
    {
        /// <summary>Unexplored parchment, the large map's fog color measured in game.</summary>
        public const uint Fog = 0xB09B71;
        public const uint Water = 0x44606C;

        /// <summary>
        /// The Markers map's opening zoom (map widths across the view's short side): the large map opens close around the player,
        /// so an explored spawn area fills a visible part of the view instead of a few pixels of the whole world.
        /// </summary>
        public const float StartZoom = 8f;

        /// <summary>The game's large map pin size (Minimap.m_pinSizeLarge), used when the running game's value is unusable.</summary>
        public const float LargePinSize = 48f;

        /// <summary>The player arrow's size on the Markers map; pins are drawn at least twice as large so one under the arrow stays readable.</summary>
        public const float ArrowSize = 22f;

        /// <summary>The opacity of a pin not chosen for the compass: it reads clearly, and the fully opaque chosen pins still stand out.</summary>
        public const float DimmedAlpha = 0.7f;

        /// <summary>
        /// The Markers map's pin icon size: the large map's own pin size (<paramref name="largeMapPinSize"/>, read from the game),
        /// or <see cref="LargePinSize"/> when that value is missing or unusable. The view opens at the large map's closeness, so
        /// its pins match the large map's.
        /// </summary>
        public static float PinSize(float largeMapPinSize) =>
            float.IsNaN(largeMapPinSize) || float.IsInfinity(largeMapPinSize) || largeMapPinSize <= 0f ? LargePinSize : largeMapPinSize;

        /// <summary>
        /// The player arrow's z rotation on the north-up Markers map (degrees, counter-clockwise as Unity's UI turns) for a compass
        /// heading (degrees clockwise from north); tip up when the heading is unknown.
        /// </summary>
        public static float PlayerRotation(float heading)
        {
            if (float.IsNaN(heading) || float.IsInfinity(heading)) return 0f;
            var turn = -(heading % 360f);
            if (turn <= -180f) turn += 360f;
            if (turn > 180f) turn -= 360f;
            return turn == 0f ? 0f : turn;
        }

        /// <summary>
        /// Where the Markers map opens: centered on the player's map point (0 to 1 each way) at <see cref="StartZoom"/>; the whole map
        /// at zoom 1 when there is no usable point.
        /// </summary>
        public static (float X, float Y, float Zoom) StartView(float playerX, float playerY)
        {
            bool usable = !float.IsNaN(playerX) && !float.IsInfinity(playerX) && !float.IsNaN(playerY) && !float.IsInfinity(playerY) &&
                playerX >= 0f && playerX <= 1f && playerY >= 0f && playerY <= 1f;
            return usable ? (playerX, playerY, StartZoom) : (.5f, .5f, 1f);
        }

        /// <summary>
        /// The game's biome tints (0xRRGGBB, read from its minimap at run time) mapped to painted colors. Biomes that share a tint
        /// keep the first one listed here (mountain before deep north).
        /// </summary>
        public static IReadOnlyDictionary<uint, uint> Palette(uint meadows, uint blackForest, uint swamp, uint mountain, uint plains, uint mistlands, uint ashLands, uint deepNorth)
        {
            var map = new Dictionary<uint, uint>();
            void Add(uint tint, uint painted) { if (!map.ContainsKey(tint & 0xFFFFFF)) map.Add(tint & 0xFFFFFF, painted); }
            Add(meadows, 0x7A8C46);
            Add(blackForest, 0x42523A);
            Add(swamp, 0x62543E);
            Add(mountain, 0xD2D2CD);
            Add(plains, 0xBAA85C);
            Add(mistlands, 0x5C6068);
            Add(ashLands, 0x783A2C);
            Add(deepNorth, 0xDCE2E8);
            return map;
        }

        /// <summary>
        /// Writes <paramref name="count"/> RGBA pixels into <paramref name="rgba"/>: fog where <paramref name="explored"/> is false,
        /// water where the height is below <paramref name="waterLevel"/>, otherwise the biome's painted color (an unknown tint
        /// passes through).
        /// </summary>
        public static void Compose(byte[] rgba, int count, Func<int, uint> biome, Func<int, bool> explored, Func<int, float> height, float waterLevel, IReadOnlyDictionary<uint, uint> palette)
        {
            if (rgba == null || biome == null || explored == null || height == null || palette == null) throw new ArgumentNullException(nameof(rgba));
            if (count < 0 || rgba.Length < count * 4) throw new ArgumentException("The pixel buffer is shorter than the map.", nameof(rgba));
            for (int i = 0; i < count; i++)
            {
                uint colour;
                if (!explored(i)) colour = Fog;
                else if (height(i) < waterLevel) colour = Water;
                else
                {
                    uint tint = biome(i) & 0xFFFFFF;
                    colour = palette.TryGetValue(tint, out var painted) ? painted : tint;
                }
                int at = i * 4;
                rgba[at] = (byte)(colour >> 16);
                rgba[at + 1] = (byte)(colour >> 8);
                rgba[at + 2] = (byte)colour;
                rgba[at + 3] = 255;
            }
        }
    }
}
