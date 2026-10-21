// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// Changed by expDARE, 2026: Wildblood namespace and authority-owned raid integration.
using UnityEngine;

namespace ExtraPotions.Wildblood.Adopted.Raids
{
    /// <summary>
    /// One row of the heat table (features/raids.md section 3): how hard a raid of that heat is and what it pays. A plain
    /// value; the rows themselves are in <see cref="RaidTable"/>. The heat fixed at the start picks the row, and that row
    /// sets the raiders' number, stars and mutations, the coins they carry back and the loot they drop.
    /// </summary>
    public sealed class RaidBand
    {
        public RaidBand(int index, string name, float fromHeat, float toHeat, float raiders, int extraStars,
            float mutated, float coinsBack, float dropsLow, float dropsHigh)
        {
            Index = index;
            Name = name;
            FromHeat = fromHeat;
            ToHeat = toHeat;
            RaiderFactor = raiders;
            ExtraStars = extraStars;
            MutatedPercent = mutated;
            CoinsBack = coinsBack;
            DropsLow = dropsLow;
            DropsHigh = dropsHigh;
        }

        /// <summary>The row's place, 0 (Trivial) to 4 (Deadly); what the host's ZDO stores.</summary>
        public int Index { get; }

        /// <summary>What players read: "Brutal".</summary>
        public string Name { get; }

        /// <summary>The lowest heat in the row.</summary>
        public float FromHeat { get; }

        /// <summary>The heat where the next row starts; infinity for the last.</summary>
        public float ToHeat { get; }

        /// <summary>Times the wave sizes: 1 is the game's own count, 2 twice as many.</summary>
        public float RaiderFactor { get; }

        /// <summary>Stars added to every raider's own roll (the Warlord adds <see cref="RaidTable.WarlordExtraStars"/> more).</summary>
        public int ExtraStars { get; }

        /// <summary>Percent chance that a raider which rolled no mutation is given one; 0 leaves it to the biome's roll.</summary>
        public float MutatedPercent { get; }

        /// <summary>Times the stake: every coin the raiders carry between them.</summary>
        public float CoinsBack { get; }

        /// <summary>The raiders' loot multiplier at the row's lowest heat.</summary>
        public float DropsLow { get; }

        /// <summary>The raiders' loot multiplier at the row's highest heat (the same as <see cref="DropsLow"/> but in Trivial).</summary>
        public float DropsHigh { get; }

        /// <summary>The loot multiplier at this heat: rising through the row where the row gives a range (Trivial's 0.1 to 0.5).</summary>
        public float DropsAt(float heat)
        {
            if (float.IsInfinity(ToHeat) || ToHeat <= FromHeat)
            {
                return DropsHigh;
            }
            return Mathf.Lerp(DropsLow, DropsHigh, Mathf.InverseLerp(FromHeat, ToHeat, heat));
        }

        /// <summary>Every coin the raiders carry for this stake.</summary>
        public int CoinsFor(int stake) => (int)System.Math.Min(int.MaxValue, System.Math.Max(0, System.Math.Round((double)stake * CoinsBack, System.MidpointRounding.ToEven)));
    }
}
