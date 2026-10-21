// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn, GPL-3.0-or-later
// Horde adoption adapted from ECR f331465b3ed2551c89decc42ba81565a7b689543.
// Changed by expDARE, 2026: Wildblood authority, lifecycle and existing stat/loot seams.
using System.Collections.Generic;
using ExtraPotions.Wildblood.Adopted.Loot;
using ExtraPotions.Wildblood.AdoptedLibs.PatchGuard;
using UnityEngine;

namespace ExtraPotions.Wildblood.Adopted.Horde
{
    /// <summary>
    /// A horde creature's own loot, times 1 / the spawn multiplier (features/horde.md): twice the creatures at half the
    /// loot each, so the horde brings more fights, not more loot. The scaling is <see cref="OwnDrops"/>, the raiders'
    /// own: the creature's rows only, trophies when the trophy switch says so, rounded at random. Called by the drop
    /// list's patch (<see cref="LootPatch"/>) on the dying creature's owner, while its ZDO still answers; a failure is
    /// reported and swallowed, leaving the list as the loot rules made it.
    /// </summary>
    internal static class HordeLoot
    {
        /// <summary>Every kill's drop list passes here: with the horde off it costs one config read.</summary>
        public static void Apply(CharacterDrop drop, List<KeyValuePair<GameObject, int>> result)
        {
            Character character = drop.m_character;
            if (result == null || character == null || !HordeSettings.Enabled
                || character.m_nview == null || !character.m_nview.IsValid() || !character.m_nview.IsOwner())
            {
                return;
            }
            ZNetView? nview = character.m_nview;
            float factor = HordeFactors.Drops(character, nview != null ? nview.GetZDO() : null);
            if (!Mathf.Approximately(factor, 1f))
            {
                Guard.Run("CharacterDrop.GenerateDropList horde loot",
                    static (d, r) => OwnDrops.Scale(d, r, 1f / HordeSettings.Spawns), drop, result);
            }
        }
    }
}
