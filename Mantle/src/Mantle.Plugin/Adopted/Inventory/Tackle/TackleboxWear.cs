using Mantle.Core;
using Mantle.Layout;
using Mantle.Slots;

namespace Mantle.Tackle
{
    /// <summary>
    /// The local player's frame (from <see cref="PlayerTick"/>). When a tacklebox went into or out of its slot, or its
    /// cells changed (the YAML, a setting), the layout is applied again: cells added, or taken away with what they held
    /// moved to free cells and the rest dropped at the player's feet, as for a backpack. Only in Update, and never for a
    /// dead player: Keep Slots On Death takes the box out for the length of <c>CreateTombStone</c>, which no frame sees.
    /// </summary>
    public static class TackleboxWear
    {
        public static void Tick(Player player)
        {
            if (player.IsDead() || !InventoryState.IsLocal(player) || !InventoryState.Active)
                return;
            TackleboxKind box = Tacklebox.InSlot(player.GetInventory(), InventoryState.Layout);
            if (InventoryState.CellsOf(SlotKind.Tackle).Count != TackleboxSettings.Cells(box))
                LayoutApply.Apply(player, dropOverflow: true);
        }
    }
}
