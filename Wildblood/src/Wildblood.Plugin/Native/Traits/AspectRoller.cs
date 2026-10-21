// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using System.Collections.Generic;
using ExtraPotions.Wildblood.Adopted.Rules;
using UnityEngine;

namespace ExtraPotions.Wildblood.Adopted.Traits
{
    /// <summary>
    /// Draws one aspect for a boss from the live rules: every outcome in that boss's rotation, weighted by its chance,
    /// `none` included. An altar's shift passes the aspect it shows as <c>exclude</c>, so a shift is always a visible
    /// change - unless nothing else is left to draw, when the current one simply stays. A Bountiful draw also draws its
    /// extras, from the same rotation and weights, in the same moment, so the altar can show them and the boss carries
    /// exactly what was shown.
    /// </summary>
    public static class AspectRoller
    {
        /// <summary>Twin, Tethered and Phantom: each brings more bodies into the fight, and two of them break each other.</summary>
        private const int BodyMask = 1 << (int)Aspect.Twin | 1 << (int)Aspect.Tethered | 1 << (int)Aspect.Phantom;

        public static Aspect Roll(string bossPrefab, Aspect? exclude)
        {
            AspectRules rules = RuleState.Active.Boss.Aspects;
            if (!rules.Enabled)
            {
                return Aspect.None;
            }
            List<KeyValuePair<Aspect, float>> pool = Pool(rules, bossPrefab, exclude);
            return pool.Count == 0 ? exclude ?? Aspect.None : Pick(pool);
        }

        /// <summary>A boss's whole draw: its aspect as <see cref="Roll"/> draws it, and Bountiful's extras drawn with it.</summary>
        public static BossAspects RollBoss(string bossPrefab, Aspect? exclude)
        {
            Aspect aspect = Roll(bossPrefab, exclude);
            return new BossAspects(aspect, aspect == Aspect.Bountiful ? RollExtras(bossPrefab) : 0);
        }

        /// <summary>
        /// Bountiful's extras for this boss, one bit per aspect value: `extra aspects` distinct draws from its rotation by
        /// weight, never `none` or Bountiful itself, and never a second aspect that brings more bodies. Fewer when the pool
        /// runs out first.
        /// </summary>
        public static int RollExtras(string bossPrefab)
        {
            AspectRules rules = RuleState.Active.Boss.Aspects;
            List<KeyValuePair<Aspect, float>> pool = Pool(rules, bossPrefab, Aspect.None);
            pool.RemoveAll(entry => entry.Key == Aspect.Bountiful);
            int wanted = Mathf.RoundToInt(rules.PowerOf(Aspect.Bountiful, Fields.ExtraAspects));
            int mask = 0;
            for (int i = 0; i < wanted && pool.Count > 0; i++)
            {
                Aspect drawn = Pick(pool);
                mask |= 1 << (int)drawn;
                pool.RemoveAll(entry => entry.Key == drawn || (BringsBodies(drawn) && BringsBodies(entry.Key)));
            }
            return mask;
        }

        /// <summary>True for Twin, Tethered and Phantom: one boss carries at most one of them.</summary>
        public static bool BringsBodies(Aspect aspect) => (BodyMask & (1 << (int)aspect)) != 0;

        /// <summary>True when an extras mask already holds an aspect that brings more bodies.</summary>
        public static bool HoldsBodies(int mask) => (BodyMask & mask) != 0;

        private static List<KeyValuePair<Aspect, float>> Pool(AspectRules rules, string bossPrefab, Aspect? exclude)
        {
            List<KeyValuePair<Aspect, float>> pool = new List<KeyValuePair<Aspect, float>>();
            foreach (Aspect aspect in AspectCatalog.Outcomes)
            {
                float weight = rules.ChanceOf(aspect);
                if (aspect != exclude && weight > 0f && rules.InRotation(bossPrefab, aspect))
                {
                    pool.Add(new KeyValuePair<Aspect, float>(aspect, weight));
                }
            }
            return pool;
        }

        private static Aspect Pick(List<KeyValuePair<Aspect, float>> pool)
        {
            float total = 0f;
            foreach (KeyValuePair<Aspect, float> entry in pool)
            {
                total += entry.Value;
            }
            float draw = Random.Range(0f, total);
            foreach (KeyValuePair<Aspect, float> entry in pool)
            {
                draw -= entry.Value;
                if (draw < 0f)
                {
                    return entry.Key;
                }
            }
            return pool[pool.Count - 1].Key;
        }
    }
}
