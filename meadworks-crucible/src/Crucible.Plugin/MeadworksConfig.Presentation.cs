// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Concurrent;
using System.IO;
using BepInEx.Configuration;

namespace ExtraPotions.Meadworks.Crucible
{
    /// <summary>Presentation-only text. Empty fields keep the caller's conventional/raw fallback.</summary>
    public sealed class ConfigPresentationText
    {
        public string Label { get; }
        public string Section { get; }
        public string Help { get; }
        public ConfigPresentationText(string? label = null, string? section = null, string? help = null)
        { Label = label ?? ""; Section = section ?? ""; Help = help ?? ""; }
    }

    internal interface IConfigPresentation
    {
        string ProductId { get; }
        bool Supports(ConfigEntryBase entry);
        ConfigPresentationText? Resolve(ConfigEntryBase entry, string language);
    }

    /// <summary>Optional ConfigDescription tag for the exact config owner and setting type. Resolve live display text without changing settings.</summary>
    public sealed class ConfigPresentation<T> : IConfigPresentation
    {
        readonly string productId;
        readonly Func<ConfigEntry<T>, string, ConfigPresentationText?> resolve;
        public ConfigPresentation(string productId, Func<ConfigEntry<T>, string, ConfigPresentationText?> resolve)
        {
            if (string.IsNullOrWhiteSpace(productId)) throw new ArgumentException("A registered product owner is required.", nameof(productId));
            this.productId = productId; this.resolve = resolve ?? throw new ArgumentNullException(nameof(resolve));
        }
        string IConfigPresentation.ProductId => productId;
        bool IConfigPresentation.Supports(ConfigEntryBase entry) => entry.GetType() == typeof(ConfigEntry<T>);
        ConfigPresentationText? IConfigPresentation.Resolve(ConfigEntryBase entry, string language) => resolve((ConfigEntry<T>)entry, language);
    }

    /// <summary>Plugin GUID (the settings' product id) to the translations that plugin registered through MeadworksTranslations.</summary>
    internal static class SettingTranslations
    {
        static readonly ConcurrentDictionary<string, ProductTranslations> byPlugin = new ConcurrentDictionary<string, ProductTranslations>(StringComparer.Ordinal);
        internal static void Add(string pluginGuid, ProductTranslations translations)
        { if (!string.IsNullOrEmpty(pluginGuid) && translations != null) byPlugin.TryAdd(pluginGuid, translations); }
        internal static void Remove(string pluginGuid, ProductTranslations translations)
        { if (pluginGuid != null && byPlugin.TryGetValue(pluginGuid, out var held) && ReferenceEquals(held, translations)) byPlugin.TryRemove(pluginGuid, out _); }
        internal static bool TryGet(string pluginGuid, out ProductTranslations translations) => byPlugin.TryGetValue(pluginGuid ?? "", out translations!);
    }

    public static partial class MeadworksConfig
    {
        /// <summary>Resolves an exact config entry's display text; missing, ambiguous or failing providers retain raw fallbacks. Never changes owner, scope or values.</summary>
        public static ConfigPresentationText Presentation(ConfigEntryBase entry, string language, string label, string section, string help)
        {
            var fallback = new ConfigPresentationText(label, section, help);
            if (entry == null || !entry.ConfigFile.ContainsKey(entry.Definition)
                || !ReferenceEquals(entry.ConfigFile[entry.Definition], entry)) return fallback;
            IConfigPresentation? provider = null;
            foreach (var tag in entry.Description.Tags ?? Array.Empty<object>())
                if (tag is IConfigPresentation candidate)
                {
                    if (provider != null) return fallback;
                    provider = candidate;
                }
            if (provider == null) return Conventional(entry, language, fallback);
            if (!provider.Supports(entry) || !TryPresentationOwner(entry, out var owner)
                || !string.Equals(provider.ProductId, owner, StringComparison.Ordinal)) return fallback;
            try
            {
                var given = provider.Resolve(entry, language ?? "English");
                if (given == null) return fallback;
                return new ConfigPresentationText(Prefer(given.Label, fallback.Label), Prefer(given.Section, fallback.Section), Prefer(given.Help, fallback.Help));
            }
            catch { return fallback; }
        }
        static string Prefer(string value, string fallback) => string.IsNullOrWhiteSpace(value) ? fallback : value;

        static bool TryPresentationOwner(ConfigEntryBase entry, out string owner)
        {
            if (TryGetMetadata(entry, out var metadata)) { owner = metadata.ProductId; return true; }
            // Embedded sync libraries bind outside Crucible. Their config GUID identifies text ownership only.
            owner = Path.GetFileNameWithoutExtension(entry.ConfigFile.ConfigFilePath) ?? "";
            const string prefix = "com.meadworks.";
            return owner.StartsWith(prefix, StringComparison.Ordinal) && owner.Length > prefix.Length;
        }

        /// <summary>
        /// Without a provider tag: the owning plugin's registered translations under <see cref="SettingTextKeys"/>
        /// (cfg_section__key_name/_help, cfg_section_heading). A missing language uses the English entry, then the raw text.
        /// </summary>
        static ConfigPresentationText Conventional(ConfigEntryBase entry, string language, ConfigPresentationText fallback)
        {
            if (!TryPresentationOwner(entry, out var owner) || !SettingTranslations.TryGet(owner, out var translations)) return fallback;
            var section = entry.Definition.Section; var key = entry.Definition.Key; language = language ?? MeadworksLanguages.English;
            string Lookup(string? textKey, string raw)
            {
                if (textKey == null) return raw;
                var value = translations.Text(textKey, language);
                return string.Equals(value, textKey, StringComparison.Ordinal) ? raw : Prefer(value, raw);
            }
            return new ConfigPresentationText(Lookup(SettingTextKeys.Name(section, key), fallback.Label),
                Lookup(SettingTextKeys.Heading(section), fallback.Section), Lookup(SettingTextKeys.Help(section, key), fallback.Help));
        }
    }
}
