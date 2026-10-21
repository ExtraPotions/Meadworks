using HarmonyLib;

namespace Burrow.Stow
{
    /// <summary>
    /// <c>Auto Sort Containers</c>: the container is sorted when the panel takes it (the game's open response calls
    /// <c>InventoryGui.Show(container)</c>). <c>Auto Sort Inventory</c>: the player inventory is sorted when the
    /// panel goes from hidden to shown, not when a container is swapped into an open panel.
    /// </summary>
    [HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.Show))]
    public static class AutoSortPatch
    {
        [HarmonyPrefix]
        public static void Prefix(InventoryGui __instance, out bool __state)
        {
            __state = InventoryGui.IsVisible();
        }

        [HarmonyPostfix]
        public static void Postfix(Container container, bool __state)
        {
            if (!StowSettings.Enabled.Value || Player.m_localPlayer == null)
                return;
            // A loaded stack/sort mod owns sorting: the row steps aside (Burrow.Integration.StowRowYield), and these two settings with it.
            if (Burrow.Integration.StowRowYield.SteppedAside)
            {
                if (StowSettings.AutoSortContainers.Value || StowSettings.AutoSortInventory.Value)
                    Burrow.Integration.StowRowYield.Report();
                return;
            }
            if (container != null && StowSettings.AutoSortContainers.Value)
                Sorting.SortContainer(container, true);
            if (!__state && StowSettings.AutoSortInventory.Value)
                Sorting.SortPlayer(Player.m_localPlayer, true);
        }
    }
}
