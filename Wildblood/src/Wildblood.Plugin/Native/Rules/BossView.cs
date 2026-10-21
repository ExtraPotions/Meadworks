// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
namespace ExtraPotions.Wildblood.Adopted.Rules
{
    /// <summary>
    /// Presents the boss table as a <see cref="BiomeRules"/> so a boss travels the same scaling path a creature does -
    /// <c>StatMath</c>, <c>DamageMath</c> and the drop patch all read <c>Rules.Star</c> and need no boss branch of
    /// their own. Mutation chance is zero throughout, which is what keeps bosses free of mutations by construction
    /// rather than by a check every caller would have to remember. A boss that keeps its game level (boss stars off)
    /// gets neutral star power, <paramref name="unstarred"/>.
    /// </summary>
    public static class BossView
    {
        public static BiomeRules For(BossRules boss, bool unstarred = false)
        {
            return new BiomeRules
            {
                StarChances = boss.StarChances,
                Star = unstarred ? new StarPower() : boss.Star,
                MutationChance = new[] { 0f },
                LargeStarPower = 1f, // no mutations to enhance, so nothing to enhance them by
            };
        }
    }
}
