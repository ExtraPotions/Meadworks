using HarmonyLib;
using Mantle.Core;

namespace Mantle.Slots
{
    /// <summary>
    /// Arrows, bolts and other equipable ammo added to the player's inventory without a cell (a pickup, crafting, a
    /// trader, a click move from a chest) go into the Ammo slots first (the user's request, 2026-09-28), the way coins go
    /// to the purse: onto a stack of the same ammo in an Ammo slot up to the stack size, left to right, then into an empty
    /// Ammo slot whole (<see cref="SlotFill"/>). What the slots cannot hold goes on through the game's own add (another
    /// stack, then a free main cell). Bait is the tacklebox's (<see cref="Tackle.TackleRouting"/>), so only equipable
    /// ammo is routed here. A take all is sorted out after it (<see cref="TakeAllRouting"/>).
    /// </summary>
    [HarmonyPatch(typeof(Inventory), nameof(Inventory.AddItem), new[] { typeof(ItemDrop.ItemData) })]
    public static class AmmoRouting
    {
        /// <summary>An add another route already made (the purse, the key ring, the tacklebox) is left alone.</summary>
        [HarmonyPrefix]
        public static bool Prefix(Inventory __instance, ItemDrop.ItemData item, ref bool __result, bool __runOriginal)
        {
            if (!__runOriginal)
                return false;
            if (item == null || !InventoryState.Manages(__instance) || !IsAmmo(item) || !TakeIn(__instance, item))
                return true;
            __result = true;
            return false;
        }

        public static bool IsAmmo(ItemDrop.ItemData item) => item.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Ammo;

        /// <summary>Moves what fits of the ammo into the Ammo slots; true when none is left outside them.</summary>
        public static bool TakeIn(Inventory inventory, ItemDrop.ItemData item) =>
            SlotFill.TakeIn(inventory, item, InventoryState.CellsOf(SlotKind.Ammo));
    }
}
