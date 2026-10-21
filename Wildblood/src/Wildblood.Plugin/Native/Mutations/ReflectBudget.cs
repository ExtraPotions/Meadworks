// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using System.Collections.Generic;
using UnityEngine;

namespace ExtraPotions.Wildblood.Adopted.Mutations
{
    /// <summary>
    /// What Warding has already sent back to each attacker in the last second, on this machine. The <c>max reflect</c>
    /// ceiling covers all of those reflects together rather than each one alone, so a bow volley that lands a dozen
    /// split, chained and piercing arrows, an explosion that catches three Warding creatures at once, or a mod that
    /// deals one hit as many, all draw on the one budget a single sword swing uses. The window slides: in no second, from
    /// any start, does one attacker get back more than the ceiling. Reflects are worked out on the creature's owner, so
    /// Warding creatures owned by different players keep separate books; around one fight that is nearly always one
    /// machine.
    /// </summary>
    internal static class ReflectBudget
    {
        private const float Window = 1f;

        /// <summary>Past this many attackers on the books, the ones with nothing left in the window are dropped.</summary>
        private const int SweepAt = 64;

        private static readonly Dictionary<ZDOID, Queue<Spend>> Books = new Dictionary<ZDOID, Queue<Spend>>();

        private readonly struct Spend
        {
            public readonly float Time;
            public readonly float Amount;

            public Spend(float time, float amount)
            {
                Time = time;
                Amount = amount;
            }
        }

        /// <summary>As much of <paramref name="amount"/> as the ceiling still allows this attacker, booked as spent.</summary>
        public static float Take(ZDOID attacker, float amount, float ceiling)
        {
            float now = Time.time;
            Queue<Spend> spends = Recent(attacker, now);
            float granted = Mathf.Min(amount, Mathf.Max(0f, ceiling - Sum(spends)));
            if (granted > 0f)
            {
                spends.Enqueue(new Spend(now, granted));
            }
            return granted;
        }

        private static Queue<Spend> Recent(ZDOID attacker, float now)
        {
            if (Books.Count >= SweepAt)
            {
                Sweep(now);
            }
            if (!Books.TryGetValue(attacker, out Queue<Spend> spends))
            {
                spends = new Queue<Spend>();
                Books[attacker] = spends;
            }
            Prune(spends, now);
            return spends;
        }

        private static void Prune(Queue<Spend> spends, float now)
        {
            while (spends.Count > 0 && now - spends.Peek().Time >= Window)
            {
                spends.Dequeue();
            }
        }

        private static float Sum(Queue<Spend> spends)
        {
            float total = 0f;
            foreach (Spend spend in spends)
            {
                total += spend.Amount;
            }
            return total;
        }

        private static void Sweep(float now)
        {
            List<ZDOID> idle = new List<ZDOID>();
            foreach (KeyValuePair<ZDOID, Queue<Spend>> entry in Books)
            {
                Prune(entry.Value, now);
                if (entry.Value.Count == 0)
                {
                    idle.Add(entry.Key);
                }
            }
            foreach (ZDOID id in idle)
            {
                Books.Remove(id);
            }
        }
    }
}
