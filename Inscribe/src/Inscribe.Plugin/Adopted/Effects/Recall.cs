// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
using System.Collections;
using ExtraPotions.Inscribe.Adopted.Text;
using UnityEngine;

namespace ExtraPotions.Inscribe.Adopted.Effects
{
    /// <summary>
    /// Returning (effect <c>recall</c>), on the thrower's own client: the landed weapon goes back into the thrower's
    /// inventory. It is the very item object that left it, so its inscriptions, durability, quality and crafter are
    /// untouched and no second copy ever exists. It takes its old slot when that is free, else the first free one, and
    /// goes back into the hand while the hands are still empty (retried for a moment: the game refuses to equip while the
    /// throw animation plays). With no free slot it is not taken: the caller lets it fall where it landed.
    /// </summary>
    internal static class Recall
    {
        private const float EquipWindowSeconds = 3f;
        private static readonly WaitForSeconds Pause = new WaitForSeconds(0.1f);

        /// <summary>True when the item is in the thrower's inventory now, so nothing may be dropped.</summary>
        public static bool TryReturn(Player thrower, ItemDrop.ItemData item)
        {
            Inventory inventory = thrower.GetInventory();
            if (inventory.ContainsItem(item))
            {
                return true;
            }
            if (!PutBack(inventory, item))
            {
                thrower.Message(MessageHud.MessageType.TopLeft, Words.Localize("$ecf_msg_recall_full"));
                return false;
            }
            if (HandsFree(thrower) && !thrower.EquipItem(item))
            {
                thrower.StartCoroutine(EquipWhenFree(thrower, item));
            }
            return true;
        }

        private static bool PutBack(Inventory inventory, ItemDrop.ItemData item)
        {
            Vector2i slot = item.m_gridPos;
            bool inside = slot.x >= 0 && slot.y >= 0 && slot.x < inventory.GetWidth() && slot.y < inventory.GetHeight();
            if (inside && inventory.GetItemAt(slot.x, slot.y) == null)
            {
                return inventory.AddItem(item, slot);
            }
            return inventory.HaveEmptySlot() && inventory.AddItem(item);
        }

        private static IEnumerator EquipWhenFree(Player thrower, ItemDrop.ItemData item)
        {
            float until = Time.time + EquipWindowSeconds;
            while (Time.time < until)
            {
                yield return Pause;
                if (thrower == null || thrower.IsDead() || !thrower.GetInventory().ContainsItem(item) || !HandsFree(thrower))
                {
                    yield break;
                }
                if (thrower.EquipItem(item))
                {
                    yield break;
                }
            }
        }

        // Nothing in the right hand and at most a shield or torch in the left: the player has not taken up another
        // weapon since the throw, so equipping the returned one replaces nothing.
        private static bool HandsFree(Player player)
        {
            if (player.GetRightItem() != null)
            {
                return false;
            }
            ItemDrop.ItemData left = player.GetLeftItem();
            return left == null || left.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Shield
                || left.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Torch;
        }
    }
}
