// Threshold - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: skip a portal-pin layout when the visible pins, the map view and the spread setting are unchanged.
using System;
using System.Collections.Generic;

namespace ExtraPotions.Threshold.Map
{
    /// <summary>The last portal-pin layout. <see cref="Matches"/> is true when another frame would draw the same places.</summary>
    public sealed class PinLayoutCache
    {
        readonly List<PinPoint> points = new List<PinPoint>();
        MapView view;
        bool spread;
        bool has;

        public bool Matches(MapView view, bool spread, IReadOnlyList<PinPoint> points)
        {
            if (!has || this.spread != spread || points == null || this.points.Count != points.Count || !SameView(this.view, view))
                return false;
            for (int i = 0; i < points.Count; i++)
                if (!this.points[i].Equals(points[i]))
                    return false;
            return true;
        }

        public void Remember(MapView view, bool spread, IReadOnlyList<PinPoint> points)
        {
            if (points == null) throw new ArgumentNullException(nameof(points));
            this.view = view;
            this.spread = spread;
            this.points.Clear();
            for (int i = 0; i < points.Count; i++)
                this.points.Add(points[i]);
            has = true;
        }

        static bool SameView(MapView a, MapView b) =>
            a.UvX == b.UvX && a.UvY == b.UvY && a.UvWidth == b.UvWidth && a.UvHeight == b.UvHeight
            && a.Width == b.Width && a.Height == b.Height;
    }
}
