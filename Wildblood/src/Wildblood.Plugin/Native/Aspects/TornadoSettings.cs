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
    /// Nightfall's tornado numbers, read live from the rules each time a wave is raised, so a rule-file edit retunes
    /// the next wave, and kept inside the range where the aspect still plays fair: a tornado always forms for at least
    /// <see cref="MinForm"/> seconds and hunts for at least <see cref="MinHunt"/> after it, one wave never comes while
    /// the last one's tornadoes still turn (so a player never has two of one boss's on them), and no funnel grows
    /// absurdly large.
    /// </summary>
    internal readonly struct TornadoSettings
    {
        /// <summary>The shortest a tornado takes to form, whatever the rule file says: it must be seen coming.</summary>
        public const float MinForm = 0.2f;

        /// <summary>The fewest seconds a formed tornado hunts before it breaks up.</summary>
        public const float MinHunt = 1f;

        private const float MaxLife = 120f;

        /// <summary>Seconds at least between waves (0 switches the tornadoes off); never shorter than `life`.</summary>
        public readonly float Every;

        /// <summary>Seconds at most between waves; the delay is drawn between the two each time.</summary>
        public readonly float EveryMax;

        /// <summary>Seconds a tornado whirls into existence, harmless, before it hunts.</summary>
        public readonly float Form;

        /// <summary>Seconds from a tornado's rising to its breaking up.</summary>
        public readonly float Life;

        public readonly TornadoShape Shape;

        /// <summary>The hunting speed, in percent of a player's run speed.</summary>
        public readonly float Speed;

        /// <summary>Damage a second to a player inside the funnel.</summary>
        public readonly float Damage;

        /// <summary>Players within this many metres of the boss, on the ground plane, each get a tornado.</summary>
        public readonly float Range;

        private TornadoSettings(float every, float everyMax, float form, float life, TornadoShape shape, float speed,
            float damage, float range)
        {
            Every = every;
            EveryMax = everyMax;
            Form = form;
            Life = life;
            Shape = shape;
            Speed = speed;
            Damage = damage;
            Range = range;
        }

        public static TornadoSettings Read()
        {
            float form = Mathf.Clamp(Power(Fields.FormTime), MinForm, 10f);
            float life = Mathf.Clamp(Power(Fields.Life), form + MinHunt, MaxLife);
            float every = Power(Fields.Every) > 0f ? Mathf.Max(Power(Fields.Every), life) : 0f;
            TornadoShape shape = new TornadoShape(Clamp(Fields.BaseWidth, 0.2f, 30f), Clamp(Fields.TopWidth, 0.2f, 50f),
                Clamp(Fields.Height, 2f, 60f));
            return new TornadoSettings(every, Mathf.Max(every, Power(Fields.EveryMax)), form, life, shape,
                Clamp(Fields.TornadoSpeed, 0f, 200f), Clamp(Fields.Damage, 0f, 1000f), Clamp(Fields.Range, 0f, 200f));
        }

        /// <summary>The delay before the next wave, drawn fresh each time.</summary>
        public float NextDelay() => Random.Range(Every, EveryMax);

        private static float Clamp(string field, float min, float max) => Mathf.Clamp(Power(field), min, max);

        private static float Power(string field) => AspectMath.Power(Aspect.Nightfall, field);
    }
}
