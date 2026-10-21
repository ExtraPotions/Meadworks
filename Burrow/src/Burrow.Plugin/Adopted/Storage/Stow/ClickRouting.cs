using HarmonyLib;
using Burrow.Core;

namespace Burrow.Stow
{
    /// <summary>
    /// <c>Route Modifier</c> + left click on a player inventory item: the click is handled here and the game's own
    /// handling (with the default LeftControl, the game would move the item to the open container) is skipped,
    /// also when no target exists, as the spec asks. Clicks with a dragged item, on the container grid or without
    /// the modifier are the game's.
    /// </summary>
    [HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.OnSelectedItem))]
    public static class ClickRouting
    {
        [HarmonyPrefix]
        public static bool Prefix(InventoryGui __instance, InventoryGrid grid, ItemDrop.ItemData item, InventoryGrid.Modifier mod)
        {
            if (!StowSettings.Enabled.Value || item == null || __instance.m_dragGo != null || grid != __instance.m_playerGrid)
                return true;
            if (mod == InventoryGrid.Modifier.Drop || !Keys.Held(StowSettings.RouteModifier))
                return true;
            Routing.Route(item);
            return false;
        }
    }
}
