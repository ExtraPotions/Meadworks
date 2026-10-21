// Tempest - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: the player-window set Tempest notices stay clear of (same set as Ember's sleep HUD).
using ExtraPotions.Meadworks.Crucible.UI;

namespace ExtraPotions.Tempest.Runtime
{
    public static class TempestWindows
    {
        /// <summary>
        /// True while a player window is open: inventory, containers, crafting and skills (InventoryGui), the large map,
        /// the Compendium, the pause menu, a trader, text entry or viewer, or any MeadWorks window that hides the HUD.
        /// </summary>
        public static bool Open() => KitWindows.HudBlocked || Minimap.IsOpen() || InventoryGui.IsVisible() || Compendium() || Menu.IsVisible()
            || StoreGui.IsVisible() || TextInput.IsVisible() || (TextViewer.instance != null && TextViewer.instance.IsVisible());

        static bool Compendium()
        {
            var texts = InventoryGui.instance != null ? InventoryGui.instance.m_textsDialog : null;
            return texts != null && texts.gameObject.activeInHierarchy;
        }
    }
}
