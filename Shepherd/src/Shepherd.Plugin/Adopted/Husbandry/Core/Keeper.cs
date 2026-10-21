// Adapted from ValheimMods GrindstoneSkills/GrindstoneSkills/Husbandry/Core/Keeper.cs
// Copyright (C) 2026 Gerald Glasgow. GPL-3.0-or-later.
// Pin: ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: namespace; Crucible boundaries; owned native lifecycle.
// SPDX-License-Identifier: GPL-3.0-or-later
using UnityEngine;

namespace ExtraPotions.Shepherd.Runtime.Adopted.Husbandry
{
    /// <summary>
    /// Who tends an animal: the best Husbandry level among the players near it. Creature code (taming, breeding, eggs,
    /// growing up, produce) runs on the creature's owner, which is often not the keeper's client, so the level is read
    /// from each player's published ZDO value (<see cref="HusbandrySkill.Of"/>). Every loaded player counts, the local
    /// one included; a dedicated server sees the players whose areas it simulates. Animals only simulate while someone
    /// is near, so "the best keeper in range" is also fair when a friend with a higher level visits your farm.
    /// </summary>
    public static class Keeper
    {
        /// <summary>The best Husbandry level among living players within <paramref name="range"/> metres; 0 when none.</summary>
        public static float BestLevel(Vector3 position, float range)
        {
            float best = 0f;
            float rangeSquared = range * range;
            foreach (Player player in Player.GetAllPlayers())
            {
                if (player == null || player.IsDead() || (player.transform.position - position).sqrMagnitude > rangeSquared)
                    continue;
                best = Mathf.Max(best, HusbandrySkill.Of(player));
            }
            return best;
        }

        /// <summary>The best level within Keeper Range.</summary>
        public static float BestLevel(Vector3 position) => BestLevel(position, HusbandrySettings.KeeperRange.Value);

        /// <summary>A perk's share for the best keeper within Keeper Range: 0 when Husbandry is off.</summary>
        public static float Share(float percentAt100, Vector3 position) =>
            HusbandrySkill.Active ? HusbandrySkill.Share(percentAt100, BestLevel(position)) : 0f;
    }
}
