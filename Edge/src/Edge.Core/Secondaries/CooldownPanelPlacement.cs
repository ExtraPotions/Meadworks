// Edge - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: where the secondary attack cooldown panel goes (user findings).
namespace ExtraPotions.Edge.Secondaries
{
    public enum CooldownPanelPlace { Hidden, Hud, InventoryPreview }

    public static class CooldownPanelPlacement
    {
        /// <summary>
        /// A full-screen window (map, pause menu, store, lore, build menu, Meadworks windows) hides the panel. The plain
        /// inventory shows the preview beneath the equipment panel, including with a chest open; an inventory page (skills,
        /// texts, trophies) covers it.
        /// </summary>
        public static CooldownPanelPlace Place(bool windowOpen, bool inventoryOpen, bool inventoryCovered) =>
            windowOpen ? CooldownPanelPlace.Hidden
            : !inventoryOpen ? CooldownPanelPlace.Hud
            : inventoryCovered ? CooldownPanelPlace.Hidden
            : CooldownPanelPlace.InventoryPreview;
    }
}
