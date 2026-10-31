using HarmonyLib;
using Burrow.Shared;
using UnityEngine;

namespace Burrow.Stacks
{
    /// <summary>
    /// "Merge Into Chests": a stack dragged from the player's inventory onto the open container's grid first tops
    /// up the container's partial stacks of the same item (the game's stacking identity, IsSameType), then the
    /// game's own drop puts the remainder on the target slot. The move-click path (Inventory.MoveItemToThis with
    /// AddItem) already fills partial stacks in the game, so only the drag and drop path is patched:
    /// InventoryGrid.DropItem(Inventory fromInventory, ItemDrop.ItemData item, int amount, Vector2i pos).
    /// A chest the local client does not own (viewed, or shared through requests) is left to the Shared module,
    /// whose own prefix on this method refuses or routes the drop; nothing here touches such a chest.
    /// </summary>
    public static class MergeIntoChests
    {
        /// <summary>The open container when the inventory is its inventory, else null.</summary>
        public static Container OpenContainer(Inventory inventory)
        {
            InventoryGui gui = InventoryGui.instance;
            Container open = gui != null ? gui.m_currentContainer : null;
            return open != null && inventory != null && open.GetInventory() == inventory ? open : null;
        }

        /// <summary>Whether the inventory is the container the local player has open.</summary>
        public static bool IsOpenContainer(Inventory inventory) => OpenContainer(inventory) != null;

        /// <summary>
        /// Moves up to <paramref name="amount"/> of the item into the target's partial stacks of the same kind,
        /// leaving the stack on the target slot to the game. Returns how many were moved.
        /// </summary>
        public static int FillPartialStacks(Inventory target, ItemDrop.ItemData item, int amount, Vector2i pos)
        {
            int remaining = Mathf.Min(amount, item.m_stack);
            int moved = 0;
            foreach (ItemDrop.ItemData stack in target.GetAllItemsInGridOrder())
            {
                if (remaining <= 0)
                    break;
                if (stack == item || stack.m_gridPos == pos || !stack.IsSameType(item))
                    continue;
                int free = stack.m_shared.m_maxStackSize - stack.m_stack;
                if (free <= 0)
                    continue;
                int part = Mathf.Min(free, remaining);
                stack.m_stack += part;
                if (item.m_cheated && !PlayerProfile.s_bypassCheatChecks)
                    stack.m_cheated = true;
                item.m_stack -= part;
                remaining -= part;
                moved += part;
            }
            return moved;
        }

        [HarmonyPatch(typeof(InventoryGrid), nameof(InventoryGrid.DropItem))]
        private static class DropPatch
        {
            [HarmonyPrefix]
            private static bool Prefix(InventoryGrid __instance, Inventory fromInventory, ItemDrop.ItemData item, ref int amount, Vector2i pos, ref bool __result)
            {
                Inventory target = __instance.GetInventory();
                if (!Applies(target, fromInventory, item))
                    return true;
                int moved = FillPartialStacks(target, item, amount, pos);
                if (moved == 0)
                    return true;
                amount -= moved;
                target.Changed();
                if (amount > 0)
                {
                    fromInventory.Changed();
                    return true;
                }
                if (item.m_stack <= 0 && fromInventory.ContainsItem(item))
                    fromInventory.RemoveItem(item);
                else
                    fromInventory.Changed();
                __result = true;
                return false;
            }

            /// <summary>The setting is on, a stacking item is dropped from another inventory into the open container, and that container is the local client's own to change.</summary>
            private static bool Applies(Inventory target, Inventory fromInventory, ItemDrop.ItemData item)
            {
                if (!StacksSettings.Enabled.Value || !StacksSettings.MergeIntoChests.Value)
                    return false;
                Container open = OpenContainer(target);
                if (item == null || fromInventory == null || !fromInventory.ContainsItem(item)
                    || fromInventory == target || open == null || item.m_shared.m_maxStackSize <= 1)
                    return false;
                return !SharedState.IsViewing(open) && !ChestWriter.IsShared(open);
            }
        }
    }
}
