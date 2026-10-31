using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Burrow.Shared
{
    /// <summary>
    /// The inventory operations of a shared chest, written once and used by both sides: the owner applies them to
    /// its live chest when a request is granted, and the synchronous path of <see cref="ChestWriter"/> applies
    /// them locally when the client may change the chest at once. Everything goes through the game's own
    /// inventory methods (<c>AddItem</c>, <c>RemoveItem</c>, <c>MoveItemToThis</c>), so counts never change and
    /// every change fires the inventory's change callback.
    /// </summary>
    public static class ChestOps
    {
        public static System.Func<Inventory, bool> NativeItemRouting;
        public static readonly Vector2i NoSlot = new Vector2i(-1, -1);

        /// <summary>Removes <paramref name="amount"/> units of the stack and returns a copy holding exactly them.</summary>
        public static ItemDrop.ItemData Take(Inventory inventory, ItemDrop.ItemData item, int amount)
        {
            if (item == null || amount <= 0 || !inventory.ContainsItem(item))
                return null;
            ItemDrop.ItemData taken = ItemPacket.Copy(item, amount);
            int before = inventory.ContainsItem(item) ? item.m_stack : 0;
            try { inventory.RemoveItem(item, taken.m_stack); }
            catch (System.Exception) { }
            int after = inventory.ContainsItem(item) ? item.m_stack : 0;
            int removed = before - after;
            if (removed <= 0 || removed > before)
                return null;
            taken.m_stack = removed;
            return taken;
        }

        /// <summary>
        /// Moves up to <paramref name="amount"/> units of a stack into another inventory, into the slot when it
        /// is free or holds the same kind, else anywhere; the source loses exactly what arrived. Returns the units moved.
        /// </summary>
        public static int Transfer(Inventory from, ItemDrop.ItemData item, int amount, Inventory to, Vector2i slot)
        {
            amount = Mathf.Min(amount, item.m_stack);
            if (amount <= 0 || !from.ContainsItem(item))
                return 0;
            ItemDrop.ItemData copy = ItemPacket.Copy(item, amount);
            return InventoryTransferDelta.Move(from, item, amount, to, copy, moving => Add(to, moving, slot), "chest transfer", out _);
        }

        /// <summary>
        /// Adds an item into an inventory: into the slot when it is free or holds the same kind with room, the
        /// rest anywhere. Returns the units added; the item's stack is reduced to what did not fit.
        /// </summary>
        public static int Add(Inventory inventory, ItemDrop.ItemData item, Vector2i slot)
        {
            int wanted = item.m_stack;
            if (slot.x < 0 && NativeItemRouting?.Invoke(inventory) == true && Player.m_localPlayer != null && inventory == Player.m_localPlayer.GetInventory())
            {
                if (!AutomaticPlayerCell(inventory, new Vector2i(0, 0))) return 0;
                int before = inventory.GetAllItems().Sum(i => i.m_stack);
                inventory.AddItem(item);
                return System.Math.Max(0, System.Math.Min(wanted, inventory.GetAllItems().Sum(i => i.m_stack) - before));
            }
            if (slot.x >= 0 && SlotTakes(inventory, item, slot))
                inventory.AddItem(item, item.m_stack, slot.x, slot.y);
            if (item.m_stack <= 0)
                return wanted;
            foreach (ItemDrop.ItemData at in new List<ItemDrop.ItemData>(inventory.GetAllItems()))
            {
                if (item.m_stack <= 0)
                    break;
                if (SlotTakes(inventory, item, at.m_gridPos))
                    inventory.AddItem(item, item.m_stack, at.m_gridPos.x, at.m_gridPos.y);
            }
            bool playerGrid = Player.m_localPlayer != null && inventory == Player.m_localPlayer.GetInventory();
            for (int row = 0; row < inventory.GetHeight() && item.m_stack > 0; row++)
            {
                int y = playerGrid ? (row + 1) % inventory.GetHeight() : row;
                for (int x = 0; x < inventory.GetWidth() && item.m_stack > 0; x++)
                    if (inventory.GetItemAt(x, y) == null && SlotTakes(inventory, item, new Vector2i(x, y)))
                        inventory.AddItem(item, item.m_stack, x, y);
            }
            return wanted - item.m_stack;
        }

        /// <summary>
        /// The player receives an item the chest's owner handed over: into the slot or anywhere; what no longer
        /// fits is dropped in front of the player, the way the game drops what a player cannot carry.
        /// </summary>
        public static void GiveToPlayer(Player player, ItemDrop.ItemData item, Vector2i slot)
        {
            int wanted = item.m_stack;
            Inventory inventory = player.GetInventory();
            int before = StackCount(inventory, item);
            int added;
            try { added = Add(inventory, item, slot); }
            catch (System.Exception)
            {
                added = System.Math.Max(0, System.Math.Min(wanted, StackCount(inventory, item) - before));
            }
            if (added >= wanted)
                return;
            int rest = wanted - added;
            Transform at = player.transform;
            ItemDrop drop;
            try
            {
                drop = ItemDrop.DropItem(item, rest, at.position + at.forward + at.up, at.rotation);
                if (drop == null)
                    drop = ItemDrop.DropItem(item, rest, at.position + at.forward + at.up, at.rotation);
            }
            catch (System.Exception error)
            {
                Plugin.Log.LogWarning("Burrow: player grant drop may already exist: " + error.GetType().Name);
                return;
            }
            if (drop == null)
            {
                Plugin.Log.LogWarning($"Burrow: {rest} x {item.m_shared.m_name} could not be dropped for the player");
                return;
            }
            try { drop.OnPlayerDrop(); }
            catch (System.Exception error) { Plugin.Log.LogWarning("Burrow: player grant drop notice: " + error.GetType().Name); }
            player.Message(MessageHud.MessageType.TopLeft, "$msg_dropped " + item.m_shared.m_name, rest, item.GetIcon());
            Plugin.Log.LogDebug($"Burrow: {rest} x {item.m_shared.m_name} did not fit the inventory any more and were dropped");
        }

        private static int StackCount(Inventory inventory, ItemDrop.ItemData sample) => inventory.GetAllItems()
            .Where(item => item.m_shared != null && sample.m_shared != null && item.m_shared.m_name == sample.m_shared.m_name
                && item.m_quality == sample.m_quality && item.m_worldLevel == sample.m_worldLevel)
            .Sum(item => item.m_stack);

        /// <summary>
        /// A sent copy goes into the chest: at the slot when it is free or holds the same kind with room (a
        /// request never swaps stacks), without a slot anywhere. Returns the units accepted; the copy keeps the rest.
        /// </summary>
        public static int Put(Inventory chest, ItemDrop.ItemData copy, Vector2i slot)
        {
            int wanted = copy.m_stack;
            if (slot.x >= 0)
            {
                if (!SlotTakes(chest, copy, slot))
                    return 0;
                chest.AddItem(copy, copy.m_stack, slot.x, slot.y);
                return wanted - copy.m_stack;
            }
            return Add(chest, copy, NoSlot);
        }

        /// <summary>The slot lies in the grid and is empty or holds a stack of the same kind with room.</summary>
        public static bool SlotTakes(Inventory inventory, ItemDrop.ItemData item, Vector2i slot)
        {
            if (slot.x < 0 || slot.y < 0 || slot.x >= inventory.GetWidth() || slot.y >= inventory.GetHeight())
                return false;
            if (!AutomaticPlayerCell(inventory, slot)) return false;
            ItemDrop.ItemData at = inventory.GetItemAt(slot.x, slot.y);
            return at == null || (ItemCompatibility.Same(at, item) && at.m_stack < at.m_shared.m_maxStackSize);
        }

        /// <summary>Automatic allocation may never merge into or occupy a provider's protected player cells.</summary>
        private static bool AutomaticPlayerCell(Inventory inventory, Vector2i slot)
        {
            Player player = Player.m_localPlayer;
            if (player == null || inventory != player.GetInventory()) return true;
            if (!Plugin.IsReady) return false;
            if (!Burrow.Core.PackPanelLink.LaysOutInventory) return true;
            if (!Burrow.Core.PackPanelGrid.TryRead(player, out int width, out int rows, out int blocked)) return false;
            return slot.x < width && slot.y < rows && (slot.y != rows - 1 || slot.x < width - blocked);
        }

        /// <summary>
        /// Moves within one inventory like the game's drag and drop: onto an empty slot, onto the same kind
        /// (merge), or a swap when a whole stack lands on a different item. With <paramref name="expectedName"/>
        /// the stack must still be the one the requester saw.
        /// </summary>
        public static bool Move(Inventory inventory, Vector2i from, Vector2i to, int amount, string expectedName)
        {
            ItemDrop.ItemData item = inventory.GetItemAt(from.x, from.y);
            if (item == null || (expectedName != null && item.m_shared.m_name != expectedName))
                return false;
            if (from == to)
                return true;
            if (to.x < 0 || to.y < 0 || to.x >= inventory.GetWidth() || to.y >= inventory.GetHeight())
                return false;
            amount = Mathf.Clamp(amount, 1, item.m_stack);
            ItemDrop.ItemData target = inventory.GetItemAt(to.x, to.y);
            if (target == null || ItemCompatibility.Same(target, item))
                return inventory.MoveItemToThis(inventory, item, amount, to.x, to.y);
            if (amount != item.m_stack)
                return false;
            Vector2i position = item.m_gridPos;
            item.m_gridPos = target.m_gridPos;
            target.m_gridPos = position;
            inventory.Changed();
            return true;
        }

        /// <summary>Removes what the owner accepted from the requester's own stack; a stack that changed meanwhile is logged.</summary>
        public static void RemoveFromPlayer(Inventory inventory, ItemDrop.ItemData item, int accepted)
        {
            if (accepted <= 0 || item == null)
                return;
            if (!inventory.ContainsItem(item))
            {
                Plugin.Log.LogWarning($"Burrow: the chest accepted {accepted} x {item.m_shared.m_name} but the stack left the inventory meanwhile");
                return;
            }
            if (item.m_stack < accepted)
            {
                Plugin.Log.LogWarning($"Burrow: the chest accepted {accepted} x {item.m_shared.m_name} but only {item.m_stack} are left in the inventory");
                accepted = item.m_stack;
            }
            inventory.RemoveItem(item, accepted);
        }
    }

}
