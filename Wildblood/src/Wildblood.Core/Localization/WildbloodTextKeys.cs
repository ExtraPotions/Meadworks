// Wildblood - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: the localization keys Wildblood uses (tokens meadworks_wildblood_<key>, design section 13).
using System;
using System.Collections.Generic;
using System.Linq;
using ExtraPotions.Wildblood.Bosses;
using ExtraPotions.Wildblood.Integration;
using ExtraPotions.Wildblood.Stars;
using ExtraPotions.Wildblood.Traits;

namespace ExtraPotions.Wildblood.Localization
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
    public static partial class WildbloodTextKeys
    {
        public const string OffNoCrucible = "off_no_crucible";
        public const string OffNoWildblood = "off_no_wildblood";
        public const string LuckClientOnly = "luck_client_only";
        public const string WorldHardens = "world_hardens";
        public const string WorldTierLabel = "world_tier_label";
        public const string ImportDone = "import_done";
        public const string CheatDropMultipliers = "cheat_drop_multipliers";
        public const string CheatLuckyKills = "cheat_lucky_kills";
        public const string CheatCreatureCommands = "cheat_creature_commands";

        /// <summary>Per-player difficulty yields to a named mod: {0} is its name.</summary>
        public const string StepAsidePressureScaling = "step_aside_pressure_scaling";

        /// <summary>Per-player difficulty yields because the game fields were changed by an unidentified mod.</summary>
        public const string PressureScalingExternal = "pressure_scaling_external";

        /// <summary>Decorated name: {0} the trait words, {1} the creature name (word order per language).</summary>
        public const string NameFormat = "name_format";

        public static string MutationName(Mutation mutation) => "mutation_" + Mutations.Id(mutation);
        public static string ModifierName(Modifier modifier) => "modifier_" + Modifiers.Id(modifier);
        public static string AspectName(Aspect aspect) => "aspect_" + Aspects.Id(aspect);
        public static string DifficultyName(Difficulty difficulty) => "difficulty_" + difficulty.ToString().ToLowerInvariant();

        /// <summary>Keys with fixed names.</summary>
        internal static readonly TextKey[] Fixed =
        {
            new TextKey(OffNoCrucible, 0),
            new TextKey(OffNoWildblood, 0),
            new TextKey(LuckClientOnly, 0),
            new TextKey(WorldHardens, 1),
            new TextKey("boss_material_reward", 3),
            new TextKey("boss_defeated_once", 0),
            new TextKey("boss_defeated_times", 1),
            new TextKey("boss_defeated_cap", 1),
            new TextKey(WorldTierLabel, 1),
            new TextKey(ImportDone, 2),
            new TextKey(CheatDropMultipliers, 0),
            new TextKey(CheatLuckyKills, 0),
            new TextKey(CheatCreatureCommands, 0),
            new TextKey(NameFormat, 2),
            new TextKey("recap_title", 0),
            new TextKey("recap_close", 0),
            new TextKey("recap_older", 0),
            new TextKey("recap_newer", 0),
            new TextKey("recap_no_video", 0),
            new TextKey("recap_no_deaths", 0),
            new TextKey("recap_play", 0),
            new TextKey("recap_pause", 0),
            new TextKey("recap_back", 0),
            new TextKey("recap_forward", 0),
            new TextKey("recap_death", 0),
            new TextKey("recap_previous_hit", 0),
            new TextKey("recap_next_hit", 0),
            new TextKey("recap_summary", 4),
            new TextKey("recap_clock", 3),
            new TextKey("recap_hits", 1),
            new TextKey("recap_damage", 3),
            new TextKey("corrodent_wear", 2),
            new TextKey("recap_when", 2),
            new TextKey("recap_killed", 1),
            new TextKey("recap_died", 1),
            new TextKey("recap_killed_2", 1),
            new TextKey("recap_killed_3", 1),
            new TextKey("recap_died_2", 1),
            new TextKey("recap_died_3", 1),
            new TextKey("recap_notice", 2),
            new TextKey("tier_plate_title", 0),
            new TextKey("karma_value", 1),
            new TextKey("karma_level_value", 2),
            new TextKey("karma_level", 1),
            new TextKey("board_defeated", 1),
            new TextKey("board_more", 1),
            new TextKey("board_page", 2),
            new TextKey("board_previous", 0),
            new TextKey("board_next", 0),
            new TextKey("board_open", 0),
            new TextKey("board_help", 0),
            new TextKey("board_unavailable", 0),
            new TextKey("board_none", 0),
            new TextKey("board_waiting", 0),
            new TextKey("board_timeout", 0),
            new TextKey("egg_plain", 0),
            new TextKey("egg_hatches", 1),
            new TextKey("egg_one_star", 0),
            new TextKey("egg_stars", 1),
            new TextKey("lore_title", 0),
            new TextKey("lore_tab_mutations", 0),
            new TextKey("lore_tab_modifiers", 0),
            new TextKey("lore_tab_aspects", 0),
            new TextKey("lore_tab_difficulty", 0),
            new TextKey("lore_previous", 0),
            new TextKey("lore_next", 0),
            new TextKey("lore_close", 0),
            new TextKey("lore_page", 2),
            new TextKey("lore_example", 1),
            new TextKey("lore_global", 0),
            new TextKey("lore_boss_rules", 0),
            new TextKey("lore_disabled", 0),
            new TextKey("lore_none", 0),
            new TextKey("lore_prose_note", 0),
            new TextKey("lore_difficulty", 1),
            new TextKey("lore_stars", 1),
            new TextKey("lore_stars_off", 0),
            new TextKey("lore_mutations", 1),
            new TextKey("lore_star_chances", 2),
            new TextKey("lore_compendium", 0),
            new TextKey("lore_help", 0),
            new TextKey("lore_unavailable", 0),
            new TextKey("lore_reference_done", 2),
            new TextKey("lore_reference_failed", 0),
            new TextKey("lore_reference_world", 0),
            new TextKey("lore_help_more", 0),
            new TextKey("lore_help_tabmutations", 0),
            new TextKey("lore_help_tabmodifiers", 0),
            new TextKey("lore_help_tabaspects", 0),
            new TextKey("lore_help_tabdifficulty", 0),
            new TextKey("lore_help_previous", 0),
            new TextKey("lore_help_next", 0),
            new TextKey("lore_help_close", 0),
            new TextKey("recap_help_older", 0),
            new TextKey("recap_help_newer", 0),
            new TextKey("recap_help_play", 0),
            new TextKey("recap_help_back", 0),
            new TextKey("recap_help_forward", 0),
            new TextKey("recap_help_death", 0),
            new TextKey("recap_help_previous_hit", 0),
            new TextKey("recap_help_next_hit", 0),
            new TextKey("recap_help_close", 0),
            new TextKey("recap_help_time", 0),
            new TextKey("recap_help_timeline", 0),
            new TextKey("theft_status_none", 0),
            new TextKey("theft_status_line", 4),
            new TextKey("theft_phase_unknown", 0),
            new TextKey("theft_phase_approved", 0),
            new TextKey("theft_phase_denied", 0),
            new TextKey("theft_legacy", 0),
            new TextKey("theft_recover_done", 3),
            new TextKey("theft_unavailable", 0),
            new TextKey("inspect_locked", 0),
            new TextKey("commands_off", 0),
            new TextKey("spawn_modifiers_applied", 1),
            new TextKey("spawn_modifier_dropped", 1),
            new TextKey("spawn_modifiers_failed", 1),
            new TextKey("spawn_modifiers_unresolved", 0),
            new TextKey("raid_request_command", 0),
            new TextKey("raid_request_player", 0),
            new TextKey("raid_request_chest", 0),
            new TextKey("raid_request_unavailable", 0),
            new TextKey("raid_request_sent", 1),
            new TextKey("raid_request_started", 1),
            new TextKey("raid_request_stopped", 1),
            new TextKey("raid_request_refused", 1),
            new TextKey("raid_request_timeout", 1),
            new TextKey("raid_usage", 0),
            new TextKey("raid_tier_invalid", 1),
            new TextKey(StepAsidePressureScaling, 1),
            new TextKey(PressureScalingExternal, 0),
            new TextKey("karma_quote_01", 0),
            new TextKey("karma_quote_02", 0),
            new TextKey("karma_quote_03", 0),
            new TextKey("karma_quote_04", 0),
            new TextKey("karma_quote_05", 0),
            new TextKey("enforcer_spawn_01", 0),
            new TextKey("enforcer_spawn_02", 0),
            new TextKey("enforcer_spawn_03", 0),
            new TextKey("enforcer_spawn_04", 0),
            new TextKey("enforcer_spawn_05", 0),
            new TextKey("enforcer_death_01", 0),
            new TextKey("enforcer_death_02", 0),
            new TextKey("enforcer_death_03", 0),
            new TextKey("enforcer_death_04", 0),
            new TextKey("enforcer_death_05", 0),
            new TextKey("enforcer_warning", 1),
            new TextKey("enforcer_suffix", 0),
            new TextKey("minion_suffix", 0),
            new TextKey("status_corrosive_name", 0),
            new TextKey("status_exposed_tooltip", 0),
            new TextKey("status_weakened_tooltip", 0),
            new TextKey("status_withered_tooltip", 0),
            new TextKey("status_crippling_tooltip", 0),
            new TextKey("status_disruptive_tooltip", 0),
            new TextKey("status_adrenaline_drain_tooltip", 0),
            new TextKey("status_corrosive_tooltip", 0),
            new TextKey("damage_blunt", 0),
            new TextKey("damage_slash", 0),
            new TextKey("damage_pierce", 0),
            new TextKey("damage_chop", 0),
            new TextKey("damage_pickaxe", 0),
            new TextKey("damage_fire", 0),
            new TextKey("damage_frost", 0),
            new TextKey("damage_lightning", 0),
            new TextKey("damage_poison", 0),
            new TextKey("damage_spirit", 0),
            new TextKey("resistance_weak", 0),
            new TextKey("resistance_very_weak", 0),
            new TextKey("resistance_resistant", 0),
            new TextKey("resistance_very_resistant", 0),
            new TextKey("resistance_immune", 0),
        };

        /// <summary>Fixed keys, then one step-aside notice per module that can step aside, then every catalog name.</summary>
        public static IReadOnlyList<TextKey> All { get; } = Array.AsReadOnly(Fixed
            .Concat(((WildbloodModule[])Enum.GetValues(typeof(WildbloodModule))).Where(m => m != WildbloodModule.Display)
                .Select(m => new TextKey(new StepAsideNotice(m, "").Key, 1)))
            .Concat(Mutations.InOrder.Select(m => new TextKey(MutationName(m), 0)))
            .Concat(Modifiers.InOrder.Select(m => new TextKey(ModifierName(m), 0)))
            .Concat(Aspects.Outcomes.Select(a => new TextKey(AspectName(a), 0)))
            .Concat(((Difficulty[])Enum.GetValues(typeof(Difficulty))).Select(d => new TextKey(DifficultyName(d), 0)))
            .ToArray());
    }
}
