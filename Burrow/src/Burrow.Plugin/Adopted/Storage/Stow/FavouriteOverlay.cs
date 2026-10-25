using System.Runtime.CompilerServices;
using HarmonyLib;

namespace Burrow.Stow
{
    /// <summary><c>Show Favourites</c>: after the grid has drawn its elements, every element shows its marks.
    /// Favourite slots are shown on the player grid only (the grid that is updated with a player).</summary>
    [HarmonyPatch(typeof(InventoryGrid), nameof(InventoryGrid.UpdateGui))]
    public static class FavouriteOverlay
    {
        private static readonly ConditionalWeakTable<InventoryElement, SlotMarks> cachedMarks = new ConditionalWeakTable<InventoryElement, SlotMarks>();

        [HarmonyPostfix]
        public static void Postfix(InventoryGrid __instance, Player player)
        {
            Refresh(__instance, player != null);
        }

        public static void Refresh(InventoryGrid grid, bool playerGrid)
        {
            bool show = StowSettings.Enabled.Value && StowSettings.ShowFavourites.Value;
            Inventory inventory = grid.m_inventory;
            foreach (InventoryElement element in grid.m_elements)
            {
                if (element == null) continue;
                if (!cachedMarks.TryGetValue(element, out SlotMarks marks) || marks == null)
                {
                    marks = element.GetComponent<SlotMarks>();
                    if (marks == null && (!show || inventory == null))
                        continue;
                    if (marks == null)
                        marks = SlotMarks.Attach(element);
                    cachedMarks.Remove(element);
                    cachedMarks.Add(element, marks);
                }
                ItemDrop.ItemData item = show && inventory != null ? inventory.GetItemAt(element.Position.x, element.Position.y) : null;
                bool favouriteItem = item != null && Favourites.IsFavouriteItem(item);
                bool favouriteSlot = show && playerGrid && Favourites.IsFavouriteSlot(element.Position);
                bool junk = item != null && Favourites.IsJunk(item);
                marks.Set(favouriteItem, favouriteSlot, junk);
            }
        }
    }
}
