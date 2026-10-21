// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
using ExtraPotions.Inscribe.Adopted.Rules;

namespace ExtraPotions.Inscribe.Adopted.Effects
{
    /// <summary>The eight damage types an affix param can name, in a fixed order shared by every table here.</summary>
    internal static class DamageSlots
    {
        public const int Count = 8;
        public const int Blunt = 0, Slash = 1, Pierce = 2, Fire = 3, Frost = 4, Lightning = 5, Poison = 6, Spirit = 7;

        public static readonly DamageMask[] Masks =
        {
            DamageMask.Blunt, DamageMask.Slash, DamageMask.Pierce, DamageMask.Fire,
            DamageMask.Frost, DamageMask.Lightning, DamageMask.Poison, DamageMask.Spirit,
        };

        /// <summary>How many of the eight types the mask names (a brand on a group splits its share among them).</summary>
        public static int CountIn(DamageMask mask)
        {
            int n = 0;
            for (int i = 0; i < Count; i++)
            {
                if ((mask & Masks[i]) != 0)
                {
                    n++;
                }
            }
            return n;
        }

        /// <summary>Scales each named type of <paramref name="d"/> by <c>1 + fractions[type]</c>; chop, pickaxe and generic untouched.</summary>
        public static void Scale(ref HitData.DamageTypes d, float[] fractions)
        {
            d.m_blunt *= 1f + fractions[Blunt];
            d.m_slash *= 1f + fractions[Slash];
            d.m_pierce *= 1f + fractions[Pierce];
            d.m_fire *= 1f + fractions[Fire];
            d.m_frost *= 1f + fractions[Frost];
            d.m_lightning *= 1f + fractions[Lightning];
            d.m_poison *= 1f + fractions[Poison];
            d.m_spirit *= 1f + fractions[Spirit];
        }

        /// <summary>Lowers each named type of <paramref name="d"/> by <c>fractions[type]</c> (never below 0); chop and pickaxe untouched.</summary>
        public static void Reduce(ref HitData.DamageTypes d, float[] fractions)
        {
            d.m_blunt *= Keep(fractions[Blunt]);
            d.m_slash *= Keep(fractions[Slash]);
            d.m_pierce *= Keep(fractions[Pierce]);
            d.m_fire *= Keep(fractions[Fire]);
            d.m_frost *= Keep(fractions[Frost]);
            d.m_lightning *= Keep(fractions[Lightning]);
            d.m_poison *= Keep(fractions[Poison]);
            d.m_spirit *= Keep(fractions[Spirit]);
        }

        private static float Keep(float reduction) => reduction >= 1f ? 0f : 1f - reduction;

        /// <summary>The combat part of a damage block: every type except chop and pickaxe (tool damage) and the non-player bonus.</summary>
        public static float Combat(in HitData.DamageTypes d) =>
            d.m_damage + d.m_blunt + d.m_slash + d.m_pierce + d.m_fire + d.m_frost + d.m_lightning + d.m_poison + d.m_spirit;

        /// <summary>Adds <paramref name="amount"/> to the type at slot <paramref name="i"/>.</summary>
        public static void AddTo(ref HitData.DamageTypes d, int i, float amount)
        {
            switch (i)
            {
                case Blunt: d.m_blunt += amount; break;
                case Slash: d.m_slash += amount; break;
                case Pierce: d.m_pierce += amount; break;
                case Fire: d.m_fire += amount; break;
                case Frost: d.m_frost += amount; break;
                case Lightning: d.m_lightning += amount; break;
                case Poison: d.m_poison += amount; break;
                default: d.m_spirit += amount; break;
            }
        }
    }
}
