// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.10.0 at d3b57606cb24775d61b7bda244b22eb89e7e7717.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
using System.Collections.Generic;
using ExtraPotions.Inscribe.Adopted.Affixes;
using ExtraPotions.Inscribe.Adopted.Rolling;
using ExtraPotions.Inscribe.Adopted.Rules;

namespace ExtraPotions.Inscribe.Adopted.Sockets
{
    /// <summary>
    /// A gem's roll when it is socketed (sockets.md section 4): a tier of its inscription's ladder that the item's level
    /// has unlocked, weaker tiers more often (the tier weights), the whole ladder open (as on a best-fit class); an item
    /// below the ladder's first unlock takes the weakest tier. The value is uniform in the tier's range, a scaled
    /// inscription times the class's damage_scale, exactly as an inscription rolls. Pure: never writes.
    /// </summary>
    internal static class GemRolls
    {
        private static readonly List<AffixTierDef> Rows = new List<AffixTierDef>();
        private static readonly List<float> Scratch = new List<float>();

        /// <summary>The roll, or null when the inscription has no tier that may roll at all (every weight 0).</summary>
        public static AffixRoll? Roll(AffixDef def, RollContext context)
        {
            TierEligibility.Eligible(def, ClassFit.Best, context, Rows);
            if (Rows.Count == 0)
            {
                AffixTierDef? weakest = Weakest(def);
                if (weakest == null)
                {
                    return null;
                }
                Rows.Add(weakest);
            }
            AffixTierDef tier = TierEligibility.Pick(Rows, context, Scratch);
            return new AffixRoll(def.Id, tier.Grade, ValueOf(def, tier, context));
        }

        /// <summary>
        /// The grades a gem can roll at this item level: the weakest tier, and the best one the level unlocks (the weakest
        /// when none is). False when no tier may roll at all.
        /// </summary>
        public static bool Range(AffixDef def, int level, out int weakest, out int best)
        {
            AffixTierDef? low = Weakest(def);
            weakest = best = low?.Grade ?? 0;
            for (int i = 0; low != null && i < def.Tiers.Count; i++)
            {
                AffixTierDef row = def.Tiers[i];
                if (row.Weight > 0f && row.Level <= level && row.Grade > best)
                {
                    best = row.Grade;
                }
            }
            return low != null;
        }

        private static AffixTierDef? Weakest(AffixDef def)
        {
            for (int i = 0; i < def.Tiers.Count; i++)
            {
                if (def.Tiers[i].Weight > 0f)
                {
                    return def.Tiers[i];
                }
            }
            return null;
        }

        private static float ValueOf(AffixDef def, AffixTierDef tier, RollContext context)
        {
            if (def.Value == AffixValueType.Flag)
            {
                return 1f;
            }
            float value = RollMath.RollValue(tier.Min, tier.Max, tier.Decimals, context.Random);
            return def.Scaled ? RollMath.Scale(value, context.Class.DamageScale, tier.Decimals) : value;
        }
    }
}
