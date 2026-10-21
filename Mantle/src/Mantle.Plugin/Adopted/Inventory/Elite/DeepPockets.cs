using System;
using ExtraPotions.Mantle.Embedded.EliteCraftingLink;

namespace Mantle.Elite
{
    /// <summary>
    /// Deep Pockets: an EliteCrafting inscription on a backpack (<see cref="EliteSetup"/>) that opens more slots while the
    /// pack is worn. The pack's slots are its own (the YAML's) plus the pack's own capped sum of the <c>pack_slots</c>
    /// effect, rounded (<see cref="Backpacks.BackpackSettings.SlotsOf"/>), so every place that sizes the grid from a worn
    /// or waiting pack (the layout, the frame's check, the grave) counts them the same way and a change in them (a rune,
    /// a carried-over upgrade, a server file that turns the inscription off) re-lays the grid exactly as a pack of that
    /// many slots would: more rows, or fewer with their items moved to free cells and the rest dropped. Read from the
    /// item's own data on the client that holds it; 0 without EliteCrafting or while its inscription effects are off.
    /// </summary>
    public static class DeepPockets
    {
        public static int Extra(ItemDrop.ItemData pack)
        {
            if (pack == null || !CraftingLink.Present)
                return 0;
            float total = CraftingInscriptions.GetItemTotal(pack, EliteDefinitions.SlotsEffect);
            return Math.Max(0, (int)Math.Round(total, MidpointRounding.AwayFromZero));
        }
    }
}
