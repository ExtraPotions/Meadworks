// Saga - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: the localization keys Saga uses (tokens meadworks_saga_<key>, design section 12).
using System;
using System.Collections.Generic;
using System.Linq;

namespace ExtraPotions.Saga.Localization
{
    public readonly struct TextKey
    {
        public TextKey(string key, int arguments)
        {
            Key = key;
            Arguments = arguments;
        }

        public string Key { get; }

        /// <summary>Number of <c>{0}</c>-style placeholders, numbered from 0.</summary>
        public int Arguments { get; }

        public override string ToString() => Key;
    }

    /// <summary>Every key of assets/localization/*.json; the tests hold the catalog to this table.</summary>
    public static class SagaTextKeys
    {
        public const string OffNoCrucible = "off_no_crucible";
        public const string OffNoSaga = "off_no_saga";

        /// <summary>Keys with fixed names.</summary>
        internal static readonly TextKey[] Fixed =
        {
            new TextKey(OffNoCrucible, 0),
            new TextKey(OffNoSaga, 0),
            new TextKey("power_recharging", 1),
            new TextKey("power_altar_locked", 1),
            new TextKey("skill_recovery_rate", 1),
            new TextKey("dodging_last_roll", 2), new TextKey("dodging_heavy_cost", 2),
            new TextKey("sneak_soft_steps", 1), new TextKey("sneak_assassin", 1),
            new TextKey("dodging_long_roll", 1), new TextKey("dodging_counter", 0), new TextKey("dodging_counter_locked", 0),
            new TextKey("dodging_heavy", 0), new TextKey("dodging_heavy_locked", 0), new TextKey("dodging_counter_callout", 0),
            new TextKey("melee_swords", 0), new TextKey("melee_knives", 0), new TextKey("melee_clubs", 0), new TextKey("melee_polearms", 0),
            new TextKey("melee_spears", 0), new TextKey("melee_axes", 0), new TextKey("melee_unarmed", 0),
            new TextKey("swimming_quick_dry", 1), new TextKey("swimming_long_breath", 2), new TextKey("swimming_salvage", 1),
            new TextKey("riding_gallop", 1), new TextKey("riding_steady_seat", 1),
            new TextKey("jumping_light_feet", 1), new TextKey("jumping_soft_landing", 2), new TextKey("jumping_roll", 1), new TextKey("jumping_roll_locked", 0),
            new TextKey("running_pack_mule", 1), new TextKey("running_burden", 0), new TextKey("running_burden_locked", 0),
            new TextKey("running_wind", 1), new TextKey("running_wind_locked", 0), new TextKey("running_second_wind", 0),
            new TextKey("ranged_bow_recover", 1), new TextKey("ranged_steady_reload", 0), new TextKey("ranged_steady_reload_locked", 0),
            new TextKey("ranged_eitr_regen", 1), new TextKey("ranged_blood_cost", 1), new TextKey("ranged_shield_xp", 0),
            new TextKey("crafting_makers_mark", 1), new TextKey("crafting_sturdy_build", 1), new TextKey("crafting_thrifty", 1),
            new TextKey("crafting_journeyman", 0), new TextKey("crafting_journeyman_locked", 0),
            new TextKey("sneak_vanish", 0), new TextKey("sneak_vanish_locked", 0),
            new TextKey("angler_close_help", 0), new TextKey("angler_fish_help", 0),
        };

        /// <summary>Fixed keys, the custom skills' names and descriptions (CS1), then the skill book's headings, rows and notes.</summary>
        public static IReadOnlyList<TextKey> All { get; } = Array.AsReadOnly(Fixed
            .Concat(Skills.SagaSkills.All.Where(s => s.IsCustom).SelectMany(s => new[] { new TextKey(s.TextKey, 0), new TextKey(s.TextKey + "_description", 0) }))
            .Concat(new[] { new TextKey(Progression.SkillBook.HeadingPerks, 0), new TextKey(Progression.SkillBook.HeadingMilestones, 0) })
            .Concat(Progression.SkillBook.PerkKeys.Select(k => new TextKey(k, 1)))
            .Concat(Progression.SkillBook.MilestoneKeys.Select(k => new TextKey(k, 1)))
            .Concat(Progression.SkillBook.NoteKeys.Select(k => new TextKey(k, 0)))
            .Concat(new[]
            {
                new TextKey(Progression.SkillBook.BreakdownBase, 1), new TextKey(Progression.SkillBook.BreakdownBonus, 2),
                new TextKey(Progression.SkillBook.BreakdownEffective, 1),
            })
            .ToArray());
    }
}
