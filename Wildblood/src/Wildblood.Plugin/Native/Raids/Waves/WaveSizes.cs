// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// Changed by expDARE, 2026: Wildblood namespace and authority-owned raid integration.
using System.Collections.Generic;
using UnityEngine;

namespace ExtraPotions.Wildblood.Adopted.Raids
{
    /// <summary>
    /// How many of each creature a wave brings (features/raids.md section 4.3): its game limit in the raid
    /// (<see cref="RaidCreatures.Limit"/>) times the wave's share (<see cref="RaidTable.WaveShares"/>: half, two thirds,
    /// all), times the band's raider count, plus a quarter more for each player past the first. The wave is rounded as a
    /// whole - its total is the exact sum rounded, at least one - and handed to the creatures by their largest fractions,
    /// so a creature with a small limit still comes in a big wave and a small wave is never empty. The last wave always
    /// brings the toughest creature (<see cref="RaidCreatures.Toughest"/>), one of which is its Warlord (section 4.4).
    /// </summary>
    internal static class WaveSizes
    {
        /// <summary>No wave brings more than this, however a modded raid sets its limits: the 20-alive cap keeps the fight
        /// sane, but a raid that cannot be fought through in its 15 minutes is no raid.</summary>
        private const int MostPerWave = 100;

        private static readonly List<int> Places = new List<int>();

        /// <summary>The plan for wave <paramref name="wave"/> of the event, for <paramref name="players"/> at the raid.</summary>
        public static WavePlan Plan(RandomEvent ev, int wave, RaidBand band, int players)
        {
            WavePlan plan = new WavePlan();
            RaidCreatures.Entries(ev, Places);
            if (Places.Count == 0)
            {
                return plan;
            }
            int[] counts = Counts(ev, wave, band, players);
            for (int i = 0; i < Places.Count; i++)
            {
                plan.Add(Places[i], counts[i]);
            }
            if (wave >= RaidTable.Waves)
            {
                plan.MakeWarlord(Places[RaidCreatures.Toughest(ev, Places)]);
            }
            plan.Later = Later(ev, wave, band, players);
            return plan;
        }

        // The regular raiders the waves after this one bring, at today's player count (the Warlord not counted).
        private static int Later(RandomEvent ev, int wave, RaidBand band, int players)
        {
            int later = 0;
            for (int next = wave + 1; next <= RaidTable.Waves; next++)
            {
                int[] counts = Counts(ev, next, band, players);
                for (int i = 0; i < counts.Length; i++)
                {
                    later += counts[i];
                }
                later -= next >= RaidTable.Waves ? 1 : 0;
            }
            return Mathf.Max(0, later);
        }

        // Each creature's count, in the order of Places.
        private static int[] Counts(RandomEvent ev, int wave, RaidBand band, int players)
        {
            float scale = Scale(wave, band, players);
            float[] exact = new float[Places.Count];
            float sum = 0f;
            for (int i = 0; i < Places.Count; i++)
            {
                exact[i] = RaidCreatures.Limit(ev.m_spawn[Places[i]]) * scale;
                sum += exact[i];
            }
            int total = Mathf.Clamp(Mathf.RoundToInt(sum), 1, MostPerWave);
            int[] counts = Share(exact, sum, total);
            int toughest = RaidCreatures.Toughest(ev, Places);
            if (wave >= RaidTable.Waves && counts[toughest] == 0)
            {
                counts[toughest] = 1; // the last wave is led by one of the toughest, however few of it the sums gave
            }
            return counts;
        }

        private static float Scale(int wave, RaidBand band, int players)
        {
            float share = RaidTable.WaveShares[Mathf.Clamp(wave, 1, RaidTable.WaveShares.Length) - 1];
            return share * band.RaiderFactor * (1f + RaidTable.ExtraPerPlayer * Mathf.Max(0, players - 1));
        }

        // Largest remainder: every creature its whole part of the total, then one more each to the largest fractions left.
        private static int[] Share(float[] exact, float sum, int total)
        {
            int[] counts = new int[exact.Length];
            int given = 0;
            for (int i = 0; i < exact.Length; i++)
            {
                exact[i] *= total / sum;
                counts[i] = Mathf.FloorToInt(exact[i]);
                given += counts[i];
            }
            for (; given < total; given++)
            {
                counts[LargestFraction(exact, counts)]++;
            }
            return counts;
        }

        private static int LargestFraction(float[] exact, int[] counts)
        {
            int best = 0;
            for (int i = 1; i < exact.Length; i++)
            {
                if (exact[i] - counts[i] > exact[best] - counts[best])
                {
                    best = i;
                }
            }
            return best;
        }
    }
}
