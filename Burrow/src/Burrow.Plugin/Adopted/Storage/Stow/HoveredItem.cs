namespace Burrow.Stow
{
    /// <summary>
    /// The slot under the pointer, found the way the grid finds the element whose tooltip it shows: the gamepad
    /// selection when the grid's UI group is active with an exclusive gamepad or touch, else the element whose
    /// rectangle contains the pointer. The player grid is checked first, then the open container's grid.
    /// </summary>
    public static class HoveredItem
    {
        public static bool Find(InventoryGui gui, out InventoryGrid grid, out Vector2i pos)
        {
            grid = null;
            pos = new Vector2i(-1, -1);
            if (gui == null)
                return false;
            if (TryGrid(gui.m_playerGrid, out pos))
            {
                grid = gui.m_playerGrid;
                return true;
            }
            bool containerShown = gui.m_currentContainer != null && gui.m_container != null && gui.m_container.gameObject.activeSelf;
            if (containerShown && TryGrid(gui.m_containerGrid, out pos))
            {
                grid = gui.m_containerGrid;
                return true;
            }
            return false;
        }

        public static ItemDrop.ItemData Item(InventoryGrid grid, Vector2i pos)
        {
            return grid != null && grid.m_inventory != null ? grid.m_inventory.GetItemAt(pos.x, pos.y) : null;
        }

        private static bool TryGrid(InventoryGrid grid, out Vector2i pos)
        {
            pos = new Vector2i(-1, -1);
            if (grid == null || grid.m_inventory == null || !grid.gameObject.activeInHierarchy)
                return false;
            bool selection = grid.m_uiGroup != null && grid.m_uiGroup.IsActive && (ZInput.IsExclusiveGamepadActive() || ZInput.IsTouchActive());
            if (selection)
            {
                pos = grid.m_selected;
                return pos.x >= 0 && pos.y >= 0;
            }
            InventoryElement element = grid.GetHoveredElement();
            if (element == null)
                return false;
            pos = grid.GetElementPos(element);
            return pos.x >= 0;
        }
    }
}
