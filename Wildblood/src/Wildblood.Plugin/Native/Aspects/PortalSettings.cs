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
    /// Portalbound's numbers, read live from the rules at each wind-up, so a rule-file edit retunes the next throw, and
    /// kept inside sane bounds: a portal never hangs absurdly high, never demands a clearance no spot could give, and
    /// never opens beyond <see cref="MaxRange"/> of its target.
    /// </summary>
    internal readonly struct PortalSettings
    {
        /// <summary>The farthest from its target a far portal may open, whatever the rule file says.</summary>
        public const float MaxRange = 60f;

        /// <summary>The fewest metres the far portal hangs above whatever is under it.</summary>
        public readonly float MinHeight;

        /// <summary>The fewest metres between the far portal's centre and any solid thing or creature.</summary>
        public readonly float Clearance;

        /// <summary>The farthest the far portal opens from its target, in metres.</summary>
        public readonly float Range;

        private PortalSettings(float minHeight, float clearance, float range)
        {
            MinHeight = minHeight;
            Clearance = clearance;
            Range = range;
        }

        public static PortalSettings Read() => new PortalSettings(Mathf.Clamp(Power(Fields.MinHeight), 0f, 30f),
            Mathf.Clamp(Power(Fields.Clearance), 0f, 10f), Mathf.Clamp(Power(Fields.Range), 0f, MaxRange));

        private static float Power(string field) => AspectMath.Power(Aspect.Portalbound, field);
    }
}
