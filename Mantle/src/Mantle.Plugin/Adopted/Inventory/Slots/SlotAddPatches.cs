using HarmonyLib;
using Mantle.Core;

namespace Mantle.Slots
{
    /// <summary>
    /// Every way an item reaches a given cell of the player's inventory keeps the slot rules. The grid's drop is
    /// checked first (<see cref="SlotDrop"/>), with a message. The positional add underneath it also serves the game's
    /// take all, which puts each item back at its old cell when that cell is free: a slot refuses what it does not take,
    /// and a take all from anything but the player's own grave (<see cref="GravePatches"/>) keeps out of the slots
    /// entirely, so the refused items go through the game's normal add into the grid. The load passes the game's
    /// skip flag and is never refused: the layout sorts the saved cells out afterwards.
    /// </summary>
    public static class SlotAddPatches
    {
        [HarmonyPatch(typeof(InventoryGrid), nameof(InventoryGrid.DropItem))]
        public static class Drop
        {
            [HarmonyPrefix]
            [HarmonyPriority(Priority.High)]
            public static bool Prefix(InventoryGrid __instance, Inventory fromInventory, ItemDrop.ItemData item, int amount, Vector2i pos, ref bool __result)
            {
                if (SlotDrop.Allowed(__instance.GetInventory(), fromInventory, item, amount, pos))
                    return true;
                Messages.Center(Words.WrongSlot);
                __result = false;
                return false;
            }
        }

        [HarmonyPatch(typeof(Inventory), nameof(Inventory.AddItem), new[] { typeof(ItemDrop.ItemData), typeof(int), typeof(int), typeof(int), typeof(bool) })]
        public static class AtCell
        {
            [HarmonyPrefix]
            public static bool Prefix(Inventory __instance, ItemDrop.ItemData item, int x, int y, bool skipValidPositionCheck, ref bool __result)
            {
                if (skipValidPositionCheck || !InventoryState.Manages(__instance))
                    return true;
                Vector2i pos = new Vector2i(x, y);
                if (InventoryState.Layout.IsMain(pos) || (!GravePatches.ForeignTakeAll && SlotDrop.Takes(__instance, pos, item)))
                    return true;
                __result = false;
                return false;
            }
        }

        [HarmonyPatch(typeof(Inventory), nameof(Inventory.AddItem), new[] { typeof(ItemDrop.ItemData), typeof(Vector2i) })]
        public static class AtPosition
        {
            [HarmonyPrefix]
            public static bool Prefix(Inventory __instance, ItemDrop.ItemData item, Vector2i pos, ref bool __result)
            {
                if (SlotDrop.Takes(__instance, pos, item))
                    return true;
                __result = __instance.AddItem(item);
                return false;
            }
        }
    }
}
