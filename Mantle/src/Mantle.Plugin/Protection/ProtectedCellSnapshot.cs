// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using Mantle.Layout;

namespace Mantle.Protection
{
    public static class ProtectedCellSnapshot
    {
        public const int MaxCells = 4096;
        public static readonly Tuple<bool, IReadOnlyList<Tuple<int, int>>> Unavailable =
            Tuple.Create(false, (IReadOnlyList<Tuple<int, int>>)Array.Empty<Tuple<int, int>>());

        public static Tuple<bool, IReadOnlyList<Tuple<int, int>>> Create(InventoryLayout layout, InventoryLayout wanted,
            int width, int height, bool pending, bool applying, bool enabled, bool hasRecord, InventoryLayout recorded)
        {
            if (pending || applying || layout == null || wanted == null || width < 1 || height < 0 ||
                width > MaxCells || height > MaxCells / width || width != layout.Width || height != layout.Height ||
                !layout.SameAs(wanted) || layout.BaseCells != wanted.BaseCells || layout.BackpackSlots != wanted.BackpackSlots ||
                layout.BaseCells < 0 || layout.BlockedCells < 0 || layout.Slots.Count > 128) return Unavailable;
            if (enabled ? !hasRecord || recorded == null || !layout.SameAs(recorded) :
                hasRecord || layout.Slots.Count != 0 || layout.BackpackSlots != 0 || layout.BlockedCells != 0) return Unavailable;
            var cells = new List<Tuple<int, int>>();
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    var cell = new Vector2i(x, y);
                    if (!layout.IsMain(cell) || layout.IsPackCell(cell)) cells.Add(Tuple.Create(x, y));
                }
            return Tuple.Create(true, (IReadOnlyList<Tuple<int, int>>)cells.AsReadOnly());
        }
    }
}
