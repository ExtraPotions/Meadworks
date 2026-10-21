// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.10.0 at d3b57606cb24775d61b7bda244b22eb89e7e7717.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
using System.Collections.Generic;
using ExtraPotions.Inscribe.Adopted.Rules;

namespace ExtraPotions.Inscribe.Adopted.Loot
{
    /// <summary>What is being rolled: a creature's death (drops.md sections 2-10) or a world container (section 11).</summary>
    public enum LootSource
    {
        Creature,
        Chest,
    }

    /// <summary>What one roll asks for (drops.md section 4): a tier, stars, the boss and creature entries, and switches.</summary>
    public struct LootInput
    {
        /// <summary>Creature (default) or chest: picks the base chances (<c>drops.chances</c> or <c>drops.chests</c>).</summary>
        public LootSource Source;

        /// <summary>1..8.</summary>
        public int Tier;

        public int Stars;

        /// <summary>A boss for the rarity rows: in the boss map or flagged by the game.</summary>
        public bool IsBoss;

        /// <summary>The boss map entry; its guarantees replace the chance model (see <see cref="LootPlanner"/>).</summary>
        public BossDrop? Boss;

        /// <summary>The creature's prefab name (null for a chest or a test roll at a bare tier): the boss gems key on it.</summary>
        public string? Prefab;

        /// <summary>The creature entry, or for a chest its container entry (same shape, drops.md section 11).</summary>
        public CreatureDrop? Creature;
        public bool StonesOn;
        public bool GearOn;
        public LootModifiers Modifiers;

        /// <summary>Elite Creatures Reborn terms (ecr-integration.md); the default changes nothing (chests, test rolls).</summary>
        public EcrRoll Ecr;
    }

    /// <summary>
    /// The result of one roll before anything is spawned: the stones drawn (one entry per stone) and the rarities of
    /// the gear items drawn (one entry per item). Reused between kills by the roller, so a kill allocates nothing here.
    /// </summary>
    public sealed class LootPlan
    {
        public readonly List<StoneDef> Stones = new List<StoneDef>();
        public readonly List<RarityDef> Gear = new List<RarityDef>();

        public int Tier { get; internal set; }
        public bool IsBoss { get; internal set; }

        /// <summary>The expected-count <c>p</c> of each chance roll (0 for a boss-map boss, whose counts are guaranteed).</summary>
        public float StoneChance { get; internal set; }

        public float GearChance { get; internal set; }

        internal void Reset(int tier, bool boss)
        {
            Stones.Clear();
            Gear.Clear();
            Tier = tier;
            IsBoss = boss;
            StoneChance = 0f;
            GearChance = 0f;
        }
    }
}
