// Wildblood - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: loot mode names (design E1: EliteCreaturesReborn Vanilla, Scaled, Rolled, Curated; CreatureManager
// CalculateChance named Amount) and the drop multiplier cheat values (design 4.12, E8).
using System;

namespace ExtraPotions.Wildblood.Loot
{
    public enum LootMode { Vanilla, Scaled, Rolled, Amount, Curated }

    /// <summary>The drop multipliers in effect: all 1 unless the Drop Multipliers cheat is allowed and on.</summary>
    public readonly struct DropMultipliers : IEquatable<DropMultipliers>
    {
        public DropMultipliers(float creature, float boss, float trophy)
        {
            Creature = creature;
            Boss = boss;
            Trophy = trophy;
        }

        public static DropMultipliers None { get; } = new DropMultipliers(1f, 1f, 1f);

        /// <summary>Non-trophy creature drops after the loot mode.</summary>
        public float Creature { get; }

        /// <summary>Boss drops, on top of <see cref="Creature"/>.</summary>
        public float Boss { get; }

        /// <summary>Trophy amounts when a trophy drops.</summary>
        public float Trophy { get; }

        public bool IsNone => Equals(None);

        public bool Equals(DropMultipliers other) => Creature.Equals(other.Creature) && Boss.Equals(other.Boss) && Trophy.Equals(other.Trophy);

        public override bool Equals(object? obj) => obj is DropMultipliers other && Equals(other);

        public override int GetHashCode() => (Creature, Boss, Trophy).GetHashCode();

        public override string ToString() => "creature x" + Creature + ", boss x" + Boss + ", trophy x" + Trophy;
    }
}
