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
    /// One Gravitic cycle's numbers: read from the rules on the boss's owner as it roars and carried in the roar itself,
    /// so every client pulls and slams by the same numbers, even while a rule-file reload is still on its way to them.
    /// Clamped here, once, so a mistyped rule file gives a tame cycle rather than a broken one.
    /// </summary>
    internal readonly struct GraviticCall
    {
        /// <summary>The longest pull a rule file may ask for, in seconds.</summary>
        private const float MaxPullTime = 10f;

        public readonly float Range;
        public readonly float PullTime;
        public readonly float PullSpeed;
        public readonly float SlamRadius;
        public readonly float SlamDamage;

        public GraviticCall(float range, float pullTime, float pullSpeed, float slamRadius, float slamDamage)
        {
            Range = Mathf.Max(0f, range);
            PullTime = Mathf.Clamp(pullTime, 0.1f, MaxPullTime);
            PullSpeed = Mathf.Max(0f, pullSpeed);
            SlamRadius = Mathf.Max(0f, slamRadius);
            SlamDamage = Mathf.Clamp(slamDamage, 0f, 100f);
        }

        public static GraviticCall FromRules() => new GraviticCall(Power(Fields.Range), Power(Fields.PullTime),
            Power(Fields.PullSpeed), Power(Fields.SlamRadius), Power(Fields.SlamDamage));

        /// <summary>Seconds between roars: `every`, but never so short that a roar comes before the last slam.</summary>
        public static float Every(GraviticCall call) => Mathf.Max(Power(Fields.Every), call.PullTime + 1f);

        public void Write(ZPackage pkg)
        {
            pkg.Write(Range);
            pkg.Write(PullTime);
            pkg.Write(PullSpeed);
            pkg.Write(SlamRadius);
            pkg.Write(SlamDamage);
        }

        public static GraviticCall Read(ZPackage pkg) =>
            new GraviticCall(pkg.ReadSingle(), pkg.ReadSingle(), pkg.ReadSingle(), pkg.ReadSingle(), pkg.ReadSingle());

        private static float Power(string field) => AspectMath.Power(Aspect.Gravitic, field);
    }
}
