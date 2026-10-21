// Shepherd - ExtraPotions Meadworks animal husbandry.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks).
//
// Adapted from GrindstoneSkills 0.14.1, Copyright (C) 2026 Gerald Glasgow, GPL-3.0-or-later (ValheimMods monorepo at
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1: GrindstoneSkills/GrindstoneSkills/Husbandry/Yield/ButcherYield.cs Scaled,
// Yield/Produce.cs Advance, Yield/ProduceTable.cs Pick, Yield/ProduceTables.cs IsProduce, Yield/ExtraHoney.cs Reward,
// Companions/PackLeader.cs DealtFactor and Soften).
// Full notice in THIRD_PARTY_NOTICES/ValheimMods-GPL-3.0-or-later.txt.
//
// Changed by expDARE, 2026: Unity-free (doubles, dice through IBreedingRandom); the produce clock keeps the next due
// time in milliseconds under Shepherd's shepherd.produce_next key instead of the last roll in ticks; a zero share or a
// whole amount consumes no roll.
// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using ExtraPotions.Shepherd.Breeding;

namespace ExtraPotions.Shepherd.Yield
{
    public enum ProduceStep { Start, Wait, Roll }

    /// <summary>What one produce tick does and the due time it leaves on the creature's ZDO.</summary>
    public readonly struct ProduceTick
    {
        public ProduceTick(ProduceStep step, long nextMs) { Step = step; NextMs = nextMs; }
        public ProduceStep Step { get; }
        /// <summary>World time in long milliseconds of the next roll (<see cref="BreedingKeys.ProduceNext"/>).</summary>
        public long NextMs { get; }
    }

    /// <summary>
    /// Produce timing (design D3): the first tick only starts the clock, so taming never pays out at once; a due roll
    /// waits while no keeper with a level is in range; it never stacks more than one roll; a clock that lies ahead of
    /// world time by more than one interval (a world moved back) restarts.
    /// </summary>
    public static class ProduceClock
    {
        public static ProduceTick Step(long nowMs, long nextMs, double intervalSeconds, double chance)
        {
            long interval = (long)Math.Max(1000.0, intervalSeconds * 1000.0);
            if (nextMs == 0L || nextMs - nowMs > interval)
                return new ProduceTick(ProduceStep.Start, nowMs + interval);
            if (nowMs < nextMs || chance <= 0.0)
                return new ProduceTick(ProduceStep.Wait, nextMs);
            return new ProduceTick(ProduceStep.Roll, nowMs + interval);
        }
    }

    /// <summary>The decisions of animal yield and pack leader, given the keeper's share (0 to the setting over 100).</summary>
    public static class YieldMath
    {
        /// <summary>The game's cap per drop-list entry.</summary>
        public const int MaxAmount = 100;
        /// <summary>Pack Toughness never takes away more than this.</summary>
        public const double MaxToughness = 0.9;

        /// <summary><paramref name="amount"/> times 1 + <paramref name="share"/>, the fraction a chance of one more; within the game's cap, never below the amount.</summary>
        public static int Butchered(int amount, double share, IBreedingRandom random)
        {
            if (random == null) throw new ArgumentNullException(nameof(random));
            if (amount <= 0 || share <= 0.0) return amount;
            double scaled = amount * (1.0 + share);
            int whole = (int)Math.Floor(scaled);
            double fraction = scaled - whole;
            if (fraction > 0.0 && random.Value() < fraction) whole++;
            return Math.Min(Math.Max(whole, amount), Math.Max(amount, MaxAmount));
        }

        /// <summary>The index picked by weight for a uniform <paramref name="unit"/> in [0, 1); -1 when no weight is positive.</summary>
        public static int PickWeighted(double[] weights, double unit)
        {
            if (weights == null) return -1;
            double total = 0.0;
            int last = -1;
            for (int i = 0; i < weights.Length; i++)
                if (weights[i] > 0.0) { total += weights[i]; last = i; }
            if (last < 0) return -1;
            double roll = Math.Min(Math.Max(unit, 0.0), 1.0) * total;
            for (int i = 0; i < weights.Length; i++)
            {
                if (weights[i] <= 0.0) continue;
                roll -= weights[i];
                if (roll < 0.0) return i;
            }
            return last;
        }

        /// <summary>A drop a living animal may produce: no trophy, no food value, not a cooking input.</summary>
        public static bool IsProduce(bool trophy, double foodValue, bool cookingInput) => !trophy && foodValue == 0.0 && !cookingInput;

        /// <summary>One roll of <paramref name="share"/> per honey handed out; the number of extra honey.</summary>
        public static int ExtraHoney(int count, double share, IBreedingRandom random)
        {
            if (random == null) throw new ArgumentNullException(nameof(random));
            if (count <= 0 || share <= 0.0) return 0;
            int extra = 0;
            for (int i = 0; i < count; i++)
                if (random.Value() < share) extra++;
            return extra;
        }

        /// <summary>Damage dealt by a follower: 1 + the leader's Pack Damage share.</summary>
        public static double PackDealt(double share) => 1.0 + Math.Max(0.0, share);

        /// <summary>Damage taken by a follower: 1 - the leader's Pack Toughness share, at most 90 percent less.</summary>
        public static double PackTaken(double share) => 1.0 - Math.Min(Math.Max(0.0, share), MaxToughness);
    }
}
