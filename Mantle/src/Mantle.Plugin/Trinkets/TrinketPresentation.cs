// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Globalization;
using System.Text;
using BepInEx.Configuration;
using ExtraPotions.Meadworks.Crucible;
using Mantle.Accessories;

namespace Mantle.Trinkets
{
    internal static class TrinketPresentation
    {
        internal static ConfigPresentationText Values(ConfigEntry<bool> entry)
        {
            string help = Text("cfg_7_trinkets__display_effective_values_help");
            return new ConfigPresentationText(Text("cfg_7_trinkets__display_effective_values_name"),
                Text("cfg_7_trinkets_heading"), help);
        }

        internal static ConfigPresentationText Details(ConfigEntry<int> entry)
        {
            string prefab = TrinketTable.NativeIds[entry.Value - 1];
            string help = Text("cfg_7_trinkets__trinket_details_help");
            if (TrinketSettings.DisplayEffectiveValues.Value) help += "\n\n" + Describe(prefab);
            return new ConfigPresentationText(AccessoryText.Format("trinket_selected", Number(entry.Value), Name(prefab)),
                Text("cfg_7_trinkets_heading"), help);
        }

        static string Describe(string prefab)
        {
            var text = new StringBuilder();
            text.AppendLine(Text(TrinketSettings.ServerOverlay ? "trinket_owner_server" : "trinket_owner_local"));
            var effective = TrinketSettings.EffectiveTable;
            var native = TrinketSettings.NativeTable;
            if (effective == null || native == null) return text.Append(Text("trinket_native_pending")).ToString();
            string reason = TrinketRuntime.FallbackReason ?? "";
            bool canApply = TrinketRuntime.CanApply && string.IsNullOrEmpty(reason);
            if (!canApply) effective = native;
            text.AppendLine(Text(canApply ? "trinket_tuning_active"
                : TrinketSettings.Enabled ? "trinket_native_restored" : "trinket_native_fallback"));
            if (TrinketSettings.Enabled && !canApply)
            {
                if (!TrinketKillCredit.TuningSupported) reason = "peer-protocol";
                int separator = reason.IndexOf(':');
                string code = separator < 0 ? reason : reason.Substring(0, separator);
                string key = "trinket_fallback_" + code.Replace('-', '_');
                string translated = Text(key);
                text.AppendLine(translated == key ? Text("trinket_fallback_unavailable") : translated);
                if (separator >= 0) text.AppendLine(AccessoryText.Format("trinket_diagnostic", reason));
            }
            if (effective.TryGet(prefab, out var row))
            {
                native.TryGet(row.PrefabId, out var original);
                text.AppendLine();
                text.AppendLine(Name(row.PrefabId));
                text.AppendLine(AccessoryText.Format("trinket_threshold", Number(row.Threshold), Number(original.Threshold)));
                text.AppendLine(AccessoryText.Format("trinket_duration", Number(row.Duration), Number(original.Duration)));
                text.AppendLine(AccessoryText.Format("trinket_kill_fraction", Number(row.KillFraction * 100f)));
                foreach (var field in row.Effects)
                    text.AppendLine(AccessoryText.Format("trinket_effect", Label(field.Key), Value(field.Key, field.Value), Value(field.Key, original.Effects[field.Key])));
            }
            return text.ToString().TrimEnd();
        }

        internal static string PresetKey(TrinketPreset preset)
        {
            if (!Enum.IsDefined(typeof(TrinketPreset), preset)) throw new ArgumentOutOfRangeException(nameof(preset));
            return "trinket_preset_" + preset.ToString().ToLowerInvariant();
        }

        /// <summary>The name shown for a preset. Read on each call so a language change updates open settings. The config value stays the English identifier.</summary>
        internal static string PresetLabel(TrinketPreset preset)
        {
            string key = PresetKey(preset);
            string text = Text(key);
            return string.IsNullOrWhiteSpace(text) || string.Equals(text, key, StringComparison.Ordinal) ? preset.ToString() : text;
        }

        internal static string PresetHelp() => AccessoryText.Format("cfg_7_trinkets__trinket_preset_help",
            PresetLabel(TrinketPreset.Native), PresetLabel(TrinketPreset.Conservative), PresetLabel(TrinketPreset.Custom));

        /// <summary>Almanac resolves this on each redraw. The catalog help is a template; the three names are read here so a language change fills them in.</summary>
        internal static ConfigPresentationText Preset(ConfigEntry<TrinketPreset> entry)
        {
            if (entry == null) throw new ArgumentNullException(nameof(entry));
            return new ConfigPresentationText(Text("cfg_7_trinkets__trinket_preset_name"),
                Text("cfg_7_trinkets_heading"), PresetHelp());
        }

        internal static bool TryParsePreset(string text, out TrinketPreset preset)
        {
            preset = TrinketPreset.Native;
            if (string.IsNullOrWhiteSpace(text)) return false;
            text = text.Trim();
            if (Enum.TryParse(text, true, out preset) && Enum.IsDefined(typeof(TrinketPreset), preset)) return true;
            foreach (TrinketPreset option in Enum.GetValues(typeof(TrinketPreset)))
                if (string.Equals(PresetLabel(option), text, StringComparison.Ordinal)) { preset = option; return true; }
            return false;
        }

        static string Name(string prefab)
        {
            var item = ObjectDB.instance ? ObjectDB.instance.GetItemPrefab(prefab)?.GetComponent<ItemDrop>() : null;
            return item && Localization.instance != null ? Localization.instance.Localize(item.m_itemData.m_shared.m_name) : prefab;
        }
        static string Text(string key) => AccessoryText.Text(key);
        static string Number(float value) => value.ToString("0.###", CultureInfo.CurrentCulture);
        static string Label(string key) => Text("trinket_field_" + key.Replace('.', '_').ToLowerInvariant());
        static string Value(string key, float value)
        {
            if (key.StartsWith("resistance.", StringComparison.Ordinal))
            {
                string name = Enum.GetName(typeof(HitData.DamageModifier), (int)value);
                return Text("trinket_resistance_" + (name ?? "normal").ToLowerInvariant());
            }
            if (key.EndsWith("RegenMultiplier", StringComparison.Ordinal))
                return AccessoryText.Format("trinket_unit_percent", Number((value - 1f) * 100f));
            if (key.EndsWith("UseModifier", StringComparison.Ordinal) || key.EndsWith("DrainModifier", StringComparison.Ordinal)
                || key == "m_timedBlockBonus" || key.EndsWith("SpeedModifier", StringComparison.Ordinal)
                || key == "m_speedModifier" || key.StartsWith("m_percentigeDamageModifiers.", StringComparison.Ordinal))
                return AccessoryText.Format("trinket_unit_percent", Number(value * 100f));
            return Number(value);
        }
    }
}
