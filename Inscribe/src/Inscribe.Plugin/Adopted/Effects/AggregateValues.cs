// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.11.0 at a0e33b9e74be2141e6ef38909d1d77de482cd7ac.
// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
using System;
using ExtraPotions.Inscribe.Adopted.Rules;

namespace ExtraPotions.Inscribe.Adopted.Effects
{
    /// <summary>
    /// One complete set of player-global totals, already capped, in the units the game wants: a fraction for a
    /// percent affix (12% → 0.12), the raw number for a flat one. The rebuild fills one, which the aggregate status
    /// effect reads. Local player only. Reused across rebuilds, so a rebuild allocates nothing.
    /// </summary>
    internal sealed partial class AggregateValues
    {
        /// <summary>Skill types map into a compact array: 0-31 as is, 100-131 at 32-63, <c>All</c> kept apart.</summary>
        private const int SkillSlots = 64;

        private static readonly int KindCount = Enum.GetValues(typeof(EffectKind)).Length;

        private readonly float[] _byKind = new float[KindCount];

        public readonly float[] SkillLevel = new float[SkillSlots];
        public readonly float[] SkillGain = new float[SkillSlots];
        public float SkillLevelAll;
        public float SkillGainAll;

        /// <summary>Per damage type (<see cref="DamageSlots"/> order): the fraction added to that part of every hit.</summary>
        public readonly float[] DamageDealt = new float[DamageSlots.Count];

        /// <summary>Whether any damage-dealt part is set, so <c>ModifyAttack</c> can skip its arithmetic.</summary>
        public bool AnyDamage;

        /// <summary>The total of a simple (single-number) kind.</summary>
        public float this[EffectKind kind] => _byKind[(int)kind];

        public void Clear()
        {
            Array.Clear(_byKind, 0, _byKind.Length);
            Array.Clear(SkillLevel, 0, SkillLevel.Length);
            Array.Clear(SkillGain, 0, SkillGain.Length);
            Array.Clear(DamageDealt, 0, DamageDealt.Length);
            SkillLevelAll = SkillGainAll = 0f;
            AnyDamage = false;
            ClearParams();
        }

        /// <summary>Folds one capped channel total in. <paramref name="amount"/> is already a fraction or a flat number.</summary>
        public void Add(EffectKind kind, ChannelDef channel, float amount)
        {
            switch (kind)
            {
                case EffectKind.SkillLevel:
                    AddSkills(SkillLevel, ref SkillLevelAll, channel, amount);
                    break;
                case EffectKind.SkillGain:
                    AddSkills(SkillGain, ref SkillGainAll, channel, amount);
                    break;
                case EffectKind.DamageDealt:
                    AddDamage(channel, amount);
                    break;
                default:
                    if (AddParam(kind, channel, amount))
                    {
                        break;
                    }
                    _byKind[(int)kind] += amount;
                    break;
            }
        }

        public float SkillLevelFor(Skills.SkillType skill)
        {
            int i = SkillIndex(skill);
            return i < 0 ? SkillLevelAll : SkillLevelAll + SkillLevel[i];
        }

        public float SkillGainFor(Skills.SkillType skill)
        {
            int i = SkillIndex(skill);
            return i < 0 ? SkillGainAll : SkillGainAll + SkillGain[i];
        }

        private static int SkillIndex(Skills.SkillType skill)
        {
            int v = (int)skill;
            if (v >= 0 && v < 32)
            {
                return v;
            }
            return v >= 100 && v < 132 ? v - 68 : -1;
        }

        // A param naming several skills raises each of them by the full amount; All raises every skill.
        private static void AddSkills(float[] table, ref float all, ChannelDef channel, float amount)
        {
            foreach (Skills.SkillType skill in channel.Sample.ParamSkills)
            {
                int i = SkillIndex(skill);
                if (skill == Skills.SkillType.All)
                {
                    all += amount;
                }
                else if (i >= 0)
                {
                    table[i] += amount;
                }
            }
        }

        // A group param (physical, elemental, all) raises each member type by the full amount (affixes.md: "the
        // blunt, slash and pierce parts of your hits are +X%").
        private void AddDamage(ChannelDef channel, float fraction)
        {
            DamageMask mask = channel.Sample.ParamDamage;
            for (int i = 0; i < DamageSlots.Count; i++)
            {
                if ((mask & DamageSlots.Masks[i]) != 0)
                {
                    DamageDealt[i] += fraction;
                    AnyDamage = true;
                }
            }
        }
    }
}
