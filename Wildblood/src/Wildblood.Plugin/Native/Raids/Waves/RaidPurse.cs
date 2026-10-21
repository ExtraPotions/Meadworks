// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// Changed by expDARE, 2026: Wildblood namespace and authority-owned raid integration.
using UnityEngine;

namespace ExtraPotions.Wildblood.Adopted.Raids
{
    /// <summary>
    /// The coins each raider carries (features/raids.md section 3, "The gold"): every coin the raiders carry between them
    /// (<see cref="RaidState.CoinsBack"/>) is shared across every raider of every wave, the Warlord carrying a quarter.
    /// Each share is taken out of <see cref="RaidState.CoinsUnspent"/> as the raider arrives, so what is handed out comes
    /// to exactly the coins back: a regular raider takes an even part of what is unspent (less the Warlord's quarter while
    /// the Warlord is still to come) over the regular raiders still to come, itself included - this wave's rest plus what
    /// the later waves are expected to bring, re-planned as each wave begins - and the last raider of the raid takes
    /// whatever is left. Host's owner only, in the frame the raider is tagged.
    /// </summary>
    internal static class RaidPurse
    {
        /// <summary>The Warlord's share: its quarter, or every coin left when nobody comes after it.</summary>
        public static int ForWarlord(RaidState state, bool lastOfRaid)
        {
            int unspent = Mathf.Max(0, state.CoinsUnspent);
            return Take(state, lastOfRaid ? unspent : Mathf.Min(Quarter(state), unspent));
        }

        /// <summary>A regular raider's share, with <paramref name="stillToCome"/> regular raiders of the raid left to
        /// arrive counting this one, and whether the Warlord is still to come.</summary>
        public static int ForRaider(RaidState state, int stillToCome, bool warlordToCome)
        {
            int unspent = Mathf.Max(0, state.CoinsUnspent);
            int pool = unspent - (warlordToCome ? Mathf.Min(Quarter(state), unspent) : 0);
            if (stillToCome <= 1)
            {
                return Take(state, pool); // the last regular raider: the rest, bar a Warlord's quarter still to come
            }
            return Take(state, Mathf.Clamp(Mathf.RoundToInt((float)pool / stillToCome), 0, pool));
        }

        private static int Quarter(RaidState state) =>
            Mathf.Max(0, Mathf.RoundToInt(state.CoinsBack * RaidTable.WarlordCoinShare));

        private static int Take(RaidState state, int coins)
        {
            if (coins > 0)
            {
                state.CoinsUnspent -= coins;
            }
            return coins;
        }
    }
}
