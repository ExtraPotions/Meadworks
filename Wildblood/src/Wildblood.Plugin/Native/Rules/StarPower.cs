// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using UnityEngine;

namespace ExtraPotions.Wildblood.Adopted.Rules
{
    /// <summary>
    /// The six per-star lines - growth, hp, attack, swing speed, speed, drops - each one entry per star count. A plain
    /// data holder: the parser fills the arrays, the scaling reads them. An index beyond an array's length reuses its
    /// last entry, so a creature above the tabulated ceiling keeps the top row's values.
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
            if (line == null || line.Length == 0)
            {
                return 1f;
            }
            int index = Mathf.Clamp(stars, 0, line.Length - 1);
            return line[index];
        }

        /// <summary>
        /// Lengthens every line shorter than <paramref name="reference"/>'s, continuing it by the reference's own steps:
        /// an Extreme world's 6-8 stars on a file written for five get the built-in increments on top of the file's own
        /// 5-star values (features/difficulty.md section 6). Longer lines are left alone.
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
            if (line == null || line.Length == 0 || line.Length >= reference.Length)
            {
                return line!;
            }
            float[] longer = new float[reference.Length];
            line.CopyTo(longer, 0);
            for (int i = line.Length; i < longer.Length; i++)
            {
                longer[i] = longer[i - 1] + (reference[i] - reference[i - 1]);
            }
            return longer;
        }

        public StarPower Clone()
        {
            return new StarPower
            {
                Growth = (float[])Growth.Clone(), Hp = (float[])Hp.Clone(), Attack = (float[])Attack.Clone(),
                SwingSpeed = (float[])SwingSpeed.Clone(), Speed = (float[])Speed.Clone(), Drops = (float[])Drops.Clone(),
            };
        }
    }
}
