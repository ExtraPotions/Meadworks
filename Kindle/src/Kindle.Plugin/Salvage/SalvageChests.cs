// Kindle salvage from nearby chests, through Burrow's optional chest capabilities.
// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Linq;
using ExtraPotions.Kindle.Salvage;
using ExtraPotions.Meadworks.Crucible;
using UnityEngine;
using CruciblePlugin = ExtraPotions.Meadworks.Crucible.Plugin;

namespace ExtraPotions.Kindle.Runtime.Salvage
{
    /// <summary>
    /// The Salvage tab's chest side. Burrow publishes <c>meadworks.storage.chest_items</c> 1.0 (the gear in the chests its Reach
    /// crafting may use: same range, wards, privacy, in-use and allow/deny rules) and <c>meadworks.storage.chest_take</c> 1.0
    /// (takes one listed item through Burrow's durable chest hold and calls back once the removal is decided). Kindle has no
    /// reference to Burrow: without it, or with Salvage / Nearby Chests off, nothing is listed and Kindle behaves as before.
    /// One chest salvage runs at a time; a handover from the chest's owner is waited for up to five seconds.
    /// </summary>
    internal static class SalvageChests
    {
        const string ItemsId = "meadworks.storage.chest_items", TakeId = "meadworks.storage.chest_take";
        static readonly System.Version ContractVersion = new System.Version(1, 0);
        static readonly CapabilityLink<Func<IReadOnlyList<Tuple<object, object>>>> items =
            new CapabilityLink<Func<IReadOnlyList<Tuple<object, object>>>>(ItemsId, ContractVersion);
        static readonly CapabilityLink<Func<object, object, Action, int>> take =
            new CapabilityLink<Func<object, object, Action, int>>(TakeId, ContractVersion);
        static CapabilityRegistry? Registry => CruciblePlugin.IsReady ? CruciblePlugin.Services.Capabilities : null;

        sealed class Pending
        {
            internal Container Chest = null!; internal ItemDrop.ItemData Item = null!; internal Player Player = null!;
            internal List<SalvageReturn> Returns = null!; internal ChestTakeRetry Retry = null!; internal Action Done = null!;
        }
        static Pending? pending;

        /// <summary>Chest salvage is on and Burrow offers both capabilities.</summary>
        internal static bool On
        {
            get
            {
                if (!SalvageSettings.NearbyChests.Value) return false;
                try { var registry = Registry; return items.TryGet(registry, out _) && take.TryGet(registry, out _); }
                catch (Exception) { return false; }
            }
        }

        /// <summary>A chest salvage is waiting for Burrow.</summary>
        internal static bool Busy => pending != null;

        /// <summary>The (chest, item) pairs Burrow lists now; empty without Burrow, when off, or when Burrow fails.</summary>
        internal static List<KeyValuePair<Container, ItemDrop.ItemData>> Items()
        {
            var found = new List<KeyValuePair<Container, ItemDrop.ItemData>>();
            if (!SalvageSettings.NearbyChests.Value) return found;
            try
            {
                if (!items.TryGet(Registry, out var read) || read == null || !take.TryGet(Registry, out _)) return found;
                var listed = read();
                if (listed == null) return found;
                foreach (var pair in listed)
                    if (pair != null && pair.Item1 is Container chest && chest && pair.Item2 is ItemDrop.ItemData item && chest.GetInventory() != null)
                        found.Add(new KeyValuePair<Container, ItemDrop.ItemData>(chest, item));
            }
            catch (Exception) { found.Clear(); }
            return found;
        }

        /// <summary>A value that changes when the listed chest items change (instances, stacks or slots).</summary>
        internal static int Stamp()
        {
            int stamp = 17;
            foreach (var pair in Items())
                stamp = unchecked(stamp * 31 + System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(pair.Value) * 7 + pair.Value.m_stack * 3
                    + pair.Value.m_gridPos.x * 101 + pair.Value.m_gridPos.y);
            return stamp;
        }

        /// <summary>Asks with the Salvage tab's yes/no question, then salvages the chest item on yes.</summary>
        internal static void Confirm(Player player, Container chest, ItemDrop.ItemData item, Action afterwards)
        {
            if (!chest) { Messages.Center(SalvageWords.ChestBusy); return; }
            SalvageActions.Ask(player, item, chest.GetInventory(), () => Salvage(player, chest, item, afterwards));
        }

        /// <summary>
        /// Salvages a whole chest stack with the same blockers and returns as an inventory stack, except that there is no
        /// room check: what does not fit drops at the player's feet.
        /// </summary>
        internal static void Salvage(Player player, Container chest, ItemDrop.ItemData item, Action afterwards)
        {
            string? blocker = chest ? SalvageRules.Blocker(player, item, chest.GetInventory()) : SalvageWords.ChestBusy;
            if (blocker == null && !On) blocker = SalvageWords.Off;
            if (blocker != null) { Messages.Center(blocker); return; }
            if (pending != null) { Messages.Center(SalvageWords.ChestWait); return; }
            List<SalvageReturn> returns = SalvageActions.Returns(item);
            if (returns.Count == 0) { Messages.Center(SalvageWords.Nothing); return; }
            // Each chance is rolled once, now; a retried take delivers the same materials.
            List<SalvageReturn> rolled = SalvageRuneLink.Roll(returns);
            int lostGems = SalvageGemLink.Lost(item, rolled);
            string summary = SalvageActions.Summary(item, rolled);
            Start(player, chest, item, rolled, () => { SalvageGemLink.ReportLost(lostGems); Messages.TopLeft(summary); afterwards?.Invoke(); });
        }

        /// <summary>
        /// Starts salvaging a chest item: Burrow takes the item, and the materials are delivered only after the removal is
        /// decided. <paramref name="done"/> runs after a delivery.
        /// </summary>
        internal static void Start(Player player, Container chest, ItemDrop.ItemData item, List<SalvageReturn> rolled, Action done)
        {
            if (pending != null) { Messages.Center(SalvageWords.ChestWait); return; }
            pending = new Pending { Chest = chest, Item = item, Player = player, Returns = rolled, Retry = new ChestTakeRetry(Time.realtimeSinceStartupAsDouble), Done = done };
            if (!Attempt()) Messages.Center(SalvageWords.ChestWait);
        }

        /// <summary>Asks Burrow again for a waiting salvage. Main thread, every frame.</summary>
        internal static void Tick()
        {
            if (pending == null) return;
            if (!KindleSalvage.Ready || pending.Player != Player.m_localPlayer || !pending.Chest) { pending = null; Messages.Center(SalvageWords.ChestBusy); return; }
            Attempt();
        }

        internal static void Shutdown() => pending = null;

        /// <summary>One request to Burrow. True when the salvage finished either way.</summary>
        static bool Attempt()
        {
            var job = pending!;
            bool delivered = false;
            int code;
            try
            {
                code = take.TryGet(Registry, out var run) && run != null
                    ? run(job.Chest, job.Item, () => { delivered = true; Deliver(job.Player, job.Returns); })
                    : ChestTakeCodes.Unavailable;
            }
            catch (Exception) { code = ChestTakeCodes.Unavailable; }
            switch (job.Retry.Next(code, delivered, Time.realtimeSinceStartupAsDouble))
            {
                case ChestTakeStep.Retry: return false;
                case ChestTakeStep.Done: pending = null; Guard(job.Done); return true;
                case ChestTakeStep.Changed: pending = null; Messages.Center(SalvageWords.ChestChanged); KindleSalvage.Rebuild(); return true;
                default: pending = null; Messages.Center(SalvageWords.ChestBusy); KindleSalvage.Rebuild(); return true;
            }
        }

        /// <summary>The materials go into the player's inventory with the game's AddItem; what it refuses drops at the player's feet.</summary>
        internal static void Deliver(Player player, List<SalvageReturn> returns)
        {
            var inventory = player.GetInventory();
            int dropped = SalvageDelivery.Deliver(returns.Select(entry => (entry, entry.Amount, entry.MaxStack)),
                (entry, chunk) =>
                {
                    var stack = SalvageInventory.NewStack(entry.Item, chunk);
                    if (inventory.AddItem(stack)) return chunk;
                    // The game merges into partial stacks before it refuses the remainder; the remainder stays in the stack.
                    return entry.MaxStack > 1 ? chunk - stack.m_stack : 0;
                },
                (entry, count) =>
                {
                    try
                    {
                        Transform at = player.transform;
                        var drop = ItemDrop.DropItem(SalvageInventory.NewStack(entry.Item, count), count, at.position + at.forward + at.up, at.rotation);
                        if (drop != null) drop.OnPlayerDrop();
                    }
                    catch (Exception error) { KindleSalvage.Warn("Salvage drop: " + error.GetType().Name); }
                },
                entry => Stacked(inventory, entry));
            if (dropped > 0) Messages.Center(SalvageWords.Dropped);
        }

        /// <summary>Units of the return's prefab currently in the inventory, so a throw after a partial merge is not counted as nothing.</summary>
        static int Stacked(Inventory inventory, SalvageReturn entry)
        {
            int count = 0;
            UnityEngine.GameObject prefab = entry.Item != null ? entry.Item.gameObject : null;
            foreach (ItemDrop.ItemData item in inventory.GetAllItems())
            {
                if (item == null || item.m_stack <= 0 || item.m_dropPrefab != prefab)
                    continue;
                count += item.m_stack;
            }
            return count;
        }

        static void Guard(Action action) { try { action(); } catch (Exception error) { KindleSalvage.Warn("Salvage from chest: " + error.GetType().Name); } }
    }
}
