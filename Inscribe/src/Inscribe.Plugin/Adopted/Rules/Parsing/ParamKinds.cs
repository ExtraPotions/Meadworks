// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
using System;
using System.Collections.Generic;
using ExtraPotions.Inscribe.Adopted.Core;
using ExtraPotions.Inscribe.Adopted.Effects;

namespace ExtraPotions.Inscribe.Adopted.Rules
{
    /// <summary>
    /// Parses and validates an affix's <c>param</c> against its effect's param kind (affixes.md section 3): skill
    /// names (one, a comma list, or <c>All</c>), damage types and their groups, elements, creature families,
    /// resources and aura kinds.
    /// </summary>
    internal static class ParamKinds
    {
        private static readonly Dictionary<string, DamageMask> DamageIds = new Dictionary<string, DamageMask>
        {
            ["blunt"] = DamageMask.Blunt, ["slash"] = DamageMask.Slash, ["pierce"] = DamageMask.Pierce,
            ["fire"] = DamageMask.Fire, ["frost"] = DamageMask.Frost, ["lightning"] = DamageMask.Lightning,
            ["poison"] = DamageMask.Poison, ["spirit"] = DamageMask.Spirit, ["physical"] = DamageMask.Physical,
            ["elemental"] = DamageMask.Elemental, ["all"] = DamageMask.All,
        };

        private static readonly string[] Elements = { "fire", "frost", "lightning", "poison" };
        private static readonly string[] Families = { "undead", "beasts", "sea", "boss" };
        private static readonly string[] Resources = { "health", "stamina", "eitr" };
        private static readonly string[] Auras = { "damage_dealt", "damage_taken" };

        /// <summary>Fills the parsed param fields of <paramref name="def"/>; returns an error text or null.</summary>
        public static string? Apply(AffixDef def, EffectDef effect)
        {
            if (effect.Param == EffectParamKind.None)
            {
                return def.Param == null ? null : $"effect '{effect.Id}' takes no param";
            }
            if (string.IsNullOrEmpty(def.Param))
            {
                return $"effect '{effect.Id}' needs a param ({EnumIds<EffectParamKind>.Id(effect.Param)})";
            }
            return ApplyKind(def, effect.Param, def.Param!);
        }

        private static string? ApplyKind(AffixDef def, EffectParamKind kind, string param)
        {
            switch (kind)
            {
                case EffectParamKind.Skill: return ApplySkills(def, param);
                case EffectParamKind.DamageType: return ApplyDamage(def, param, allowGroups: true);
                case EffectParamKind.Element: return ApplyDamage(def, param, allowGroups: false);
                case EffectParamKind.CreatureFamily: return OneOf(param, Families, "creature family");
                case EffectParamKind.Resource: return OneOf(param, Resources, "resource");
                case EffectParamKind.Aura: return OneOf(param, Auras, "aura");
                default: return null;
            }
        }

        private static string? ApplySkills(AffixDef def, string param)
        {
            List<Skills.SkillType> skills = new List<Skills.SkillType>();
            foreach (string part in param.Split(','))
            {
                if (!TrySkill(part.Trim(), out Skills.SkillType skill))
                {
                    return $"'{part.Trim()}' is not a skill name (the game's names: Swords, Axes, WoodCutting, Run, ..., or All)";
                }
                skills.Add(skill);
            }
            def.ParamSkills = skills;
            return null;
        }

        private static string? ApplyDamage(AffixDef def, string param, bool allowGroups)
        {
            bool known = DamageIds.TryGetValue(param, out DamageMask mask);
            if (!known || (!allowGroups && Array.IndexOf(Elements, param) < 0))
            {
                return $"'{param}' is not one of: {(allowGroups ? string.Join(", ", DamageIds.Keys) : string.Join(", ", Elements))}";
            }
            def.ParamDamage = mask;
            return null;
        }

        private static string? OneOf(string param, string[] allowed, string what) =>
            Array.IndexOf(allowed, param) >= 0 ? null : $"'{param}' is not a {what} ({string.Join(", ", allowed)})";

        /// <summary>A game skill name, exactly as the game spells it; <c>None</c> and numbers are refused.</summary>
        public static bool TrySkill(string name, out Skills.SkillType skill)
        {
            skill = Skills.SkillType.None;
            if (name.Length == 0 || !char.IsLetter(name[0]))
            {
                return false;
            }
            return Enum.TryParse(name, false, out skill) && skill != Skills.SkillType.None
                && Enum.IsDefined(typeof(Skills.SkillType), skill);
        }
    }
}
