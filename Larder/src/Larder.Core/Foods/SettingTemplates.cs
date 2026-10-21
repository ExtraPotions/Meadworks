// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
using System.Collections.Generic;

namespace ExtraPotions.Larder.Foods
{
    /// <summary>
    /// Text of the per-item settings (one section per food, mead and cooking station). The plugin formats the English
    /// help below into the config description; the translated templates under the same keys live in Larder's
    /// localization files, formatted with the item's localized name. Literal cfg_ keys would add two entries per
    /// setting per language; these templates add a few dozen.
    /// </summary>
    public static class SettingTemplates
    {
        public const string Food = "food", Mead = "mead", Station = "station";

        /// <summary>A station's per-recipe entries share one template; the entry key is the raw item's prefab name.</summary>
        public const string Recipe = "recipe";

        public static readonly IReadOnlyDictionary<string, string> SectionPrefix = new Dictionary<string, string>
        {
            [Food] = "Food: ", [Mead] = "Mead: ", [Station] = "Station: ",
        };

        /// <summary>Kind, entry key, English help. {0} is the item's name; a station recipe adds {1}, the cooked item.</summary>
        public static readonly IReadOnlyList<(string Kind, string Key, string Help)> All = new[]
        {
            (Food, "Health", "Health {0} adds to your maximum while it lasts. Health Modifier in 2 - Foods applies on top. The game's value is the default."),
            (Food, "Stamina", "Stamina {0} adds to your maximum while it lasts. Stamina Modifier in 2 - Foods applies on top. The game's value is the default."),
            (Food, "Duration", "Seconds {0} lasts after eating. Duration Modifier in 2 - Foods and Food Rate in 5 - Exertion also apply. The game's value is the default."),
            (Food, "HealthRegen", "Health {0} restores on each regeneration tick while it lasts. Health Regen Modifier in 2 - Foods applies on top. The game's value is the default."),
            (Food, "Eitr", "Eitr {0} adds to your maximum while it lasts. Eitr Modifier in 2 - Foods applies on top. The game's value is the default."),
            (Food, "Vigor", "Extra Vigor of {0}: percent added to stamina regeneration while the food is active, on top of its Stamina times Vigor Per Stamina Point. Does not fade with the food. Stacks with Regen Per Extra Stamina Point."),
            (Food, "EitrVigor", "Extra Eitr Vigor of {0}: percent added to eitr regeneration while the food is active, on top of its Eitr times Eitr Vigor Per Eitr Point. Does not fade with the food."),
            (Mead, "Duration", "Duration of {0} effect in seconds. While it runs, neither this mead nor another of its group (the healing meads, for example) can be drunk, so this is also the mead's cooldown."),
            (Mead, "HealthOverTime", "Total health restored over time by {0}."),
            (Mead, "StaminaOverTime", "Total stamina restored over time by {0}."),
            (Mead, "EitrOverTime", "Total eitr restored over time by {0}."),
            (Mead, "HealthRegenMultiplier", "Health regeneration multiplier while {0} lasts (1 = no change)."),
            (Mead, "StaminaRegenMultiplier", "Stamina regeneration multiplier while {0} lasts (1 = no change). Combines with the stamina rules in 4 - Recovery, like the game's own meads."),
            (Mead, "EitrRegenMultiplier", "Eitr regeneration multiplier while {0} lasts (1 = no change). Combines with the eitr rules in 4 - Recovery."),
            (Mead, "RunStaminaModifier", "Added fraction of the run stamina drain while {0} lasts: -0.2 makes running 20% cheaper."),
            (Mead, "JumpStaminaModifier", "Added fraction of the jump stamina cost while {0} lasts: -0.2 makes jumping 20% cheaper."),
            (Station, Recipe, "Seconds {0} takes to cook into {1} on this station. Cook Time Multiplier in 6 - Kitchen applies on top."),
        };

        static readonly Dictionary<(string, string), string> help = new Dictionary<(string, string), string>();

        static SettingTemplates()
        {
            foreach (var (kind, key, text) in All) help[(kind, key)] = text;
        }

        /// <summary>The English help of an entry, formatted as the config description.</summary>
        public static string Help(string kind, string key, params object[] arguments)
            => string.Format(System.Globalization.CultureInfo.InvariantCulture, help[(kind, key)], arguments);

        public static string HeadingKey(string kind) => "tpl_" + kind + "_heading";
        public static string NameKey(string kind, string key) => "tpl_" + kind + "__" + key.ToLowerInvariant() + "_name";
        public static string HelpKey(string kind, string key) => "tpl_" + kind + "__" + key.ToLowerInvariant() + "_help";

        /// <summary>English heading template: the section name with {0} in place of the prefab name.</summary>
        public static string Heading(string kind) => SectionPrefix[kind] + "{0}";
    }
}
