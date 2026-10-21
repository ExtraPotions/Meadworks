// Wildblood - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks).
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later, adapted from
// EliteCreaturesReborn/Rules/StarPower.cs and the star power defaults of Rules/RuleDefaults.cs at
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Changed by expDARE, 2026: Wildblood namespace; Unity-free; defaults split into StarPowerDefaults; StarMath added.
// SPDX-License-Identifier: GPL-3.0-or-later
using System;

namespace ExtraPotions.Wildblood.Stars
{
    /// <summary>
    /// The six per-star lines (design A6) - growth, hp, attack, swing speed, speed, drops - each one entry per star count.
    /// An index beyond a line's length reuses its last entry, so a creature above the tabulated ceiling keeps the top
    /// row's values. Star power replaces the game's own per-level growth (A7).
    /// </summary>
    public sealed class StarPower
    {
        public float[] Growth = { 0f };
        public float[] Hp = { 1f };
        public float[] Attack = { 1f };
        public float[] SwingSpeed = { 1f };
        public float[] Speed = { 1f };
        public float[] Drops = { 1f };

        public float GrowthAt(int stars) => At(Growth, stars);
        public float HpAt(int stars) => At(Hp, stars);
        public float AttackAt(int stars) => At(Attack, stars);
        public float SwingSpeedAt(int stars) => At(SwingSpeed, stars);
        public float SpeedAt(int stars) => At(Speed, stars);
        public float DropsAt(int stars) => At(Drops, stars);

        private static float At(float[] line, int stars)
        {
            if (line == null || line.Length == 0) return 1f;
            return line[Math.Min(line.Length - 1, Math.Max(0, stars))];
        }

        /// <summary>
        /// Lengthens every line shorter than <paramref name="reference"/>'s, continuing it by the reference's own steps:
        /// an Extreme world's 6-8 stars on a file written for five get the built-in increments on top of the file's own
        /// 5-star values. Longer lines are left alone.
        /// </summary>
        public void PadFrom(StarPower reference)
        {
            Growth = Pad(Growth, reference.Growth);
            Hp = Pad(Hp, reference.Hp);
            Attack = Pad(Attack, reference.Attack);
            SwingSpeed = Pad(SwingSpeed, reference.SwingSpeed);
            Speed = Pad(Speed, reference.Speed);
            Drops = Pad(Drops, reference.Drops);
        }

        private static float[] Pad(float[] line, float[] reference)
        {
            if (line == null || line.Length == 0 || line.Length >= reference.Length) return line!;
            float[] longer = new float[reference.Length];
            line.CopyTo(longer, 0);
            for (int i = line.Length; i < longer.Length; i++)
                longer[i] = longer[i - 1] + (reference[i] - reference[i - 1]);
            return longer;
        }

        public StarPower Clone() => new StarPower
        {
            Growth = (float[])Growth.Clone(), Hp = (float[])Hp.Clone(), Attack = (float[])Attack.Clone(),
            SwingSpeed = (float[])SwingSpeed.Clone(), Speed = (float[])Speed.Clone(), Drops = (float[])Drops.Clone(),
        };
    }

    /// <summary>EliteCreaturesReborn's built-in star power (wiki "What a star is worth") for creatures and bosses.</summary>
    public static class StarPowerDefaults
    {
        /// <summary>6-8 stars (Extreme only) continue each line by its 5th star's step; the speeds by +0.03.</summary>
        public static StarPower Creatures() => new StarPower
        {
            Growth = new[] { 0.06f, 0.10f, 0.15f, 0.20f, 0.25f, 0.30f, 0.35f, 0.40f, 0.45f },
            Hp = new[] { 1f, 1.4f, 1.95f, 2.6f, 3.3f, 4.0f, 4.7f, 5.4f, 6.1f },
            Attack = new[] { 1f, 1.15f, 1.3f, 1.5f, 1.75f, 2f, 2.25f, 2.5f, 2.75f },
            SwingSpeed = new[] { 1f, 1.02f, 1.05f, 1.08f, 1.12f, 1.16f, 1.19f, 1.22f, 1.25f },
            Speed = new[] { 1f, 1f, 1.03f, 1.06f, 1.1f, 1.15f, 1.18f, 1.21f, 1.24f },
            Drops = new[] { 1f, 1f, 1.5f, 2f, 2.5f, 3f, 3.5f, 4f, 4.5f },
        };

        /// <summary>Boss star table (design D3): stars add health, damage and size; no drops line (stars pay in trophies).</summary>
        public static StarPower Bosses() => new StarPower
        {
            Growth = new[] { 0f, 0.05f, 0.10f, 0.15f, 0.20f, 0.20f },
            Hp = new[] { 1f, 1.5f, 2.25f, 3.4f, 5.1f, 7.6f },
            Attack = new[] { 1f, 1.25f, 1.55f, 1.9f, 2.3f, 2.75f },
            SwingSpeed = new[] { 1f },
            Speed = new[] { 1f },
        };

        /// <summary>Boss star chances [90, 6, 3, 1] (design D3).</summary>
        public static float[] BossStarChances() => new[] { 90f, 6f, 3f, 1f };
    }

    /// <summary>Stars are the vanilla level minus one (design A1); star power replaces vanilla growth (A7).</summary>
    public static class StarMath
    {
        public static int StarsOfLevel(int level) => Math.Max(0, level - 1);

        public static int LevelOfStars(int stars) => Math.Max(0, stars) + 1;

        /// <summary>Max health: base health times the hp line (the game's health-times-level growth is not applied).</summary>
        public static float MaxHealth(float baseHealth, StarPower power, int stars) => baseHealth * power.HpAt(stars);
    }
}
