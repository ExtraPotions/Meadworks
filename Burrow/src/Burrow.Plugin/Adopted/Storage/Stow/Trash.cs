using System;
using System.Collections.Generic;
using Burrow.Core;
using Burrow.Shared;
using ExtraPotions.Meadworks.Crucible.UI;

namespace Burrow.Stow
{
    /// <summary>
    /// Destroys stacks: the hovered one (<c>Trash Key</c>), the dragged one (a click on the trash can while
    /// dragging), a clicked one in trash mode (<see cref="TrashMode"/>, no confirmation) and every junk stack of the
    /// inventory (<c>Destroy Junk Key</c>). With <c>Confirm Trash</c>
    /// the game's yes/no popup asks first, so gamepad confirm and cancel work. With <c>Trash Uses Salvage</c> (and Kindle's salvage present) a
    /// whole stack of the player inventory that can be salvaged is salvaged instead. A stack of the open chest is
    /// destroyed in the chest when the client can change it now; in a chest another player is using (Full mode)
    /// it is taken into the inventory through the writer and what arrived is destroyed on the reply; a chest the
    /// player only views refuses with "Viewing only".
    /// </summary>
    public static class Trash
    {
        public static void TrashHovered(InventoryGui gui)
        {
            if (!StowActions.Ready(out Player player) || !HoveredItem.Find(gui, out InventoryGrid grid, out Vector2i pos))
                return;
            ItemDrop.ItemData item = HoveredItem.Item(grid, pos);
            Inventory inventory = grid.m_inventory;
            if (item == null || Refused(gui, player, inventory, item))
                return;
            int amount = item.m_stack;
            Confirm(StowWords.Format(StowWords.TrashAsk, Label(ItemNames.DisplayName(item), amount)), () => Destroy(gui, inventory, item, amount));
        }

        public static void TrashDragged(InventoryGui gui)
        {
            if (!StowActions.Ready(out Player player))
                return;
            if (gui.m_dragGo == null || gui.m_dragItem == null || gui.m_dragInventory == null)
            {
                Messages.Center(StowWords.DragHint);
                return;
            }
            ItemDrop.ItemData item = gui.m_dragItem;
            Inventory inventory = gui.m_dragInventory;
            int amount = Math.Min(gui.m_dragAmount, item.m_stack);
            if (Refused(gui, player, inventory, item))
                return;
            Confirm(StowWords.Format(StowWords.TrashAsk, Label(ItemNames.DisplayName(item), amount)), () =>
            {
                gui.SetupDragItem(null, null, 1);
                Destroy(gui, inventory, item, amount);
            });
        }

        /// <summary>A click in trash mode (<see cref="TrashMode"/>): the whole stack, with the trash's rules, no confirmation.</summary>
        public static void TrashClicked(InventoryGui gui, Inventory inventory, ItemDrop.ItemData item)
        {
            if (!StowActions.Ready(out Player player) || item == null || Refused(gui, player, inventory, item))
                return;
            Destroy(gui, inventory, item, item.m_stack);
        }

        public static void DestroyJunk(InventoryGui gui)
        {
            if (!StowActions.Ready(out Player player))
                return;
            Inventory inventory = player.GetInventory();
            List<ItemDrop.ItemData> junk = inventory.GetAllItems().FindAll(item => Favourites.IsJunk(item) && Movable.CanMove(player, inventory, item));
            if (junk.Count == 0)
            {
                Messages.Center(StowWords.NoJunk);
                return;
            }
            Confirm(StowWords.Format(StowWords.DestroyJunkAsk, junk.Count), () =>
            {
                gui.SetupDragItem(null, null, 1);
                foreach (ItemDrop.ItemData item in junk)
                    Destroy(gui, inventory, item, item.m_stack);
            });
        }

        /// <summary>The item may not move, or its chest cannot be changed (a viewed chest says "Viewing only").</summary>
        private static bool Refused(InventoryGui gui, Player player, Inventory inventory, ItemDrop.ItemData item)
        {
            Container container = ContainerOf(gui, inventory);
            if (container != null && !StowTargets.IsTarget(container))
            {
                if (SharedState.IsViewing(container))
                    Messages.Center(SharedWords.ReadOnly);
                return true;
            }
            string blocker = Movable.Blocker(player, inventory, item);
            if (blocker == null)
                return false;
            // A favourite is never destroyed or salvaged by the trash; the refusal says so in the game language.
            if (blocker == StowWords.FavouriteKept)
                Messages.Center(string.Format(Burrow.Integration.StowRowYield.Text("trash_favorite_refused"), ItemNames.DisplayName(item)));
            else if (blocker.Length > 0)
                Messages.Center(StowWords.Format(blocker, ItemNames.DisplayName(item)));
            return true;
        }

        private static string Label(string name, int amount)
        {
            return amount > 1 ? name + " x" + amount : name;
        }

        private static void Confirm(string text, Action action)
        {
            if (!StowSettings.ConfirmTrash.Value || !UnifiedPopup.IsAvailable())
            {
                action();
                return;
            }
            string header = Language.Localize(StowWords.Trash);
            Player askedPlayer = Player.m_localPlayer;
            Action accept = () =>
            {
                if (askedPlayer == null || askedPlayer != Player.m_localPlayer || !StowActions.Ready(out Player current) || current != askedPlayer) return;
                action();
            };
            var source = Burrow.Plugin.Source;
            if (source != null && source.IsActive && CrucibleUiPlugin.Active)
            {
                if (KitDialogs.Confirm(source, header, text, accept, localizeText: false) != null) return;
            }
            bool answered = false;
            UnifiedPopup.Push(new YesNoPopup(header, text, () =>
            {
                if (answered) return;
                answered = true;
                UnifiedPopup.Pop();
                accept();
            }, () => { if (answered) return; answered = true; UnifiedPopup.Pop(); }, false));
        }

        /// <summary>Removes the amount from the stack; a container is claimed first and saved after. A whole
        /// stack of the player inventory is salvaged instead when the setting is on and the item allows it.</summary>
        private static void Destroy(InventoryGui gui, Inventory inventory, ItemDrop.ItemData item, int amount)
        {
            if (!StowActions.Ready(out Player player) || inventory == null || !inventory.ContainsItem(item))
                return;
            Container container = ContainerOf(gui, inventory);
            if ((inventory != player.GetInventory() && container == null) || Refused(gui, player, inventory, item))
                return;
            if (container != null && StowTargets.IsShared(container))
            {
                TakeAndDestroy(player, container, item, amount);
                return;
            }
            if (container != null && !ContainerScan.Claim(container))
                return;
            string label = Label(ItemNames.DisplayName(item), amount);
            // Burrow: salvage belongs to Kindle; it is reached through the optional capability (Integration/SalvageLink).
            if (container == null && amount >= item.m_stack && StowSettings.TrashUsesSalvage.Value && Burrow.Integration.SalvageLink.CanSalvage(item))
            {
                if (Burrow.Integration.SalvageLink.Salvage(player, item))
                    Messages.Center(StowWords.Format(StowWords.Salvaged, label));
                return;
            }
            inventory.RemoveItem(item, amount);
            if (container != null)
                ContainerScan.Save(container);
            Messages.Center(StowWords.Format(StowWords.Trashed, label));
        }

        /// <summary>
        /// A shared chest has no destroy request: the stack is taken into the inventory and the units that arrived
        /// (the count of that kind after the reply against before, at most the amount) are destroyed there; the
        /// player never loses more than the trashed stack. A refusal is said by the writer.
        /// </summary>
        private static void TakeAndDestroy(Player player, Container container, ItemDrop.ItemData item, int amount)
        {
            Inventory inventory = player.GetInventory();
            string name = item.m_shared.m_name;
            int quality = item.m_quality;
            string display = ItemNames.DisplayName(item);
            int before = inventory.CountItems(name, quality, false);
            ChestWriter.Take(container, item, amount, ok =>
            {
                if (!ok)
                    return;
                int arrived = Math.Min(amount, inventory.CountItems(name, quality, false) - before);
                if (arrived <= 0)
                    return;
                inventory.RemoveItem(name, arrived, quality, false);
                Messages.Center(StowWords.Format(StowWords.Trashed, Label(display, arrived)));
            });
        }

        private static Container ContainerOf(InventoryGui gui, Inventory inventory)
        {
            Container open = gui != null ? gui.m_currentContainer : null;
            return open != null && open.GetInventory() == inventory ? open : null;
        }
    }
}
