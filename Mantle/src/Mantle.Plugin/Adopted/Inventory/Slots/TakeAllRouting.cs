using System.Linq;
using HarmonyLib;

namespace Mantle.Slots
{
    /// <summary>
    /// A take all into the player's inventory (<c>Inventory.MoveAll</c>) from a chest puts each item back at its old cell
    /// when that cell is free, so a chest's top row would land on the hotbar. Here each chest item goes through the normal
    /// <c>Inventory.AddItem</c> instead, in the chest's reading order: the main grid first, then a legal extra slot, the hotbar
    /// last, and keys, bait and ammo into their own cells (<see cref="Mantle.Layout.LegalSlotRouting"/>,
    /// <see cref="Mantle.Ring.KeyRouting"/>, <see cref="AmmoRouting"/>, <see cref="Mantle.Tackle.TackleRouting"/>). Only the
    /// quantity the inventory accepted leaves the chest; with no free cell for an item, only its partial stacks are topped up. The player's own grave keeps the native cell-for-cell restore
    /// (<see cref="GravePatches.TakeAll"/>), which puts gear back into its slots and items back on the hotbar.
    /// </summary>
    [HarmonyPatch(typeof(Inventory), nameof(Inventory.MoveAll))]
    public static class TakeAllRouting
    {
        [HarmonyPrefix, HarmonyPriority(Priority.Last)]
        public static bool Prefix(Inventory __instance, Inventory __0, bool __runOriginal)
        {
            // GravePatches.TakeAll's prefix runs first and sets ForeignTakeAll only for a managed inventory and a non-grave source.
            if (!__runOriginal || !GravePatches.ForeignTakeAll || ReferenceEquals(__instance, __0)) return true;
            foreach (var item in __0.GetAllItems().OrderBy(i => i.m_gridPos.y).ThenBy(i => i.m_gridPos.x).ToArray())
            {
                var copy = item.Clone();
                // Without a free cell only partial stacks can take more; the game's add would otherwise log an occupied-slot warning.
                if (!__instance.CanAddItem(copy))
                    copy.m_stack = System.Math.Min(copy.m_stack, __instance.FindFreeStackSpace(copy.m_shared.m_name, copy.m_worldLevel));
                if (copy.m_stack <= 0) continue;
                int before = __instance.GetAllItems().Sum(i => i.m_stack);
                __instance.AddItem(copy);
                int moved = System.Math.Min(item.m_stack, __instance.GetAllItems().Sum(i => i.m_stack) - before);
                if (moved > 0) __0.RemoveItem(item, moved);
            }
            return false;
        }
    }
}
