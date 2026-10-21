// Wildblood - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks).
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later, adapted from
// EliteCreaturesReborn/Traits/AspectRoller.cs, Traits/BossAspects.cs and the count rule of Loot/BossTrophies.cs at
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Changed by expDARE, 2026: Wildblood namespace; rules and dice passed in (no global state, Unity-free); the trophy count
// as a pure function of players near, stars, aspect and the Phantom copy flag.
// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;

namespace ExtraPotions.Wildblood.Bosses
{
    /// <summary>A boss's whole draw: its aspect and, for Bountiful, the extras drawn in the same moment (one bit per aspect).</summary>
    public readonly struct BossAspects
    {
        public BossAspects(Aspect aspect, int extras)
        {
            Aspect = aspect;
            Extras = extras;
        }

        public Aspect Aspect { get; }
        public int Extras { get; }
    }

    /// <summary>
    /// Draws one aspect for a boss: every outcome in that boss's rotation, weighted by its chance, None included. An altar's
    /// shift passes the aspect it shows as <c>exclude</c>, so a shift is always a visible change unless nothing else is left.
    /// </summary>
    public static class AspectRoller
    {
        private const int BodyMask = 1 << (int)Aspect.Twin | 1 << (int)Aspect.Tethered | 1 << (int)Aspect.Phantom;

        public static Aspect Roll(AspectRules rules, string bossPrefab, Aspect? exclude, IDice dice)
        {
            if (rules == null) throw new ArgumentNullException(nameof(rules));
            if (!rules.Enabled) return Aspect.None;
            var pool = Pool(rules, bossPrefab, exclude);
            return pool.Count == 0 ? exclude ?? Aspect.None : Pick(pool, dice);
        }

        public static BossAspects RollBoss(AspectRules rules, string bossPrefab, Aspect? exclude, IDice dice)
        {
            var aspect = Roll(rules, bossPrefab, exclude, dice);
            return new BossAspects(aspect, aspect == Aspect.Bountiful ? RollExtras(rules, bossPrefab, dice) : 0);
        }

        /// <summary>
        /// Bountiful's extras: distinct draws from the boss's rotation by weight, never None or Bountiful, and never a second
        /// aspect that brings more bodies. Fewer when the pool runs out first.
        /// </summary>
        public static int RollExtras(AspectRules rules, string bossPrefab, IDice dice)
        {
            var pool = Pool(rules, bossPrefab, Aspect.None);
            pool.RemoveAll(entry => entry.Key == Aspect.Bountiful);
            var mask = 0;
            for (var i = 0; i < rules.BountifulExtras && pool.Count > 0; i++)
            {
                var drawn = Pick(pool, dice);
                mask |= Aspects.Bit(drawn);
                pool.RemoveAll(entry => entry.Key == drawn || (BringsBodies(drawn) && BringsBodies(entry.Key)));
            }
            return mask;
        }

        /// <summary>Twin, Tethered and Phantom: each brings more bodies into the fight; one boss carries at most one.</summary>
        public static bool BringsBodies(Aspect aspect) => (BodyMask & Aspects.Bit(aspect)) != 0;

        private static List<KeyValuePair<Aspect, float>> Pool(AspectRules rules, string bossPrefab, Aspect? exclude)
        {
            var pool = new List<KeyValuePair<Aspect, float>>();
            foreach (var aspect in Aspects.Outcomes)
            {
                var weight = rules.ChanceOf(aspect);
                if (aspect != exclude && weight > 0f && rules.InRotation(bossPrefab, aspect))
                    pool.Add(new KeyValuePair<Aspect, float>(aspect, weight));
            }
            return pool;
        }

        private static Aspect Pick(List<KeyValuePair<Aspect, float>> pool, IDice dice)
        {
            var weights = new float[pool.Count];
            for (var i = 0; i < pool.Count; i++) weights[i] = pool[i].Value;
            return pool[dice.PickWeighted(weights)].Key;
        }
    }

    /// <summary>
    /// A boss's own trophies (design D4): one per player within 100 m plus stars plus one, in every loot mode; Bountiful
    /// alone scales the count (times its loot); a Phantom copy pays none. Capped at the game's per-row 100.
    /// </summary>
    public static class BossTrophies
    {
        public const float PlayerRange = 100f;
        public const int AmountCap = 100;

        public static int Count(int playersNear, int stars, Aspect aspect, AspectRules rules, bool phantomCopy)
        {
            if (phantomCopy) return 0;
            float heads = Math.Max(0, playersNear) + Math.Max(0, stars) + 1;
            if (aspect == Aspect.Bountiful) heads *= rules.LootOf(Aspect.Bountiful);
            return Math.Min(AmountCap, Math.Max(1, (int)Math.Round(heads, MidpointRounding.AwayFromZero)));
        }
    }
}
