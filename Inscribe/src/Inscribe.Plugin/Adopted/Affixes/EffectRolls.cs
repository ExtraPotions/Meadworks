// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.10.0 at d3b57606cb24775d61b7bda244b22eb89e7e7717.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
using System;
using System.Collections.Generic;
using ExtraPotions.Inscribe.Adopted.Rules;

namespace ExtraPotions.Inscribe.Adopted.Affixes
{
    /// <summary>One roll that counts for effects: an active inscription with its live definition.</summary>
    public readonly struct EffectRoll
    {
        public EffectRoll(AffixRoll roll, AffixDef def)
        {
            Roll = roll;
            Def = def;
        }

        public AffixRoll Roll { get; }
        public AffixDef Def { get; }
    }

    /// <summary>
    /// Builds an item state's <see cref="ItemState.EffectRolls"/> once, when the state is resolved: the active affixes
    /// in stored order, then the active gems' inscriptions in socket order (a gem counts exactly like the same inscription
    /// rolled on the item, channel caps included). Dormant affixes and gems count for nothing.
    /// </summary>
    internal static class EffectRollBuilder
    {
        public static EffectRoll[] Build(ItemState state)
        {
            if (state.AffixCount == 0 && state.Gems.Count == 0)
            {
                return Array.Empty<EffectRoll>();
            }
            List<EffectRoll> rolls = new List<EffectRoll>(state.AffixCount + state.Gems.Count);
            for (int i = 0; i < state.AffixCount; i++)
            {
                if (state.IsActiveAt(i))
                {
                    rolls.Add(new EffectRoll(state.Affixes[i], state.DefinitionAt(i)!));
                }
            }
            for (int i = 0; i < state.Gems.Count; i++)
            {
                if (state.IsGemActiveAt(i))
                {
                    rolls.Add(new EffectRoll(state.Gems[i].Roll, state.GemDefinitionAt(i)!));
                }
            }
            return rolls.ToArray();
        }
    }
}
