using System;
using System.Collections.Generic;
using Burrow.Core;

namespace Burrow.Stow
{
    /// <summary>
    /// Tops up every stack of the inventory that is not full from the open container and the nearby ones. Only
    /// items already in the inventory, favourite items included; nothing new arrives. The room per kind (name,
    /// quality, world level: the game's stacking rule) is counted once and spent as takes are sent, so shared
    /// chests, whose answers come later, are never asked for more than fits. Every take goes through the writer,
    /// which merges what arrives into the partial stacks the way the game's add does.
    /// </summary>
    public static class TopUp
    {
        public static void Run()
        {
            if (!StowActions.Ready(out Player player))
                return;
            Dictionary<string, int> room = Room(player.GetInventory());
            int units = 0;
            bool waiting = false;
            foreach (Container container in StowTargets.Nearby(player))
            {
                if (Burrow.Integration.IntegrationLinks.IsFeedContainer(container))
                    continue;
                ChestBatch batch = new ChestBatch(container, StowWords.ToppedUpFrom);
                Fill(player, container, room, batch);
                batch.Finish();
                if (batch.Waiting)
                    waiting = true;
                else
                    units += batch.Moved;
            }
            if (units > 0 || !waiting)
                Messages.Center(units > 0 ? StowWords.Format(StowWords.ToppedUp, units) : StowWords.Nothing);
        }

        /// <summary>The free units per kind over the inventory's partial stacks.</summary>
        private static Dictionary<string, int> Room(Inventory inventory)
        {
            Dictionary<string, int> room = new Dictionary<string, int>();
            Player player = Player.m_localPlayer;
            if (player == null || inventory != player.GetInventory() || !Plugin.IsReady
                || Burrow.Resources.NativeChestIdentity.World == 0) return room;
            foreach (ItemDrop.ItemData item in inventory.GetAllItems())
            {
                if (PackPanelGrid.InSlot(player, inventory, item) || !Partial(item))
                    continue;
                string key = Key(item);
                room.TryGetValue(key, out int space);
                room[key] = space + item.m_shared.m_maxStackSize - item.m_stack;
            }
            return room;
        }

        private static bool Partial(ItemDrop.ItemData item)
        {
            return item.m_shared.m_maxStackSize > 1 && item.m_stack < item.m_shared.m_maxStackSize && !item.m_shared.m_questItem;
        }

        private static string Key(ItemDrop.ItemData item) => item.m_shared.m_name + "|" + item.m_quality + "|" + item.m_worldLevel;

        /// <summary>Sends one take per chest stack of a wanted kind; a chest changed at once is claimed first so
        /// the stacks picked are the ones the claim loaded.</summary>
        private static void Fill(Player player, Container container, Dictionary<string, int> room, ChestBatch batch)
        {
            if (!Wants(container.GetInventory(), room))
                return;
            if (!batch.Shared && !ContainerScan.Claim(container))
                return;
            foreach (ItemDrop.ItemData item in new List<ItemDrop.ItemData>(container.GetInventory().GetAllItems()))
            {
                string key = Key(item);
                if (!room.TryGetValue(key, out int space) || space <= 0)
                    continue;
                Vector2i? destination = Destination(player, key);
                if (!destination.HasValue)
                    continue;
                int take = Math.Min(space, item.m_stack);
                room[key] = space - take;
                batch.Take(item, take, take, destination.Value);
            }
        }

        private static Vector2i? Destination(Player player, string key)
        {
            Inventory inventory = player.GetInventory();
            foreach (ItemDrop.ItemData item in inventory.GetAllItems())
                if (!PackPanelGrid.InSlot(player, inventory, item) && Partial(item) && Key(item) == key)
                    return item.m_gridPos;
            return null;
        }

        private static bool Wants(Inventory source, Dictionary<string, int> room)
        {
            foreach (ItemDrop.ItemData item in source.GetAllItems())
            {
                if (room.TryGetValue(Key(item), out int space) && space > 0)
                    return true;
            }
            return false;
        }
    }
}
