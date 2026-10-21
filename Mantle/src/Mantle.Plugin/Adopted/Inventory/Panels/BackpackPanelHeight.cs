using Mantle.Core;
using Mantle.Layout;
using UnityEngine;

namespace Mantle.Panels
{
    /// <summary>
    /// The height the side panels (the stats panel and the slot panel) take: the inventory panel's as it would be without
    /// the worn backpack's cells (the user's call, 2026-09-28: a backpack's rows grow only the inventory panel) and never
    /// less than with the game's 4 rows. The inventory panel itself follows its rows however few (the user's call,
    /// 2026-10-02: with only the two hand cells OpenKeep's buttons sit right under them), and the side panels hang from
    /// its top at their full height. Each row is the game's grid row height (<c>m_invGridHeight</c>), as
    /// <see cref="PanelSize"/> adds it.
    /// </summary>
    public static class BackpackPanelHeight
    {
        public static float Of(InventoryGui gui)
        {
            if (!InventoryState.Active)
                return gui.m_player.sizeDelta.y;
            InventoryLayout layout = InventoryState.Layout;
            int sideRows = Mathf.Max(InventorySettings.GameRows, layout.BaseRows);
            return gui.m_player.sizeDelta.y + (sideRows - layout.MainRows) * gui.m_invGridHeight;
        }
    }
}
