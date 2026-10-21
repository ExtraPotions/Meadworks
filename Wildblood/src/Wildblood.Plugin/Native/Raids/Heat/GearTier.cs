// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// Changed by expDARE, 2026: Wildblood namespace, authority-owned raid integration and the Deep North raid tier 7.
using System.Collections.Generic;
using UnityEngine;

namespace ExtraPotions.Wildblood.Adopted.Raids
{
    /// <summary>
    /// A player's gear tier, 0-7 for a raid (features/raids.md section 3) and for Hel's Trial (7 the Deep North's
    /// Bloodgold): the higher of the best metal they have ever held - the game's known materials - and the tier of the
    /// armour they wear (<see cref="GearItems"/> lists both). The known
    /// materials and the worn items live only on that player's own client, so each client works its own player's tier out
    /// when it can change (<see cref="GearTierPatch"/>: equipment set up, a new item known, spawning) and writes it into
    /// its player's ZDO when it differs; the raid's owner then reads it from there for every player near (<see cref="Of"/>).
    /// </summary>
    public static class GearTier
    {
        /// <summary>The tier a player published, as the raids count it (0-7, the Deep North its own tier); 0 for one that
        /// has not (yet).</summary>
        public static int Of(Player player) => Mathf.Clamp(Published(player), 0, RaidTable.MaxTier);

        /// <summary>The tier a player published, the Deep North's included (0-7), for Hel's Trial.</summary>
        public static int Full(Player player) => Mathf.Clamp(Published(player), 0, TopTier);

        /// <summary>The highest tier: Bloodgold, the Deep North's.</summary>
        public const int TopTier = 7;

        private static int Published(Player player)
        {
            ZDO? zdo = player.m_nview != null ? player.m_nview.GetZDO() : null;
            return zdo != null ? zdo.GetInt(RaidKeys.GearTierHash) : 0;
        }

        /// <summary>The local player's client: works the tier out and writes it when it changed.</summary>
        public static void Publish(Player player)
        {
            if (!ReferenceEquals(player, Player.m_localPlayer) || player.m_nview == null)
            {
                return;
            }
            ZDO? zdo = player.m_nview.GetZDO();
            if (zdo == null || !zdo.IsOwner())
            {
                return;
            }
            int tier = Work(player);
            if (zdo.GetInt(RaidKeys.GearTierHash) != tier)
            {
                zdo.Set(RaidKeys.GearTierHash, tier);
            }
        }

        /// <summary>The tier from what this machine knows of its own player: metal held and armour worn.</summary>
        public static int Work(Player player) => Mathf.Max(Metal(player), Worn(player));

        private static int Metal(Player player)
        {
            int best = 0;
            foreach (KeyValuePair<string, int> metal in GearItems.MetalTokens)
            {
                if (metal.Value > best && player.m_knownMaterial.Contains(metal.Key))
                {
                    best = metal.Value;
                }
            }
            return best;
        }

        private static int Worn(Player player) => Mathf.Max(GearItems.ArmourTier(player.m_helmetItem),
            Mathf.Max(GearItems.ArmourTier(player.m_chestItem), GearItems.ArmourTier(player.m_legItem)));
    }
}
