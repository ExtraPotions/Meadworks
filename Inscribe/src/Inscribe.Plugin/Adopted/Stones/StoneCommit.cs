// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.10.0 at d3b57606cb24775d61b7bda244b22eb89e7e7717.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries; gem recovery.
using ExtraPotions.Inscribe.Adopted.Affixes;
using ExtraPotions.Inscribe.Adopted.Core;
using ExtraPotions.Inscribe.Adopted.Text;
using ExtraPotions.Inscribe.Runtime;
using UnityEngine;

namespace ExtraPotions.Inscribe.Adopted.Stones
{
    /// <summary>
    /// Pipeline step 14 (applying-stones.md section 3), in one frame on the owning client: take the whole cost, and only
    /// then write the dry-run state. A short payment writes nothing. A refused write puts the cost back. Keep the rest of
    /// the stack on the cursor, then the feedback. An equipped target needs nothing more here: the write raises
    /// <see cref="ItemStateCache.Written"/>, which the effects area listens to for its rebuild.
    /// <para>
    /// Multiplayer: nothing is sent anywhere. The new state lives in the item's <c>m_customData</c>, which the game
    /// saves with the player and serializes into chests, the ground, tombstones and trades, so other players see it
    /// when the item next leaves this inventory. No RPC is needed (Phase 3's transient visuals will add one).
    /// </para>
    /// </summary>
    internal static class StoneCommit
    {
        public static void Commit(StoneJob job, StoneResult result)
        {
            if (result.State == null)
            {
                return;   // a refusal or a socket still to pick: nothing to write
            }
            if (!Pay(job))
            {
                return;
            }
            bool written;
            using (ItemChanges.Because(ItemChanges.Rune(job.Def?.Id)))
            {
                written = ItemState.Write(job.Target, result.State!);
            }
            if (!written)
            {
                Log.Error($"rune '{job.Def?.Id}' passed every check but the item state write was refused; the cost was put back");
                Undo(job);
                return;
            }
            job.Supply?.NoteUsed(job);
            if (result.ReturnedGem != null)
            {
                Runtime.GemRecoveryRuntime.Give(job.Player, result.ReturnedGem);   // after the write: never a copy without the swap
            }
            KeepCarrying(job);
            StoneFeedback.Success(job.Player, result);
        }

        // The whole cost leaves before the write. A carried stack is paid from the inventory that holds it (the player's,
        // or the open chest this client owns). At the Rune Table the supply pays its runes and its essence.
        private static bool Pay(StoneJob job)
        {
            if (job.Supply != null)
            {
                return job.Supply.Pay(job);
            }
            if (job.Cost <= 0)
            {
                return true;
            }
            Inventory? source = job.StoneSource;
            if (source == null)
            {
                return false;
            }
            int removed = SafeInventory.RemoveFrom(source, job.Stone, job.Cost);
            if (removed == job.Cost)
            {
                return true;
            }
            Restore(source, job.Stone, removed, job.Player);
            return false;
        }

        private static void Undo(StoneJob job)
        {
            if (job.Supply != null)
            {
                job.Supply.Refund(job);
                return;
            }
            if (job.Cost > 0 && job.StoneSource != null)
            {
                Restore(job.StoneSource, job.Stone, job.Cost, job.Player);
            }
        }

        // A short removal, or a write that was refused after a full removal, puts those stones back on the same stack.
        // What does not fit is dropped at the player's feet.
        private static void Restore(Inventory inventory, ItemDrop.ItemData stone, int count, Player player)
        {
            if (count <= 0)
            {
                return;
            }
            if (inventory.ContainsItem(stone))
            {
                stone.m_stack += count;
                return;
            }
            stone.m_stack = count;
            if (!inventory.AddItem(stone))
            {
                SafeInventory.AddOrDrop(inventory, stone, SafeInventory.AtFeet(player));
            }
        }

        // The stack stays on the cursor for the next click, its carried amount clamped to what is left; used up, the drag clears.
        private static void KeepCarrying(StoneJob job)
        {
            InventoryGui gui = InventoryGui.instance;
            if (gui == null || job.Supply != null || gui.m_dragItem != job.Stone)
            {
                return;
            }
            if (job.Stone.m_stack <= 0 || !job.StoneSource!.ContainsItem(job.Stone))
            {
                // As vanilla does when a split drag ends: without it the grids' drop-focus overlay stays on.
                gui.SetupDragItem(null, null, 1);
                gui.IsSplitDropping = false;
                return;
            }
            gui.m_dragAmount = Mathf.Clamp(gui.m_dragAmount, 1, job.Stone.m_stack);
        }
    }

    /// <summary>
    /// What the player sees (applying-stones.md section 6): the message in the center of the screen and the vanilla
    /// item-move sound on success. Local player only.
    /// </summary>
    internal static class StoneFeedback
    {
        public static void Show(Player player, StoneMessage message)
        {
            player.Message(MessageHud.MessageType.Center, Words.Localize(message.Key, message.Words));
        }

        public static void Success(Player player, StoneResult result)
        {
            if (result.Feedback != null)
            {
                Show(player, result.Feedback);
            }
            InventoryGui gui = InventoryGui.instance;
            if (gui != null)
            {
                gui.m_moveItemEffects.Create(gui.transform.position, Quaternion.identity);
            }
        }
    }
}
