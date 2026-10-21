// Adapted from assets/references/source/ValheimMods-openkeep-3.0.0/GrindstoneSkills/GrindstoneSkills/Pickaxes/Seams/CleanStrikes.cs at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Copyright (C) 2026 Gerald Glasgow. SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Saga namespace, configuration, CS1 and ownership boundaries.
using UnityEngine;

namespace ExtraPotions.Saga.Runtime.Adopted.Grindstone
{
    /// <summary>
    /// A clean strike: the local miner's hit on an open seam's chunk inside its window, on the miner's own client, inside
    /// the swing and before the game sends the hit to the rock's owner (<see cref="Seams.OnLocalHit"/>). In this order:
    /// <list type="bullet">
    /// <item>the hit's damage, every type, is multiplied (<see cref="Multiplier"/>); the owner applies the hit as sent;</item>
    /// <item>the owner is told first (<see cref="CleanStrikeMarks.Send"/>), so the break of that chunk can give an ore
    /// deposit its extra roll: the mark goes the same route as the hit and arrives before it;</item>
    /// <item>the experience is credited at once (<see cref="MineXp.OnCleanStrike"/>);</item>
    /// <item>"Clean strike!" floats up over the chunk for the miner, "Clean strike ×N!" from the second link of a chain,
    /// a little above the game's own damage number there.</item>
    /// </list>
    /// </summary>
    internal static class CleanStrikes
    {
        /// <summary>Metres above the chunk's centre, where the game shows the hit's damage number.</summary>
        private const float CalloutRise = 0.6f;

        /// <summary>Lands a clean strike that is link <paramref name="link"/> of its chain (1 = the first).</summary>
        public static void Land(Rock rock, HitData hit, int area, int link)
        {
            hit.m_damage.Modify(Multiplier(link, PickSkill.Local()));
            CleanStrikeMarks.Send(rock, area);
            HookGuard.Run("clean strike experience", () => MineXp.OnCleanStrike(rock));
            MineCallout.ShowLocal(RockChunks.Centre(rock, area) + Vector3.up * CalloutRise, Callout(link));
        }

        /// <summary>
        /// The damage multiplier of chain link <paramref name="link"/> (1 = the first clean strike). Below the Unbroken
        /// level every link is Clean Strike Damage. From it, each link after the first adds Unbroken Bonus Per Link
        /// percent of that base, for at most Unbroken Max Links links; later links keep the last multiplier. With the
        /// defaults: ×2, ×2.4, ×2.8, ×3.2, ×3.6, ×4, then ×4.
        /// </summary>
        public static float Multiplier(int link, float level)
        {
            float multiplier = Mathf.Max(1f, SeamSettings.CleanStrikeDamage.Value);
            if (link < 2 || !PickSkill.Reached(level, SeamSettings.UnbrokenLevel.Value))
                return multiplier;
            int links = Mathf.Min(link - 1, Mathf.Max(0, SeamSettings.UnbrokenMaxLinks.Value));
            return multiplier * (1f + Mathf.Max(0f, SeamSettings.UnbrokenBonus.Value) / 100f * links);
        }

        private static string Callout(int link) => link < 2 ? SagaText.Text("gs_02e35dcee375b8ec") : SagaText.Format("gs_5b96158829d76631", link);
    }
}
