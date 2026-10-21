// Wildblood - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: World scope resolution (design 10.1, 10.2). The storage is Crucible CS4 (proposed); this is the
// order and the key naming Wildblood expects from it.
using System;
using System.Globalization;
using System.Text;

namespace ExtraPotions.Wildblood.Config
{
    /// <summary>Where a World setting's value came from (shown by Almanac, design 10.4).</summary>
    public enum ValueSource { World, Config, Default }

    public static class WorldSettings
    {
        public const string KeyPrefix = "mw.wildblood.";

        /// <summary>
        /// The world key of a World setting: <c>mw.wildblood.&lt;section&gt;.&lt;key&gt;</c>, lowercase, the section's number
        /// dropped, spaces and dashes as underscores (game global keys are lowercase).
        /// </summary>
        public static string Key(WildbloodSetting setting)
        {
            if (setting == null) throw new ArgumentNullException(nameof(setting));
            if (setting.Scope != SettingScope.World) throw new ArgumentException(setting.Id + " is not a World setting.", nameof(setting));
            var section = setting.Section;
            var dash = section.IndexOf(" - ", StringComparison.Ordinal);
            if (dash >= 0) section = section.Substring(dash + 3);
            return KeyPrefix + Slug(section) + "." + Slug(setting.Key);
        }

        /// <summary>
        /// World value if set and readable, else the cfg value (ServerAuthoritative) if any, else the default. A world
        /// without values behaves exactly as the cfg says, so existing worlds need no migration.
        /// </summary>
        public static (T Value, ValueSource Source) Resolve<T>(WildbloodSetting<T> setting, string? worldText, T? configValue) where T : struct =>
            ResolveCore(setting, worldText, configValue.HasValue, configValue.GetValueOrDefault());

        /// <summary>Text settings (Boss Keys).</summary>
        public static (string Value, ValueSource Source) ResolveText(WildbloodSetting<string> setting, string? worldText, string? configValue) =>
            ResolveCore(setting, worldText, configValue != null, configValue!);

        private static (T, ValueSource) ResolveCore<T>(WildbloodSetting<T> setting, string? worldText, bool hasConfig, T configValue)
        {
            if (setting == null) throw new ArgumentNullException(nameof(setting));
            if (worldText != null && TryParse(worldText, out T world)) return (setting.Normalize(world), ValueSource.World);
            if (hasConfig) return (setting.Normalize(configValue), ValueSource.Config);
            return (setting.Default, ValueSource.Default);
        }

        public static bool TryParse<T>(string text, out T value)
        {
            value = default!;
            var t = text.Trim();
            object? parsed = null;
            if (typeof(T) == typeof(string)) parsed = t;
            else if (typeof(T) == typeof(bool)) { if (bool.TryParse(t, out var b)) parsed = b; }
            else if (typeof(T) == typeof(int)) { if (int.TryParse(t, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i)) parsed = i; }
            else if (typeof(T) == typeof(float)) { if (float.TryParse(t, NumberStyles.Float, CultureInfo.InvariantCulture, out var f)) parsed = f; }
            else if (typeof(T).IsEnum && t.Length > 0 && !char.IsDigit(t[0]) && t[0] != '-')
            {
                try { parsed = Enum.Parse(typeof(T), t, true); } catch (ArgumentException) { parsed = null; }
                if (parsed != null && !Enum.IsDefined(typeof(T), parsed)) parsed = null;
            }
            if (parsed == null) return false;
            value = (T)parsed;
            return true;
        }

        private static string Slug(string text)
        {
            var sb = new StringBuilder();
            foreach (var c in text.Trim().ToLowerInvariant())
                sb.Append(char.IsLetterOrDigit(c) ? c : '_');
            return sb.ToString();
        }
    }
}
