// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.10.0 at d3b57606cb24775d61b7bda244b22eb89e7e7717.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries; socket odds
// per MeadWorks tier.
using System;
using System.Linq;
using ExtraPotions.Inscribe.Adopted.Affixes;
using ExtraPotions.Inscribe.Adopted.Core;
using ExtraPotions.Inscribe.Adopted.Items;
using ExtraPotions.Inscribe.Adopted.Rolling;
using ExtraPotions.Inscribe.Adopted.Rules;

namespace ExtraPotions.Inscribe.Adopted.Sockets
{
    /// <summary>
    /// Sockets on dropped gear (sockets.md section 2): every pre-rolled weapon, staff, armour piece or shield of a magic
    /// rarity draws 0-3 sockets once, while <c>Gems and sockets</c> is on. Fixed weights per tier for 0/1/2/3 sockets:
    /// Uncommon 80/15/4/1, Rare 60/25/11/4, Epic 35/35/22/8, Legendary 0/40/40/20 (at least one). Runs where the drop is built (the creature's owner). The Dvergr
    /// Chisel cuts by the same weights without the none (sockets.md section 5).
    /// </summary>
    internal static class SocketDrops
    {

        /// <summary>The rolled state with its sockets; the state itself when none are drawn or the base takes none.</summary>
        public static ItemState Add(ItemState state, ClassInfo info, RarityDef rarity, Random random)
        {
            if (!SocketSwitch.On || GemCatalog.BaseOf(info) == SocketBase.None)
            {
                return state;
            }
            int count = RollMath.PickWeighted(ExtraPotions.Inscribe.Rarity.RarityTiers.SocketWeights(rarity.Id), random);
            return count <= 0 ? state : state.ToBuilder().SetSockets(count).Build();
        }

        /// <summary>
        /// The sockets a Dvergr Chisel cuts (user 2026-10-08: "I want the dropped gear odds on the chisel use too"): 1-3 by
        /// the drop weights of the item's rarity, a Normal item as Magic, never none.
        /// </summary>
        public static int Cut(string? rarityId, Random random) => 1 + RollMath.PickWeighted(CutWeights(rarityId), random);

        /// <summary>The chance in percent that a chisel cuts this many sockets (1-3) into an item of this rarity.</summary>
        public static string CutChance(string? rarityId, int sockets)
        {
            float[] weights = CutWeights(rarityId);
            return Numbers.Format(100f * weights[sockets - 1] / weights.Sum());
        }

        // The drop weights of the rarity without the none (Rarity/RarityTiers in Inscribe.Core).
        private static float[] CutWeights(string? rarityId) => ExtraPotions.Inscribe.Rarity.RarityTiers.SocketWeights(rarityId).Skip(1).ToArray();
    }
}
