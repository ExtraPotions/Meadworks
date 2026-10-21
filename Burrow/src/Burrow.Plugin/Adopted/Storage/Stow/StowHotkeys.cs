using HarmonyLib;
using Burrow.Core;

namespace Burrow.Stow
{
    /// <summary>
    /// Polls the inventory hotkeys once per frame after the game's own inventory update, only while the panel is
    /// visible and no popup or split dialog is up. Combined shortcuts are checked before the single key they share
    /// (Shift+G before G, Shift+F before F, Shift+Delete before Delete); the core's single-key rule keeps them apart anyway.
    /// </summary>
    [HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.Update))]
    public static class StowHotkeys
    {
        [HarmonyPostfix]
        public static void Postfix(InventoryGui __instance)
        {
            PanelButtons.Follow(__instance);
            Poll(__instance);
        }

        public static void Poll(InventoryGui gui)
        {
            Burrow.Integration.TrashCanMode.Tick();
            TrashMode.Tick(gui);
            if (!StowSettings.Enabled.Value || !Keys.InventoryOpen || Player.m_localPlayer == null || UnifiedPopup.IsVisible())
                return;
            if (gui.m_splitDialog != null && gui.m_splitDialog.IsActive)
                return;
            PollActions(gui);
            PollHovered(gui);
            Cycling.Poll(gui);
        }

        private static void PollActions(InventoryGui gui)
        {
            // The row's own keys step aside with it (Burrow.Integration.StowRowYield); take all and the rest stay.
            bool row = !Burrow.Integration.StowRowYield.SteppedAside;
            if (row && Keys.Pressed(StowSettings.QuickStackKey))
                StowActions.QuickStack();
            else if (Keys.Pressed(StowSettings.TakeAllKey))
                StowActions.TakeAll(gui);
            else if (row && Keys.Pressed(StowSettings.StoreAllKey))
                StowActions.StoreAll();
            else if (row && Keys.Pressed(StowSettings.TopUpKey))
                TopUp.Run();
            else if (row && Keys.Pressed(StowSettings.SortInventoryKey))
                Sorting.SortPlayer(Player.m_localPlayer, false);
            else if (row && Keys.Pressed(StowSettings.SortContainerKey))
                Sorting.SortOpenContainer();
            else if (Keys.Pressed(StowSettings.FeedSourceKey))
                Burrow.Integration.FeedSources.ToggleOpen();
            else if (Keys.Pressed(StowSettings.DestroyJunkKey))
                Trash.DestroyJunk(gui);
            else if (Keys.Pressed(StowSettings.TrashKey))
                Trash.TrashHovered(gui);
        }

        private static void PollHovered(InventoryGui gui)
        {
            if (!HoveredItem.Find(gui, out InventoryGrid grid, out Vector2i pos))
                return;
            bool playerGrid = grid == gui.m_playerGrid;
            ItemDrop.ItemData item = HoveredItem.Item(grid, pos);
            if (Keys.Pressed(StowSettings.FavouriteSlotKey))
            {
                if (playerGrid)
                    Favourites.ToggleSlotWithMessage(pos);
            }
            else if (item == null)
                return;
            else if (Keys.Pressed(StowSettings.FavouriteItemKey))
                Favourites.ToggleItemWithMessage(item);
            else if (Keys.Pressed(StowSettings.JunkKey))
                Favourites.ToggleJunkWithMessage(item);
            else if (Keys.Pressed(StowSettings.FindKey))
                Finder.Find(item);
            else if (playerGrid && Keys.Pressed(StowSettings.StoreOneKey))
                Routing.StoreOne(item);
        }
    }
}
