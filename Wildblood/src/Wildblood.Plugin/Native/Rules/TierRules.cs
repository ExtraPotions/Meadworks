// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
// Changed by expDARE, 2026: Deep North defeat key (Kall Fimbulbringer, defeated_frozenking_p3).
using System.Collections.Generic;
using UnityEngine;

namespace ExtraPotions.Wildblood.Adopted.Rules
{
    /// <summary>
    /// The world tier settings from the rule file's `world tiers:` block. The tier is one number for the whole world:
    /// how many of the listed bosses have been defeated, each counted once however often it is killed, whoever killed
    /// it. It raises star and mutation chances in every biome through the two boost lines, which are indexed by tier the
    /// way the star lines are indexed by stars - past the end the last entry repeats. Bosses roll on their own table
    /// and never read the tier.
    /// </summary>
    public sealed class TierRules
    {
        /// <summary>The key the game sets when each vanilla boss dies, in the order the game presents them. Kall Fimbulbringer
        /// fights in three phase prefabs; only the last, FrozenKing_p3, sets defeated_frozenking_p3, so it counts once.</summary>
        public static readonly string[] VanillaBossKeys =
        {
            "defeated_eikthyr", "defeated_gdking", "defeated_bonemass", "defeated_dragon",
            "defeated_goblinking", "defeated_queen", "defeated_fader", "defeated_frozenking_p3",
        };

        /// <summary>Off holds the world at tier 0, so both boost lines use their first entry and nothing is announced.</summary>
        public bool Enabled = true;
        public int Override = -1;
        public bool Announce = true;

        /// <summary>The global keys that each raise the tier by one while the world carries them. Lower case, no repeats.</summary>
        public List<string> BossKeys = new List<string>(VanillaBossKeys);

        /// <summary>Per tier: each star count's weight is multiplied by this once per star. A judgement call, tunable.</summary>
        public float[] StarBoost = { 1f, 1.1f, 1.2f, 1.3f, 1.4f, 1.5f, 1.6f, 1.7f };

        /// <summary>Per tier: multiplies every mutation chance, capped at 100. A judgement call, tunable.</summary>
        public float[] MutationBoost = { 1f, 1.15f, 1.3f, 1.45f, 1.6f, 1.75f, 1.9f, 2f };

        public float StarBoostAt(int tier) => At(StarBoost, tier);

        public float MutationBoostAt(int tier) => At(MutationBoost, tier);

        private static float At(float[] line, int tier)
        {
            if (line == null || line.Length == 0)
            {
                return 1f;
            }
            return Mathf.Max(0f, line[Mathf.Clamp(tier, 0, line.Length - 1)]);
        }
    }
}
