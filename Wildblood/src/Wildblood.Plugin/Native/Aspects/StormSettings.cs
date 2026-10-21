// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using ExtraPotions.Wildblood.Adopted.Rules;
using ExtraPotions.Wildblood.Adopted.Scaling;
using ExtraPotions.Wildblood.Adopted.Traits;
using UnityEngine;

namespace ExtraPotions.Wildblood.Adopted.Aspects
{
    /// <summary>
    /// Stormbound's numbers, read live from the rules each time they are needed, so a rule-file edit retunes a fight in
    /// progress, and kept inside the range where the aspect still plays fair: a circle always shows for at least
    /// <see cref="MinTell"/> seconds, one storm never overlaps the next, and a strike never takes more than the whole
    /// of a player's health.
    /// </summary>
    internal readonly struct StormSettings
    {
        /// <summary>The shortest warning a circle gives, whatever the rule file says.</summary>
        public const float MinTell = 0.5f;

        /// <summary>Seconds between storms (0 switches the aspect off); always longer than the tell.</summary>
        public readonly float Every;

        /// <summary>Seconds a circle shows before the lightning falls.</summary>
        public readonly float Tell;

        /// <summary>The circle's radius in metres.</summary>
        public readonly float Radius;

        /// <summary>The strike's damage, in percent of the struck player's maximum health.</summary>
        public readonly float Damage;

        /// <summary>Players within this many metres of the boss, on the ground plane, each get a circle.</summary>
        public readonly float Range;

        private StormSettings(float every, float tell, float radius, float damage, float range)
        {
            Every = every;
            Tell = tell;
            Radius = radius;
            Damage = damage;
            Range = range;
        }

        public static StormSettings Read()
        {
            float tell = Mathf.Max(MinTell, Power(Fields.TellTime));
            float every = Power(Fields.Every);
            return new StormSettings(every > 0f ? Mathf.Max(every, tell + 1f) : 0f, tell,
                Mathf.Max(0.5f, Power(Fields.Radius)), Mathf.Clamp(Power(Fields.Damage), 0f, 100f),
                Mathf.Max(0f, Power(Fields.Range)));
        }

        private static float Power(string field) => AspectMath.Power(Aspect.Stormbound, field);
    }
}
