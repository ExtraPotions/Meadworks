using System.Collections.Generic;
using Mantle.Core;
using Mantle.Layout;
using Mantle.Slots;

namespace Mantle.Tackle
{
    /// <summary>
    /// The tacklebox a player carries in the Tacklebox slot (the user's request, 2026-09-28: a slot next to the key ring's
    /// button, boxes crafted like the backpacks). While a box lies there the layout has its cells (<see cref="SlotKind.Tackle"/>,
    /// ordinary cells of the player's inventory after every other slot), shown in a pop-up under the slot panel
    /// (<see cref="TacklePopup"/>). The game knows no equip state for it: it is a Misc item, carried by where it lies.
    /// </summary>
    public static class Tacklebox
    {
        /// <summary>The layout in use has tackle cells: a box lies in the slot.</summary>
        public static bool Active => InventoryState.Active && InventoryState.CellsOf(SlotKind.Tackle).Count > 0;

        /// <summary>The kind of the box in the Tacklebox slot of a layout, or null.</summary>
        public static TackleboxKind InSlot(Inventory inventory, InventoryLayout layout)
        {
            IReadOnlyList<Vector2i> cells = layout.CellsOf(SlotKind.Tacklebox);
            return cells.Count > 0 ? TackleboxCatalog.Of(inventory.GetItemAt(cells[0].x, cells[0].y)) : null;
        }

        /// <summary>
        /// The cells the box adds to the next layout. The items still lie where the recorded layout put them, so the
        /// Tacklebox slot is looked up there.
        /// </summary>
        public static int CellsFor(Player player) => TackleboxSettings.Cells(InSlot(player.GetInventory(), LayoutRecord.Read(player)));
    }
}
