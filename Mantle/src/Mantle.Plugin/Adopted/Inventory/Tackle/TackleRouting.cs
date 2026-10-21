using HarmonyLib;
using Mantle.Core;
using Mantle.Slots;

namespace Mantle.Tackle
{
    /// <summary>
    /// Bait added to the player's inventory without a cell (pickups, a trader, a click move from a chest) goes into the
    /// tacklebox first, the way keys go to the ring and coins to the purse: onto a stack of the same bait in the box up to
    /// the stack size, then into an empty cell whole. What the box cannot hold goes on through the game's own add (another
    /// stack, then a free main cell). A take all is sorted out after it (<see cref="TakeAllRouting"/>).
    /// </summary>
    [HarmonyPatch(typeof(Inventory), nameof(Inventory.AddItem), new[] { typeof(ItemDrop.ItemData) })]
    public static class TackleRouting
    {
        /// <summary>An add the purse or the key ring already made (an item named in Tackle Items could be either) is left alone.</summary>
        [HarmonyPrefix]
        public static bool Prefix(Inventory __instance, ItemDrop.ItemData item, ref bool __result, bool __runOriginal)
        {
            if (!__runOriginal)
                return false;
            if (item == null || !InventoryState.Manages(__instance) || !Tacklebox.Active || !TackleRules.IsTackle(item) || !TakeIn(__instance, item))
                return true;
            __result = true;
            return false;
        }

        /// <summary>
        /// Moves what fits of an item into the box's cells (<see cref="SlotFill"/>); true when none is left outside it. The
        /// item is either not in the inventory yet (an add) or in a main cell (a take all).
        /// </summary>
        public static bool TakeIn(Inventory inventory, ItemDrop.ItemData item) =>
            SlotFill.TakeIn(inventory, item, InventoryState.CellsOf(SlotKind.Tackle));
    }
}
