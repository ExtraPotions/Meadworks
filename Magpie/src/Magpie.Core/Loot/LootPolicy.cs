// Magpie - ExtraPotions Meadworks loot handling.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;

namespace ExtraPotions.Magpie.Loot
{
    /// <summary>The spawn path a ground item came from (design section 7).</summary>
    public enum LootSource
    {
        /// <summary>No loot scope was open: never loot.</summary>
        Unknown,
        Creature,
        Destructible,
        /// <summary>MineRock, MineRock5, TreeBase, TreeLog drops; gated by the Destructibles flag.</summary>
        RockOrTree,
        Pickable,
        Spawner,
        PlayerDrop,
        ContainerSpill,
        TombstoneSpill,
        PieceRefund,
        StationOutput,
        ShipSpill,
    }

    /// <summary>The <c>6 - Cheats / Loot Sources</c> flags (design 4.6); never includes player drops, spills, refunds or output.</summary>
    [Flags]
    public enum LootSources
    {
        None = 0,
        Creatures = 1,
        Destructibles = 2,
        Pickables = 4,
        Spawners = 8,
        Default = Creatures | Destructibles | Pickables | Spawners,
    }

    /// <summary>The extra amount a successful take of a loot-marked item adds.</summary>
    public readonly struct LootExtra
    {
        public LootExtra(int amount)
        {
            Amount = amount;
        }

        public int Amount { get; }
    }

    /// <summary>
    /// Loot multiplier rules (design D1 to D5, 3.4; user decisions Q1 world loot only, Q2 skip creature drops while
    /// Wildblood multiplies them, Q3 revised 2026-10-07: extras carry no cheated mark of Magpie's own, Q4 stackable only).
    /// </summary>
    public static class LootPolicy
    {
        public const int MinMultiplier = 1;
        public const int MaxMultiplier = 20;

        /// <summary>Q1: creature, destructible, rock and tree, pickable and spawner drops; nothing else.</summary>
        public static bool IsWorldLoot(LootSource source)
        {
            switch (source)
            {
                case LootSource.Creature:
                case LootSource.Destructible:
                case LootSource.RockOrTree:
                case LootSource.Pickable:
                case LootSource.Spawner:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>The configuration flag that gates a world loot source; None for anything that is never loot.</summary>
        public static LootSources FlagFor(LootSource source)
        {
            switch (source)
            {
                case LootSource.Creature: return LootSources.Creatures;
                case LootSource.Destructible:
                case LootSource.RockOrTree: return LootSources.Destructibles;
                case LootSource.Pickable: return LootSources.Pickables;
                case LootSource.Spawner: return LootSources.Spawners;
                default: return LootSources.None;
            }
        }

        /// <summary>
        /// Whether the spawning peer writes <c>magpie.loot</c> on a new drop: world loot, its flag on, the effective
        /// multiplier above 1 (worlds without the cheat carry no extra data), and for creature drops only while
        /// Wildblood's creature drop multiplier is not above 1 (Q2; a non-finite value reads as Wildblood absent).
        /// </summary>
        public static bool ShouldMark(LootSource source, LootSources enabled, int multiplier, float wildbloodCreatureMultiplier)
        {
            if (ClampMultiplier(multiplier) <= 1 || !IsWorldLoot(source))
                return false;
            var flag = FlagFor(source);
            if ((enabled & flag) != flag)
                return false;
            if (source == LootSource.Creature && wildbloodCreatureMultiplier > 1f)
                return false;
            return true;
        }

        /// <summary>The multiplier to act on: the configured value while the authority gate allows the cheat, else the safe value 1 (section 4.6).</summary>
        public static int EffectiveMultiplier(int configured, bool allowed) => allowed ? ClampMultiplier(configured) : MinMultiplier;

        /// <summary>Amount the take really added: the inventory count difference, so a partial take multiplies only what was taken (3.4).</summary>
        public static int Picked(int before, int after) => Math.Max(0, after - before);

        /// <summary>
        /// User decision 2026-10-07 (Q3 revised, "drop the mark"): Magpie adds no cheated mark of its own to extras. The
        /// extras are clones of the taken item, so they keep the game's mark only when the taken item already had it; the
        /// multiplier itself stays a server-gated cheat (C02).
        /// </summary>
        public static bool ExtraCheated(bool takenCheated) => takenCheated;

        public static int ClampMultiplier(int multiplier) => Math.Min(MaxMultiplier, Math.Max(MinMultiplier, multiplier));

        /// <summary>
        /// Extra = picked amount x (multiplier - 1), where the picked amount is the inventory count difference, so a partial
        /// take only multiplies what was taken (3.4). No extra for unmarked items or, with Stackable Only, non-stackable items.
        /// </summary>
        public static LootExtra Extra(bool marked, int maxStack, int pickedAmount, int multiplier, bool stackableOnly)
        {
            var effective = ClampMultiplier(multiplier);
            if (!marked || effective <= 1 || pickedAmount <= 0 || (stackableOnly && maxStack <= 1))
                return new LootExtra(0);
            var amount = (long)pickedAmount * (effective - 1);
            return new LootExtra(amount > int.MaxValue ? int.MaxValue : (int)amount);
        }

        /// <summary>Stack sizes for adding an amount as clones of the item; a maximum below 1 reads as 1.</summary>
        public static IReadOnlyList<int> SplitIntoStacks(int amount, int maxStack)
        {
            var size = Math.Max(1, maxStack);
            var stacks = new List<int>();
            for (var left = Math.Max(0, amount); left > 0; left -= size)
                stacks.Add(Math.Min(size, left));
            return stacks;
        }
    }
}
