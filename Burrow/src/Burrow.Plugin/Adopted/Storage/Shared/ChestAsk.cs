using System;
using System.Collections.Generic;
using Burrow.Core;

namespace Burrow.Shared
{
    /// <summary>
    /// The request path of <see cref="ChestWriter"/>: builds each request of SPEC 9.2, checks what the requester
    /// must check first (a take fits the inventory, a stack-all has candidates), and reports the durable requester's
    /// settled answer. The chest itself is never touched here; its copy on this client refreshes
    /// from the ZDO once the owner saved.
    /// </summary>
    internal static class ChestAsk
    {
        public static void Take(Container container, ItemDrop.ItemData item, int amount, Player player, Vector2i slot, Action<bool> done)
        {
            Vector2i from = item.m_gridPos;
            if (ChestRequester.IsPending(container, from))
            {
                done(false);
                return;
            }
            int fit = new InventoryFit(player.GetInventory()).Reserve(item, amount);
            if (fit <= 0)
            {
                Messages.Center(SharedWords.NoFit);
                done(false);
                return;
            }
            string name = item.m_shared.m_name;
            int expected = item.m_stack;
            ChestRequester.Send(container, ChestRequests.TakeRpc, from,
                pkg => { pkg.Write(from); pkg.Write(name); pkg.Write(expected); pkg.Write(fit); },
                payload => done(true),
                reason => done(false), slot);
        }

        public static void Put(Container container, ItemDrop.ItemData item, int amount, Player player, Vector2i slot, Action<bool> done)
        {
            ItemDrop.ItemData copy = ItemPacket.Copy(item, amount);
            if (copy.m_dropPrefab == null)
            {
                Messages.Center(SharedWords.Unavailable);
                done(false);
                return;
            }
            bool hasSlot = slot.x >= 0;
            ChestRequester.Send(container, ChestRequests.PutRpc, slot,
                pkg => { ItemPacket.Write(pkg, copy); pkg.Write(hasSlot); pkg.Write(slot); },
                payload =>
                {
                    done(payload.ReadInt() > 0);
                },
                reason => done(false));
        }

        public static void Move(Container container, Vector2i from, Vector2i to, int amount, Action<bool> done)
        {
            ItemDrop.ItemData item = container.GetInventory().GetItemAt(from.x, from.y);
            if (item == null || ChestRequester.IsPending(container, from))
            {
                done(false);
                return;
            }
            string name = item.m_shared.m_name;
            ChestRequester.Send(container, ChestRequests.MoveRpc, from,
                pkg => { pkg.Write(from); pkg.Write(to); pkg.Write(amount); pkg.Write(name); },
                payload => done(true),
                reason => done(false));
        }

        public static void TakeAll(Container container, Player player, Action<bool> done)
        {
            List<KeyValuePair<ItemDrop.ItemData, int>> entries = FitEntries(container.GetInventory(), player.GetInventory());
            if (entries.Count == 0)
            {
                Messages.Center(SharedWords.NoFit);
                done(false);
                return;
            }
            ChestRequester.Send(container, ChestRequests.TakeAllRpc, ChestOps.NoSlot,
                pkg => WriteEntries(pkg, entries),
                payload => done(payload.ReadInt() > 0),
                reason => done(false));
        }

        /// <summary>The chest stacks, in the game's order, with the units of each that the inventory can take.</summary>
        private static List<KeyValuePair<ItemDrop.ItemData, int>> FitEntries(Inventory chest, Inventory inventory)
        {
            InventoryFit fit = new InventoryFit(inventory);
            List<KeyValuePair<ItemDrop.ItemData, int>> entries = new List<KeyValuePair<ItemDrop.ItemData, int>>();
            foreach (ItemDrop.ItemData item in chest.GetAllItems())
            {
                int units = fit.Reserve(item, item.m_stack);
                if (units > 0)
                    entries.Add(new KeyValuePair<ItemDrop.ItemData, int>(item, units));
            }
            return entries;
        }

        private static void WriteEntries(ZPackage pkg, List<KeyValuePair<ItemDrop.ItemData, int>> entries)
        {
            pkg.Write(entries.Count);
            foreach (KeyValuePair<ItemDrop.ItemData, int> entry in entries)
            {
                pkg.Write(entry.Key.m_gridPos);
                pkg.Write(entry.Key.m_shared.m_name);
                pkg.Write(entry.Value);
            }
        }

        public static void StackAll(Container container, Player player, Action<bool> done)
        {
            List<ItemDrop.ItemData> candidates = StackCandidates(container.GetInventory(), player);
            if (candidates.Count == 0)
            {
                Messages.Center("$msg_stackall_none");
                done(false);
                return;
            }
            ChestRequester.Send(container, ChestRequests.StackAllRpc, ChestOps.NoSlot,
                pkg => WriteCandidates(pkg, candidates),
                payload => done(ReceiveAccepted(payload)),
                reason => done(false));
        }

        /// <summary>
        /// The game's stack-all candidates: unequipped inventory stacks whose kind the chest already holds, from the grid
        /// only (PackPanel's slots keep theirs, as with the game's own Place stacks: <see cref="PackPanelGrid"/>).
        /// </summary>
        private static List<ItemDrop.ItemData> StackCandidates(Inventory chest, Player player)
        {
            List<ItemDrop.ItemData> candidates = new List<ItemDrop.ItemData>();
            Inventory own = player.GetInventory();
            foreach (ItemDrop.ItemData item in own.GetAllItems())
            {
                if (item.m_equipped || player.IsItemEquiped(item) || item.m_dropPrefab == null || PackPanelGrid.InSlot(player, own, item))
                    continue;
                if (chest.ContainsItemByName(item.m_shared.m_name))
                    candidates.Add(item);
            }
            return candidates;
        }

        private static void WriteCandidates(ZPackage pkg, List<ItemDrop.ItemData> candidates)
        {
            pkg.Write(candidates.Count);
            for (int i = 0; i < candidates.Count; i++)
            {
                pkg.Write(i);
                ItemPacket.Write(pkg, ItemPacket.Copy(candidates[i], candidates[i].m_stack));
            }
        }

        private static bool ReceiveAccepted(ZPackage payload)
        {
            int count = payload.ReadInt();
            int total = 0;
            for (int i = 0; i < count; i++)
            {
                payload.ReadInt();
                int accepted = payload.ReadInt();
                total += accepted;
            }
            Messages.Center(total > 0 ? "$msg_stackall " + total : "$msg_stackall_none");
            return total > 0;
        }
    }
}
