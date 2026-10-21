using System;
using HarmonyLib;
using Mantle.Core;

namespace Mantle.Panels
{
    /// <summary>
    /// <c>InventoryGui.Show(Container, int)</c> prefix: the crafting panel's size (<see cref="CraftingPanel"/>) is set
    /// before the game builds the recipe list (Show calls <c>UpdateCraftingPanel</c>), so the rows are made at the new
    /// width; it follows the screen, the inventory's own panels and the settings each time the inventory opens. A change
    /// of Crafting Panel Width or Height applies at once to an open inventory.
    /// </summary>
    [HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.Show))]
    public static class CraftingPanelPatch
    {
        private static bool hooked;

        [HarmonyPrefix]
        public static void Prefix(InventoryGui __instance)
        {
            Hook();
            CraftingPanel.Apply(__instance);
        }

        /// <summary>Takes the handlers off again (the plugin is going away).</summary>
        public static void Unhook()
        {
            if (!hooked)
                return;
            hooked = false;
            InventorySettings.CraftingWidth.SettingChanged -= Changed;
            InventorySettings.CraftingHeight.SettingChanged -= Changed;
            ExtraPotions.Meadworks.Crucible.UI.KitInventoryScreen.RegionsChanged -= Reapply;
        }

        private static void Hook()
        {
            if (hooked)
                return;
            hooked = true;
            InventorySettings.CraftingWidth.SettingChanged += Changed;
            InventorySettings.CraftingHeight.SettingChanged += Changed;
            ExtraPotions.Meadworks.Crucible.UI.KitInventoryScreen.RegionsChanged += Reapply;
        }

        /// <summary>The layout gave the crafting column another size (a new screen, another panel size reported).</summary>
        private static void Reapply() => Changed(null, EventArgs.Empty);

        /// <summary>Logged, never thrown back into the setting's change (a cfg reload must go on).</summary>
        private static void Changed(object sender, EventArgs args)
        {
            try
            {
                InventoryGui gui = InventoryGui.instance;
                if (gui == null || !InventoryGui.IsVisible() || Player.m_localPlayer == null)
                    return;
                // Only a new size needs the recipe rows built again; the layout reports on every open and while it settles,
                // and a refresh each time logged "Setting selected recipe" 4 to 5 times per open (play test, 2026-10-13).
                if (CraftingPanel.Apply(gui))
                    gui.UpdateCraftingPanel();
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"Mantle: resizing the crafting panel failed: {e}");
            }
        }
    }
}
