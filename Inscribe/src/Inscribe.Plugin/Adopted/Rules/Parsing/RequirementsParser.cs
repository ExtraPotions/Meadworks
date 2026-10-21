// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
using System.Collections.Generic;
using ExtraPotions.Inscribe.Adopted.Core;
using ExtraPotions.Inscribe.Adopted.Items;

namespace ExtraPotions.Inscribe.Adopted.Rules
{
    /// <summary>Reads an affix's <c>requires</c> block: <c>skill</c> (any of), <c>hands</c>, <c>traits</c> (all of).</summary>
    internal static class RequirementsParser
    {
        public static AffixRequirements Parse(MapReader parent)
        {
            MapReader? sub = parent.Sub("requires");
            if (sub == null)
            {
                return AffixRequirements.Any;
            }
            MapReader r = sub.Value;
            r.Unknown("skill", "hands", "traits");
            return new AffixRequirements
            {
                Skills = ReadSkills(r),
                Hands = ReadHands(r),
                Traits = ReadTraits(r),
            };
        }

        private static List<Skills.SkillType> ReadSkills(MapReader r)
        {
            List<Skills.SkillType> skills = new List<Skills.SkillType>();
            foreach (string name in r.Strings("skill") ?? new List<string>())
            {
                if (ParamKinds.TrySkill(name, out Skills.SkillType skill) && skill != Skills.SkillType.All)
                {
                    skills.Add(skill);
                }
                else
                {
                    r.Error("skill", $"'{name}' is not a skill name");
                }
            }
            return skills;
        }

        private static Hands ReadHands(MapReader r)
        {
            Hands hands = r.Enum("hands", Hands.None);
            if (r.Has("hands") && hands == Hands.None)
            {
                r.Error("hands", "should be one or two");
            }
            return hands;
        }

        private static ItemTraits ReadTraits(MapReader r)
        {
            ItemTraits traits = ItemTraits.None;
            foreach (string name in r.Strings("traits") ?? new List<string>())
            {
                if (EnumIds<ItemTraits>.TryParse(name, out ItemTraits trait) && trait != ItemTraits.None)
                {
                    traits |= trait;
                }
                else
                {
                    r.Error("traits", $"'{name}' is not a trait (wears_out, movement_penalty, builds, projectile, ammo, can_parry)");
                }
            }
            return traits;
        }
    }
}
