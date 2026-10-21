// Edge - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: the localization keys Edge uses (tokens meadworks_edge_<key>, design section 12).
using System;
using System.Collections.Generic;
using System.Linq;
using ExtraPotions.Edge.Integration;
using ExtraPotions.Edge.Secondaries;

namespace ExtraPotions.Edge.Localization
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
    public static class EdgeTextKeys
    {
        public const string OffNoCrucible = "off_no_crucible";
        public const string OffNoEdge = "off_no_edge";

        /// <summary>{0} number of settings, {1} source name.</summary>
        public const string ImportDone = "import_done";
        public const string CheatNoSecondaryCooldowns = "cheat_no_secondary_cooldowns";
        public const string HintSecondary = "hint_secondary";
        public const string HintDetonate = "hint_detonate";

        /// <summary>{0} preset name.</summary>
        public const string TooltipSecondary = "tooltip_secondary";

        /// <summary>{0} seconds.</summary>
        public const string TooltipCooldown = "tooltip_cooldown";
        public const string HudReady = "hud_ready";

        /// <summary>Keys with fixed names.</summary>
        internal static readonly TextKey[] Fixed =
        {
            new TextKey(OffNoCrucible, 0),
            new TextKey(OffNoEdge, 0),
            new TextKey(ImportDone, 2),
            new TextKey(CheatNoSecondaryCooldowns, 0),
            new TextKey(HintSecondary, 0),
            new TextKey(HintDetonate, 0),
            new TextKey(TooltipSecondary, 1),
            new TextKey(TooltipCooldown, 1),
            new TextKey(HudReady, 0),
            new TextKey("tooltip_summon_quality", 0),
            new TextKey("tooltip_stamina", 1),
            new TextKey("tooltip_eitr", 1),
            new TextKey("tooltip_health", 1),
            new TextKey("page_health_max", 0),
            new TextKey("page_health_current", 0),
            new TextKey("page_health_experience", 1),
            new TextKey("page_summon_lifetime", 1),
            new TextKey("page_cooldown_reduction", 3),
            new TextKey("page_ambush_charge", 2),
            new TextKey("hud_empower", 0),
            new TextKey("summon_name_format", 1),
            new TextKey("dodge_cancel_description", 0),
            new TextKey("page_dodge_cancel", 0),
            new TextKey("keep_crossbow_loaded_description", 0),
            new TextKey("arrow_recovery_chance_description", 0),
            new TextKey("bow_zoom_description", 0),
            new TextKey("bow_zoom_factor_description", 0),
            new TextKey("draw_indicator_description", 0),
        };

        /// <summary>Fixed keys, one step-aside notice per module, a name and a description per catalog preset, then the Compendium page.</summary>
        public static IReadOnlyList<TextKey> All { get; } = Array.AsReadOnly(Fixed
            .Concat(((EdgeModule[])Enum.GetValues(typeof(EdgeModule))).Select(m => new TextKey(StepAsideNotice.KeyOf(m), 1)))
            .Concat(PresetCatalog.Entries.SelectMany(p => new[] { new TextKey(p.NameKey, 0), new TextKey(p.DescriptionKey, 0) }))
            .Concat(CompendiumPage.Keys.Select(k => new TextKey(k, 0)))
            .ToArray());
    }
}
