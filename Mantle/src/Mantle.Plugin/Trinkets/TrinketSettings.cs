// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;
using ExtraPotions.Mantle.Embedded.SyncedConfig;
using ExtraPotions.Meadworks.Crucible;
using Mantle.Core;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Mantle.Trinkets
{
    internal static class TrinketSettings
    {
        internal const string Section = "7. Trinkets";
        internal static ConfigEntry<bool> Tuning { get; private set; }
        internal static ConfigEntry<TrinketPreset> Preset { get; private set; }
        internal static ConfigEntry<string> CustomTable { get; private set; }
        internal static ConfigEntry<bool> DisplayEffectiveValues { get; private set; }
        internal static ConfigEntry<int> DetailsTrinket { get; private set; }
        internal static event Action Changed;
        internal static Func<string> EffectiveStatus { get; set; }
        internal static bool Enabled => Tuning != null && Tuning.Value && InventorySettings.Enabled.Value;
        internal static bool KillRewardsEnabled => Enabled && killRewards;
        internal static TrinketTable EffectiveTable => effective;
        internal static TrinketTable NativeTable => native;
        internal static bool ServerOverlay => synced != null && CustomTable != null
            && synced.Sync.HoldsServerValue(CustomTable.Definition);

        static SyncedConfiguration synced;
        static TrinketTable native, custom, effective;
        static TableValue validation;
        static bool killRewards;

        internal static void Initialize(SyncedConfiguration configuration)
        {
            Dispose();
            synced = configuration;
            Tuning = synced.Bind(Section, "Trinket Tuning", false,
                "Mantle owns this host/server setting. Off restores native trinket values and adds no kill adrenaline. Changes apply live and are saved on the host; connected clients use the server overlay and keep their own saved preferences.",
                tags: new object[] { new ConfigurationManagerAttributes("trinket_tuning") });
            Preset = synced.Bind(Section, "Trinket Preset", TrinketPreset.Native,
                "Host/server preset. {0} preserves the game. {1} adds 5% of the effective adrenaline threshold per server-credited hostile kill, weighted by the target's native multiplier; no allies, tames, players or party sharing. {2} uses the complete validated table. Recipes stay native.",
                acceptableValues: new PresetValue(), tags: new object[] { new ConfigurationManagerAttributes("trinket_preset"),
                    new ConfigPresentation<TrinketPreset>(Plugin.PluginGuid, (entry, language) => TrinketPresentation.Preset(entry)) });
            validation = new TableValue();
            CustomTable = synced.Bind(Section, "Custom Trinket Table", "",
                "Host/server JSON array of all 15 native trinkets, populated from this game's validated native data. Each row requires prefab, threshold (1-200 adrenaline points), duration (0.1-300 seconds), killFraction (0-0.10), and the exact native effects map. Resource bursts: 0-200 points; skills: 0-30 levels; armor: 0-40 points; beneficial multipliers: 1-2 (0-100% bonus); stamina-use modifiers: -1 to 0 (-100% to 0%); movement: 0-0.5 (0-50%); damage bonus: 0-1 (0-100%); resistances: native enum integers only. Unknown, duplicate, incomplete, nonfinite or out-of-range values reject the entire edit and retain the last valid table. Native prefabs and typed effect names remain stable JSON identifiers. Timed block bonus: 0-1 (0-100%).",
                acceptableValues: validation, tags: new object[] { new ConfigurationManagerAttributes("custom_trinket_table") });
            DisplayEffectiveValues = synced.Bind(Section, "Display Effective Values", true,
                "Local display preference. Show Mantle's actual tuning owner, server/local scope, effective and native threshold, duration, typed effects and kill fraction here. Native fallback is shown when tuning cannot apply.",
                synced: false, tags: new object[] { new ConfigPresentation<bool>(Plugin.PluginGuid, (entry, language) => TrinketPresentation.Values(entry)) });
            DetailsTrinket = synced.Bind(Section, "Trinket Details", 1,
                "Local display selection, 1-15. Choose a native trinket to inspect its actual name, effective and native values, owner, scope and fallback. Display Effective Values controls these details; this selector never changes gameplay.",
                synced: false, acceptableValues: new AcceptableValueRange<int>(1, TrinketTable.NativeIds.Count),
                tags: new object[] { new ConfigPresentation<int>(Plugin.PluginGuid, (entry, language) => TrinketPresentation.Details(entry)) });
            MeadworksBlockedOptions.Track(Plugin.PluginGuid, DisplayEffectiveValues, ConfigScope.Local);
            MeadworksBlockedOptions.Track(Plugin.PluginGuid, DetailsTrinket, ConfigScope.Local);
            Tuning.SettingChanged += OnChanged;
            Preset.SettingChanged += OnChanged;
            CustomTable.SettingChanged += OnChanged;
            DisplayEffectiveValues.SettingChanged += OnChanged;
            InventorySettings.Enabled.SettingChanged += OnChanged;
            synced.Sync.BindingChanged += OnBindingChanged;
            synced.Sync.Pushed += OnPushed;
        }

        internal static void SetNativeTable(TrinketTable table)
        {
            if (table == null) throw new ArgumentNullException(nameof(table));
            native = table;
            custom = table;
            validation.Ready(table);
            string initial = Serialize(table);
            if (string.IsNullOrWhiteSpace(CustomTable.Value))
            {
                // Charter owns remote overlays. Only initialize this machine's own new default.
                if (!ServerOverlay) CustomTable.Value = initial;
            }
            else if (!TryParse(CustomTable.Value, table, out custom, out string error))
            {
                custom = table;
                synced.Log.LogWarning("Trinket table rejected; native values retained: " + error);
                if (!ServerOverlay) CustomTable.Value = initial;
            }
            Refresh();
        }

        static void OnPushed(bool first) => Refresh();
        static void OnBindingChanged() => Refresh();
        static void OnChanged(object sender, EventArgs args) => Refresh();
        static void Refresh()
        {
            if (native != null && TryParse(CustomTable.Value, native, out var parsed, out _)) custom = parsed;
            effective = native == null ? null : TrinketTable.ForPreset(Enabled ? Preset.Value : TrinketPreset.Native, native, custom);
            killRewards = effective != null && effective.Rows.Values.Any(row => row.KillFraction > 0f);
            Changed?.Invoke();
        }

        internal static void Dispose()
        {
            if (Tuning != null) Tuning.SettingChanged -= OnChanged;
            if (Preset != null) Preset.SettingChanged -= OnChanged;
            if (CustomTable != null) CustomTable.SettingChanged -= OnChanged;
            if (DisplayEffectiveValues != null) DisplayEffectiveValues.SettingChanged -= OnChanged;
            if (DisplayEffectiveValues != null) MeadworksBlockedOptions.Untrack(DisplayEffectiveValues);
            if (DetailsTrinket != null) MeadworksBlockedOptions.Untrack(DetailsTrinket);
            if (InventorySettings.Enabled != null) InventorySettings.Enabled.SettingChanged -= OnChanged;
            if (synced != null)
            {
                synced.Sync.BindingChanged -= OnBindingChanged;
                synced.Sync.Pushed -= OnPushed;
            }
            synced = null; native = null; custom = null; effective = null; validation = null; EffectiveStatus = null;
            Tuning = null; Preset = null; CustomTable = null; DisplayEffectiveValues = null; DetailsTrinket = null;
            killRewards = false;
        }

        internal static string Serialize(TrinketTable table)
        {
            var rows = new JArray();
            foreach (var row in table.Rows.Values.OrderBy(value => value.PrefabId, StringComparer.Ordinal))
            {
                var effects = new JObject();
                foreach (var effect in row.Effects.OrderBy(value => value.Key, StringComparer.Ordinal)) effects.Add(effect.Key, effect.Value);
                rows.Add(new JObject { ["prefab"] = row.PrefabId, ["threshold"] = row.Threshold,
                    ["duration"] = row.Duration, ["killFraction"] = row.KillFraction, ["effects"] = effects });
            }
            return rows.ToString(Formatting.None);
        }

        internal static bool TryParse(string json, TrinketTable baseline, out TrinketTable table, out string error)
        {
            table = null;
            try
            {
                if (json == null || json.Length > 131072) throw new JsonException("Missing or oversized table.");
                var array = JArray.Parse(json, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
                var rows = new List<TrinketRow>();
                foreach (var token in array)
                {
                    if (!(token is JObject row) || row.Count != 5
                        || row.Properties().Any(property => !new[] { "prefab", "threshold", "duration", "killFraction", "effects" }.Contains(property.Name, StringComparer.Ordinal))
                        || row["prefab"]?.Type != JTokenType.String || !(row["effects"] is JObject effects))
                        throw new JsonException("Each row requires exactly prefab, threshold, duration, killFraction and effects.");
                    var values = new Dictionary<string, float>(StringComparer.Ordinal);
                    foreach (var effect in effects.Properties()) values.Add(effect.Name, Number(effect.Value));
                    rows.Add(new TrinketRow((string)row["prefab"], Number(row["threshold"]), Number(row["duration"]), Number(row["killFraction"]), values));
                }
                return TrinketTable.TryCreateCustom(rows, baseline, out table, out error);
            }
            catch (Exception exception) when (exception is JsonException || exception is ArgumentException || exception is OverflowException)
            { error = exception.Message; return false; }
        }

        static float Number(JToken value)
        {
            if (value == null || (value.Type != JTokenType.Float && value.Type != JTokenType.Integer)) throw new JsonException("Effect and row values must be numbers.");
            float result = value.Value<float>();
            if (float.IsNaN(result) || float.IsInfinity(result)) throw new JsonException("Numbers must be finite.");
            return result;
        }

        sealed class TableValue : AcceptableValueBase
        {
            TrinketTable baseline;
            internal TableValue() : base(typeof(string)) { }
            internal void Ready(TrinketTable table) { baseline = table; }
            public override object Clamp(object value)
            {
                string candidate = value as string ?? "";
                if (baseline == null) return candidate;
                if (candidate.Length == 0) return Serialize(baseline);
                if (TryParse(candidate, baseline, out _, out string error)) return candidate;
                synced?.Log.LogWarning("Trinket table rejected; last valid table retained: " + error);
                string prior = CustomTable?.Value;
                return TryParse(prior, baseline, out _, out _) ? prior : Serialize(baseline);
            }
            public override bool IsValid(object value) => baseline == null ? value is string
                : value is string text && TryParse(text, baseline, out _, out _);
            public override string ToDescriptionString() => "# Complete validated native-trinket table; invalid edits are rejected atomically.";
        }

        sealed class PresetValue : AcceptableValueBase
        {
            internal PresetValue() : base(typeof(TrinketPreset)) { }
            public override bool IsValid(object value) => value is TrinketPreset preset && Enum.IsDefined(typeof(TrinketPreset), preset);
            public override object Clamp(object value)
            {
                if (IsValid(value)) return value;
                synced?.Log.LogWarning("Undefined trinket preset rejected; previous valid preset retained.");
                return Preset != null && IsValid(Preset.Value) ? Preset.Value : TrinketPreset.Native;
            }
            public override string ToDescriptionString() => "# Native, Conservative or Custom.";
        }

        sealed class ConfigurationManagerAttributes
        {
            readonly string key;
            internal ConfigurationManagerAttributes(string key) { this.key = key; }
            bool PresetChoices => key == "trinket_preset";
            public string DispName => Accessories.AccessoryText.Text("cfg_7_trinkets__" + key + "_name");
            public string Category => Accessories.AccessoryText.Text("cfg_7_trinkets_heading");
            public string Description => PresetChoices ? TrinketPresentation.PresetHelp()
                : Accessories.AccessoryText.Text("cfg_7_trinkets__" + key + "_help");
            // Almanac shows ObjToStr on the value control and invokes it again after a language change. The saved value stays the enum name.
            public Func<object, string> ObjToStr => PresetChoices ? new Func<object, string>(ShowPreset) : null;
            public Func<string, object> StrToObj => PresetChoices ? new Func<string, object>(ReadPreset) : null;
            static string ShowPreset(object value)
            {
                var preset = value is TrinketPreset named && Enum.IsDefined(typeof(TrinketPreset), named) ? named : TrinketPreset.Native;
                return TrinketPresentation.PresetLabel(preset);
            }
            static object ReadPreset(string text)
            {
                if (!TrinketPresentation.TryParsePreset(text, out var preset)) throw new ArgumentException("Unknown trinket preset.");
                return preset;
            }
        }
    }
}
