// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.10.0 at d3b57606cb24775d61b7bda244b22eb89e7e7717.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
using System;
using System.Collections.Generic;

namespace ExtraPotions.Inscribe.Adopted.Rules
{
    /// <summary>
    /// The <c>drops:</c> section (drops.md, economy-yaml.md section 8). Tier-indexed arrays have 8 entries, tier 1 first
    /// (a YAML list of 7 is read with its last value repeated for tier 8; classes-and-tiers.md section 2).
    /// </summary>
    public sealed class DropRules
    {
        public bool Tamed { get; internal set; }
        public bool RequirePlayer { get; internal set; } = true;
        public int MaxStonesPerKill { get; internal set; } = 5;
        public int MaxGearPerKill { get; internal set; } = 2;
        public IReadOnlyList<float> StarMultipliers { get; internal set; } = new[] { 1f, 2f, 3f };
        public float StarStep { get; internal set; } = 1f;
        public IReadOnlyList<float> StoneChance { get; internal set; } = new float[8];
        public IReadOnlyList<float> GearChance { get; internal set; } = new float[8];

        /// <summary>stone id → 8 weights.</summary>
        public IReadOnlyDictionary<string, float[]> Stones { get; internal set; } = new Dictionary<string, float[]>();

        /// <summary>rarity id → 8 weights.</summary>
        public IReadOnlyDictionary<string, float[]> RarityWeights { get; internal set; } = new Dictionary<string, float[]>();
        public IReadOnlyDictionary<string, float[]> BossRarityWeights { get; internal set; } = new Dictionary<string, float[]>();

        public GearDropRules Gear { get; internal set; } = new GearDropRules();
        public IReadOnlyDictionary<string, BossDrop> Bosses { get; internal set; } = new Dictionary<string, BossDrop>();
        public IReadOnlyDictionary<string, CreatureDrop> Creatures { get; internal set; } = new Dictionary<string, CreatureDrop>();
        public ChestDrops Chests { get; internal set; } = new ChestDrops();

        /// <summary><c>drops.ecr</c>: the Elite Creatures Reborn terms (ecr-integration.md).</summary>
        public EcrDrops Ecr { get; internal set; } = new EcrDrops();

        /// <summary>The star multiplier for a creature with this many stars (0 = unstarred).</summary>
        public float StarMultiplier(int stars)
        {
            if (StarMultipliers.Count == 0)
            {
                return 1f;
            }
            int last = StarMultipliers.Count - 1;
            return stars <= last ? StarMultipliers[Math.Max(0, stars)] : StarMultipliers[last] + (stars - last) * StarStep;
        }
    }

    public sealed class GearDropRules
    {
        public bool RequireRecipe { get; internal set; } = true;
        public int TiersBelow { get; internal set; } = 1;
        public float SameTierWeight { get; internal set; } = 3f;
        public float LowerTierWeight { get; internal set; } = 1f;
        public IReadOnlyList<string> Exclude { get; internal set; } = Array.Empty<string>();
        public IReadOnlyDictionary<string, int> Include { get; internal set; } = new Dictionary<string, int>();
    }

    public sealed class BossDrop
    {
        public int Tier { get; internal set; } = 1;
        public int StoneRolls { get; internal set; }
        public int GearRolls { get; internal set; }
        public int ScrollRolls { get; internal set; }
        public IReadOnlyList<DropBonus> Bonus { get; internal set; } = Array.Empty<DropBonus>();
    }

    public sealed class CreatureDrop
    {
        /// <summary>0 = use the biome's tier.</summary>
        public int Tier { get; internal set; }
        public float Multiplier { get; internal set; } = 1f;
        public float StoneMultiplier { get; internal set; } = 1f;
        public float GearMultiplier { get; internal set; } = 1f;
        public float ScrollMultiplier { get; internal set; } = 1f;
        public IReadOnlyList<DropBonus> Bonus { get; internal set; } = Array.Empty<DropBonus>();
    }

    public sealed class DropBonus
    {
        public string Stone { get; internal set; } = "";

        /// <summary>Percent.</summary>
        public float Chance { get; internal set; } = 100f;

        public int Amount { get; internal set; } = 1;
    }

    /// <summary>World-container drops (drops.md section 11): flat chances plus per-container-prefab overrides.</summary>
    public sealed class ChestDrops
    {
        public float StoneChance { get; internal set; } = 50f;
        public float GearChance { get; internal set; } = 10f;

        /// <summary>Container prefab -> override, in the <c>drops.creatures</c> entry shape (IMP-79).</summary>
        public IReadOnlyDictionary<string, CreatureDrop> Containers { get; internal set; } =
            new Dictionary<string, CreatureDrop>(System.StringComparer.Ordinal);
    }
}
