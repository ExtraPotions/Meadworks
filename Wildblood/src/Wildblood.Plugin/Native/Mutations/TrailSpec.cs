// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using ExtraPotions.Wildblood.Adopted.Rules;
using ExtraPotions.Wildblood.Adopted.Scaling;
using ExtraPotions.Wildblood.Adopted.Traits;
using UnityEngine;

namespace ExtraPotions.Wildblood.Adopted.Mutations
{
    /// <summary>
    /// One creature's ground trail in numbers, read from its rules once: how long a patch lasts, how wide it is, how
    /// far apart they fall, how much a patch slows a player and (ice only) how much grip it leaves them. The slow is
    /// the trail's gain, so a large star enhances it; life, radius, spacing and grip are its shape and never are. The
    /// same numbers on every machine, since every machine holds the same rules, so a patch slows a player exactly as
    /// much wherever it is drawn. <see cref="Describe"/> is the `wildblood inspect` line, read through the very same calls.
    /// </summary>
    internal readonly struct TrailSpec
    {
        /// <summary>
        /// The most patches one creature may have on the ground at once: a long life spaces drops out in time.
        /// </summary>
        public const int MaxLive = 40;

        /// <summary>The least time between two drops, however fast the creature runs.</summary>
        private const float MinGap = 0.25f;

        /// <summary>
        /// The most a patch may slow a player, however its slow is set or enhanced: never a standstill.
        /// </summary>
        private const float MaxSlow = 90f;

        public readonly float Life;
        public readonly float Radius;
        public readonly float Spacing;

        /// <summary>The share of a player's speed a patch takes away, 0 to 0.9.</summary>
        public readonly float Slow;

        /// <summary>
        /// The share of their normal grip a player keeps on the patch: 1 off ice, as little as 0.01 on it.
        /// </summary>
        public readonly float Grip;

        /// <summary>The vanilla effect whose ground decal the patches are drawn with.</summary>
        public readonly string Effect;

        /// <summary>Fire damage a second to a player standing in a patch: 0 off burning ground.</summary>
        public readonly float Burn;

        private TrailSpec(float life, float radius, float spacing, float slow, float grip, string effect, float burn)
        {
            Burn = burn;
            Life = life;
            Radius = radius;
            Spacing = spacing;
            Slow = slow;
            Grip = grip;
            Effect = effect;
        }

        /// <summary>False when `trail life` is 0: the creature lays no trail at all.</summary>
        public bool Lays => Life > 0f;

        /// <summary>
        /// Seconds between two drops at the least, so no creature has more than <see cref="MaxLive"/> patches.
        /// </summary>
        public float Gap => Mathf.Max(MinGap, Life / MaxLive);

        public static TrailSpec Of(BiomeRules r, CreatureTraits t, TrailKind kind)
        {
            Mutation m = kind.Mutation;
            float slow = Mathf.Clamp(Enhance.Magnitude(r, t, m, Fields.Slow), 0f, MaxSlow) / 100f;
            float grip = kind.Slips ? Mathf.Clamp(r.PowerOf(m, Fields.Grip), 1f, 100f) / 100f : 1f;
            float life = Mathf.Max(0f, r.PowerOf(m, Fields.TrailLife));
            float radius = Mathf.Clamp(r.PowerOf(m, Fields.PatchRadius), 0.25f, 8f);
            float spacing = Mathf.Max(0.25f, r.PowerOf(m, Fields.PatchSpacing));
            float burn = kind.Burns ? Mathf.Max(0f, Enhance.Magnitude(r, t, m, Fields.Damage)) : 0f;
            return new TrailSpec(life, radius, spacing, slow, grip, r.PrefabOf(m, Fields.TrailEffect), burn);
        }

        /// <summary>
        /// The trail part of a mutation's `wildblood inspect` line (Frostbound's shares its line with the aura).
        /// </summary>
        public static string Describe(BiomeRules r, CreatureTraits t, Mutation m)
        {
            TrailKind kind = TrailKind.Of(m) ?? TrailKind.Mud;
            TrailSpec spec = Of(r, t, kind);
            if (!spec.Lays)
            {
                return $"no {kind.Name} trail (trail life 0)";
            }
            string effect = kind.Slips
                ? $"players on it slide (grip {spec.Grip * 100f:0}%) and are {spec.Slow * 100f:0}% slower"
                : kind.Burns
                    ? $"players in it set on fire, {spec.Burn:0.0} fire damage a second"
                    : $"players in it {spec.Slow * 100f:0}% slower, for {kind.Linger:0.0}s after stepping out";
            return $"{kind.Name} patch r{spec.Radius:0.0}m every {spec.Spacing:0.0}m walked, lasting {spec.Life:0}s; "
                + $"{effect}; none when tamed; drawn from {spec.Effect}";
        }
    }
}
