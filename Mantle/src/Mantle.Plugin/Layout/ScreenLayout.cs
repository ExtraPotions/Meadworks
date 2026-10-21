// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
using ExtraPotions.Meadworks.Crucible;
using UnityEngine;

namespace Mantle.Panels
{
    /// <summary>
    /// Mantle's side of the Crucible inventory screen layout (<see cref="KitScreen"/>): it says how big its stat column and
    /// equipment panel are and how far its frames reach, so the layout can put the crafting panel, the header and the Extras
    /// dock (product panels such as Burrow's buttons or Edge's cooldown panel) around them without overlapping.
    /// </summary>
    internal static class ScreenLayout
    {
        public static void ReportStatus(Vector2 size) => KitScreen.Report(KitRegion.Status, new KitVector(size.x, size.y));

        public static void ReportEquipment(InventoryGui gui, Vector2 size)
        {
            KitScreen.Registry.InventoryGap = PanelDress.StatsPanelLeft(gui);
            KitScreen.Report(KitRegion.Equipment, new KitVector(size.x, size.y));
        }
    }
}
