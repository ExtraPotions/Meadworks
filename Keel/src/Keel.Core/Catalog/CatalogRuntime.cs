// Keel - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: the rules the catalog runtime (K07b) applies to its hull clones: shape, hold size, hull paint
// bands and hammer order. TheGreatestShips is a behavior reference only (its README: ships sit in the hammer right
// after their vanilla ship; width, length and overall scale; painted bands, 0 paints solid). No code was read.
using System;
using System.Collections.Generic;
using System.Linq;

namespace ExtraPotions.Keel.Catalog
{
    /// <summary>How a hull clone is shaped (design 4.3): the root's local scale and the ship offsets measured in meters.</summary>
    public static class ShipShape
    {
        public const float Minimum = 0.5f;
        public const float Maximum = 2f;

        /// <summary>Root local scale: X (beam) = width x scale, Y (height) = scale, Z (bow to stern) = length x scale.</summary>
        public static (float X, float Y, float Z) LocalScale(float width, float length, float scale)
        {
            var w = Clamp(width);
            var l = Clamp(length);
            var s = Clamp(scale);
            return (w * s, s, l * s);
        }

        /// <summary>
        /// The ship fields the game measures in world meters from the ship's origin, so they do not follow the root scale:
        /// the rudder lever (<c>m_stearForceOffset</c>) grows with the length, the sail force height
        /// (<c>m_sailForceOffset</c>) and the floating draft (<c>m_waterLevelOffset</c>) with the overall scale.
        /// </summary>
        public static (float SteerForceOffset, float SailForceOffset, float WaterLevelOffset) Offsets(float steerForceOffset, float sailForceOffset,
            float waterLevelOffset, float width, float length, float scale)
        {
            var size = LocalScale(width, length, scale);
            return (steerForceOffset * size.Z, sailForceOffset * size.Y, waterLevelOffset * size.Y);
        }

        private static float Clamp(float value) => float.IsNaN(value) || float.IsInfinity(value) ? 1f : Math.Min(Maximum, Math.Max(Minimum, value));
    }

    /// <summary>
    /// The ship's hold (Cargo Columns and Rows). The game drops a stored item whose slot lies outside its container's grid
    /// when it loads the hold, so a hold is never made smaller than its items need.
    /// </summary>
    public static class HoldSize
    {
        public const int MaxColumns = 12;
        public const int MaxRows = 10;

        /// <summary>Ship ZDO keys of the hold size the items were last kept at (written by the ship's owner).</summary>
        public const string ColumnsKey = KeelCore.ZdoPrefix + "hold_columns";
        public const string RowsKey = KeelCore.ZdoPrefix + "hold_rows";

        /// <summary>The configured size (clamped to 1..12 by 1..10), grown per dimension to what the items need.</summary>
        public static (int Columns, int Rows) Resolve(int columns, int rows, int neededColumns, int neededRows) =>
            (Math.Max(Math.Min(MaxColumns, Math.Max(1, columns)), neededColumns), Math.Max(Math.Min(MaxRows, Math.Max(1, rows)), neededRows));

        /// <summary>The smallest grid that holds every occupied slot; (0, 0) for an empty hold.</summary>
        public static (int Columns, int Rows) Needed(IEnumerable<(int X, int Y)> occupied)
        {
            int columns = 0, rows = 0;
            foreach (var (x, y) in occupied ?? Enumerable.Empty<(int, int)>())
            {
                columns = Math.Max(columns, x + 1);
                rows = Math.Max(rows, y + 1);
            }
            return (columns, rows);
        }

        /// <summary>
        /// Whether a saved hold (the ZDO's item package, <paramref name="byteLength"/> bytes, negative when none) holds
        /// items: an empty hold saves only its version and item count, 6 bytes now and 8 in older saves; any item adds more.
        /// </summary>
        public static bool HasItems(int byteLength) => byteLength > 8;

        /// <summary>
        /// Before the items are loaded only their presence is known: an empty hold needs nothing; a stored hold needs the
        /// size it was kept at; a hold without a stored size (built before Keel recorded one) needs the catalog default.
        /// </summary>
        public static (int Columns, int Rows) NeededAtLoad(bool hasItems, int storedColumns, int storedRows, int defaultColumns, int defaultRows)
        {
            if (!hasItems) return (0, 0);
            return storedColumns > 0 && storedRows > 0 ? (storedColumns, storedRows) : (defaultColumns, defaultRows);
        }
    }

    /// <summary>Hull paint (design 4.3): stripes split the planks' height into 2n+1 equal bands, the odd ones painted.</summary>
    public static class HullPaint
    {
        /// <summary>Whether paint covers height <paramref name="v"/> (0 bottom to 1 top of the plank texture); 0 stripes paints everything.</summary>
        public static bool Painted(float v, int stripes)
        {
            if (stripes <= 0) return true;
            var t = float.IsNaN(v) ? 0f : Math.Min(1f, Math.Max(0f, v));
            var bands = 2 * stripes + 1;
            var band = Math.Min(bands - 1, (int)(t * bands));
            return band % 2 == 1;
        }

        /// <summary>The number of separate painted runs in a sampled column (a test and log helper).</summary>
        public static int Bands(IEnumerable<bool> column)
        {
            var count = 0;
            var previous = false;
            foreach (var painted in column)
            {
                if (painted && !previous) count++;
                previous = painted;
            }
            return count;
        }
    }

    /// <summary>The hammer's piece order: each Keel ship sits right after the vanilla ship it is built from (README).</summary>
    public static class HammerOrder
    {
        /// <summary>
        /// The pieces with every listed Keel ship moved behind its base hull, ships of one hull in catalog order. A ship
        /// whose base hull is not in the table goes to the end; Keel ships not in the table (disabled) stay out.
        /// </summary>
        public static IReadOnlyList<string> Arrange(IEnumerable<string> pieces, IEnumerable<ShipType> catalog)
        {
            var list = (pieces ?? Enumerable.Empty<string>()).ToList();
            var ships = (catalog ?? Enumerable.Empty<ShipType>()).Where(t => list.Contains(t.Prefab)).ToList();
            var keel = new HashSet<string>(ships.Select(t => t.Prefab), StringComparer.Ordinal);
            var result = list.Where(p => !keel.Contains(p)).ToList();
            var orphans = new List<string>();
            foreach (var hull in ships.Select(t => t.BaseHull).Distinct())
            {
                var at = result.IndexOf(hull);
                var group = ships.Where(t => t.BaseHull == hull).Select(t => t.Prefab).ToList();
                if (at < 0) orphans.AddRange(group);
                else result.InsertRange(at + 1, group);
            }
            result.AddRange(ships.Select(t => t.Prefab).Where(orphans.Contains));
            return result;
        }
    }
}
