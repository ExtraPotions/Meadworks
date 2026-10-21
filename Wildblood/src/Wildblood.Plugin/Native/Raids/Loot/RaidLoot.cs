// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// Changed by expDARE, 2026: Wildblood namespace and authority-owned raid integration.
using System.Collections.Generic;
using ExtraPotions.Wildblood.Adopted.Loot;
using ExtraPotions.Wildblood.Adopted.Patches;
using UnityEngine;

namespace ExtraPotions.Wildblood.Adopted.Raids
{
    /// <summary>
    /// A raider's own loot, times the raid's drops (features/raids.md section 3): its drop list as the mod's loot rules
    /// left it (stars, mutations, aspects, the world-wide multiplier), scaled once more by the multiplier the raider pays
    /// at (<see cref="RaiderPurse.DropsFor"/>) - x5 in a deadly raid, a tenth to a half in a trivial one, x1 once the
    /// raid was lost. The scaling is <see cref="OwnDrops"/>: the creature's own rows only, trophies when the trophy switch
    /// says so, below 1 rounded at random so a raid that pays a tenth mostly drops nothing. A raider that forfeits (a
    /// stop) drops nothing at all, every row cleared. The coins it carried are not here: they are no loot and never
    /// multiplied (<see cref="RaidCoins"/>). Called last by the drop list's patch (<see cref="LootPatch"/>), on the dying
    /// raider's owner, while its ZDO still answers; a failure is reported and swallowed, leaving the list as the loot
    /// rules made it.
    /// </summary>
    internal static class RaidLoot
    {
        /// <summary>Every kill's drop list passes here: a creature that is no raider costs three field reads and one ZDO
        /// lookup.</summary>
        public static void Apply(CharacterDrop drop, List<KeyValuePair<GameObject, int>> result)
        {
            Character character = drop.m_character;
            ZNetView? nview = character != null ? character.m_nview : null;
            ZDO? zdo = nview != null ? nview.GetZDO() : null;
            if (result != null && RaiderTag.IsRaider(zdo))
            {
                SafeCall.Run("CharacterDrop.GenerateDropList raid loot", static (d, r) => Rework(d, r), drop, result);
            }
        }

        private static void Rework(CharacterDrop drop, List<KeyValuePair<GameObject, int>> result)
        {
            ZDO zdo = drop.m_character.m_nview.GetZDO();
            if (RaiderPurse.Forfeit(zdo))
            {
                result.Clear(); // a stop: nothing at all, whoever put it in the list
                return;
            }
            float factor = Mathf.Max(0f, RaiderPurse.DropsFor(zdo));
            if (!Mathf.Approximately(factor, 1f))
            {
                OwnDrops.Scale(drop, result, factor);
            }
        }
    }
}
