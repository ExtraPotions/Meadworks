using System.Collections.Generic;
using HarmonyLib;
using Mantle.Core;
using Mantle.Slots;
using Mantle.Worn;

namespace Mantle.Tackle
{
    /// <summary>
    /// Right clicks on the inventory screen (the game's use of an item), for the player's own inventory. On a tacklebox in
    /// the grid: it goes into the Tacklebox slot (what lay there, another box, takes its cell). On the box in its slot: the
    /// box moves to a free main cell, or stays put when the inventory is full. On a bait in the box: the player fishes with that
    /// bait, through the game's own ammo equip (the rod takes the equipped bait first and the grid marks it); a right click
    /// on the marked bait unmarks it. The game itself would do nothing for either: a box is a Misc item, and bait is ammo
    /// the game never offers to equip. Only from the inventory screen; a hotbar key still offers it to what the player looks at.
    /// </summary>
    [HarmonyPatch(typeof(Humanoid), nameof(Humanoid.UseItem))]
    public static class TackleboxUse
    {
        [HarmonyPrefix]
        public static bool Prefix(Humanoid __instance, Inventory inventory, ItemDrop.ItemData item, bool fromInventoryGui)
        {
            if (!fromInventoryGui || item == null || !InventoryState.IsLocal(__instance))
                return true;
            Inventory own = inventory ?? __instance.GetInventory();
            if (!InventoryState.Manages(own) || !own.ContainsItem(item))
                return true;
            if (TackleboxCatalog.Of(item) != null)
                return !Box(__instance, own, item);
            if (!TackleRules.IsBait(item) || InventoryState.SlotAt(own, item.m_gridPos)?.Kind != SlotKind.Tackle)
                return true;
            Choose(__instance, item);
            return false;
        }

        /// <summary>True when handled: no Tacklebox slot leaves the box to the game.</summary>
        private static bool Box(Humanoid player, Inventory inventory, ItemDrop.ItemData box)
        {
            IReadOnlyList<Vector2i> cells = InventoryState.CellsOf(SlotKind.Tacklebox);
            if (cells.Count == 0)
                return false;
            if (box.m_gridPos == cells[0])
            {
                if (GearOut.Away(player, box, SlotKind.Tacklebox))
                    TackleState.Close();
                else
                    Messages.Center(Words.NoRoomOff);
                return true;
            }
            ItemDrop.ItemData there = inventory.GetItemAt(cells[0].x, cells[0].y);
            if (there != null)
                there.m_gridPos = box.m_gridPos;
            box.m_gridPos = cells[0];
            inventory.Changed();
            return true;
        }

        /// <summary>
        /// The game counts any ammo of the equipped one's name as equipped and would refuse the new stack, so the old one
        /// comes off first (another stack of the same bait with other stars, say).
        /// </summary>
        private static void Choose(Humanoid player, ItemDrop.ItemData bait)
        {
            if (bait.m_equipped)
            {
                player.UnequipItem(bait);
                return;
            }
            ItemDrop.ItemData current = player.GetAmmoItem();
            if (current != null)
                player.UnequipItem(current, triggerEquipEffects: false);
            player.EquipItem(bait);
        }
    }
}
