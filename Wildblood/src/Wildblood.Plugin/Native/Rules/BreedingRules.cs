// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
namespace ExtraPotions.Wildblood.Adopted.Rules
{
    /// <summary>
    /// The rule file's `breeding:` block. A newborn of tamed parents inherits from them instead of rolling like a wild
    /// creature: one of their mutations when either parent has one, and a star count drawn with equal odds from 0 up to
    /// the stronger parent's. Growing up keeps a young creature's traits whatever this block says - that is not
    /// inheritance, it is the same creature changing shape.
    /// </summary>
    public sealed class BreedingRules
    {
        /// <summary>Off lets newborns roll their stars and mutations from their biome, like any wild creature.</summary>
        public bool Enabled = true;

        /// <summary>Percent chance a newborn takes one of its parents' mutations when either parent has one.</summary>
        public float MutationChance = 100f;
    }
}
