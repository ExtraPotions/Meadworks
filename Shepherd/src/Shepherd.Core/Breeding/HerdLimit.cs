// Shepherd - ExtraPotions Meadworks animal husbandry.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
using System;

namespace ExtraPotions.Shepherd.Breeding
{
    /// <summary>One crowding check: the parent's own prefab plus offspring prefab within <c>m_totalCheckRange</c>.</summary>
    public readonly struct HerdInput
    {
        public HerdInput(int vanillaMax, int count, double herdSizeAt100, double keeperLevel, bool unlimited, bool strict)
        {
            VanillaMax = Math.Max(0, vanillaMax); Count = Math.Max(0, count); HerdSizeAt100 = herdSizeAt100;
            KeeperLevel = keeperLevel; Unlimited = unlimited; Strict = strict;
        }

        /// <summary>The prefab's <c>Procreation.m_maxCreatures</c>.</summary>
        public int VanillaMax { get; }
        public int Count { get; }
        public double HerdSizeAt100 { get; }
        public double KeeperLevel { get; }
        /// <summary>The Unlimited Herd cheat, already gated (C02 safe value false when blocked).</summary>
        public bool Unlimited { get; }
        /// <summary>Strict Herd Limit (off by default).</summary>
        public bool Strict { get; }
    }

    public sealed class HerdVerdict
    {
        internal HerdVerdict(int bonus, int limit, int count, bool unlimited, bool strict)
        {
            Bonus = bonus; Limit = limit; Unlimited = unlimited;
            CanConceive = unlimited || count < limit;
            BirthMayProceed = unlimited || !strict || count < limit;
            Room = unlimited ? int.MaxValue : Math.Max(0, limit - count);
        }

        /// <summary>Herd Size bonus from the keeper's level.</summary>
        public int Bonus { get; }
        /// <summary>The limit the plugin steers <c>m_maxCreatures</c> to; <see cref="int.MaxValue"/> when unlimited.</summary>
        public int Limit { get; }
        public bool Unlimited { get; }
        /// <summary>The game's conception test passes: count below the limit.</summary>
        public bool CanConceive { get; }
        /// <summary>A due birth (or twin) may happen now: always, except under the strict limit at or over the limit.</summary>
        public bool BirthMayProceed { get; }
        /// <summary>The hover's "Waiting for room".</summary>
        public bool WaitingForRoom => !BirthMayProceed;
        /// <summary>Herd room for the hover (limit minus count, never negative).</summary>
        public int Room { get; }
        /// <summary>
        /// Room the plugin adds to <c>m_maxCreatures</c> for one Procreate call: the keeper bonus, or everything up to
        /// <see cref="int.MaxValue"/> under Unlimited Herd (the pace edit clamps the sum).
        /// </summary>
        public int ExtraMax => Unlimited ? int.MaxValue : Bonus;
    }

    /// <summary>The herd line of the lore hover: a <see cref="ShepherdText"/> key and its two numbers.</summary>
    public readonly struct HerdLine
    {
        public HerdLine(string key, int room, int limit) { Key = key; Room = room; Limit = limit; }
        public string Key { get; }
        public int Room { get; }
        public int Limit { get; }
    }

    /// <summary>
    /// Herd limits (design 10.5). Vanilla: conception needs the count below <c>m_maxCreatures</c>, and a pregnant animal
    /// gives birth even when crowded. Shepherd: limit = <c>m_maxCreatures</c> + Herd Size bonus; Unlimited Herd removes
    /// it; Strict Herd Limit holds a due birth while the count is at or above the limit.
    /// </summary>
    public static class HerdLimit
    {
        public static HerdVerdict Evaluate(HerdInput input)
        {
            int bonus = HusbandryShare.ExtraRoom(input.HerdSizeAt100, input.KeeperLevel);
            int limit = input.Unlimited ? int.MaxValue : (int)Math.Min(int.MaxValue - 1L, (long)input.VanillaMax + bonus);
            return new HerdVerdict(bonus, limit, input.Count, input.Unlimited, input.Strict);
        }

        /// <summary>Strict Herd Limit: a due pregnancy waits while the herd is at or over its limit (the game would give birth).</summary>
        public static bool HoldsBirth(HerdVerdict verdict, bool pregnant, bool due) =>
            verdict != null && pregnant && due && !verdict.BirthMayProceed;

        /// <summary>"Waiting for room" for a held birth, "unlimited" under the cheat, otherwise room of limit.</summary>
        public static HerdLine Describe(HerdVerdict verdict, bool pregnant, bool due)
        {
            if (verdict == null) throw new ArgumentNullException(nameof(verdict));
            if (HoldsBirth(verdict, pregnant, due)) return new HerdLine(ShepherdText.HerdWaiting, 0, verdict.Limit);
            if (verdict.Unlimited) return new HerdLine(ShepherdText.HerdUnlimited, 0, 0);
            return new HerdLine(ShepherdText.HerdRoom, verdict.Room, verdict.Limit);
        }
    }
}
