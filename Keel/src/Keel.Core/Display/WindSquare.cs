// Keel - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: the Wind square in the minimap column (user decision 2026-10-12 14:54).
using System;
using System.Globalization;

namespace ExtraPotions.Keel.Display
{
    /// <summary>Keel's Wind square right of the minimap, shown while aboard: the wind relative to the player's heading, and its strength.</summary>
    public static class WindSquare
    {
        /// <summary>Column rank (lower sits higher): Cairn's Comfort 500 above it, Solstice's season 700 below.</summary>
        public const int Rank = 600;
        public const string Id = "keel_wind";

        /// <summary>
        /// Where the wind blows toward, in degrees clockwise from the heading (seen from above), -180 to 180: 0 is straight
        /// ahead (wind from behind), 90 to the right. The game turns its ship wind icon the same way
        /// (minus the yaw of the wind in the ship's frame).
        /// </summary>
        public static float RelativeAngle(float forwardX, float forwardZ, float windX, float windZ)
        {
            if (forwardX * forwardX + forwardZ * forwardZ < 1e-8f || windX * windX + windZ * windZ < 1e-8f) return 0f;
            var angle = (Math.Atan2(windX, windZ) - Math.Atan2(forwardX, forwardZ)) * 180.0 / Math.PI;
            angle = (angle % 360.0 + 540.0) % 360.0 - 180.0;
            return (float)(angle == -180.0 ? 180.0 : angle);
        }

        /// <summary>The wind's strength (the game's intensity, 0 to 1) as a whole percentage.</summary>
        public static string Strength(float intensity)
        {
            if (float.IsNaN(intensity) || float.IsInfinity(intensity)) intensity = 0f;
            var percent = (int)Math.Round(Math.Max(0f, Math.Min(1f, intensity)) * 100f, MidpointRounding.AwayFromZero);
            return percent.ToString(CultureInfo.InvariantCulture) + "%";
        }
    }
}
