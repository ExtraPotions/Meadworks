// Delve - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: Delve's C05 catalog (tokens meadworks_delve_<key>, seven languages embedded, server overrides in config/Meadworks/delve/localization); same shape as Keel and Edge.
using System;
using System.Collections.Generic;
using System.IO;
using BepInEx;
using BepInEx.Logging;
using ExtraPotions.Meadworks.Crucible;
using ExtraPotions.Delve.Localization;

namespace ExtraPotions.Delve.Runtime
{
    public static class DelveText
    {
        static MeadworksTranslations? catalog;

        /// <summary>The product catalog (step-aside notices read it through Crucible).</summary>
        public static ProductTranslations? Translations { get; private set; }

        /// <summary>Server-side text overrides (design D6): <c>config/Meadworks/delve/localization/&lt;Language&gt;.json</c>.</summary>
        public static string OverridesDirectory => Path.Combine(Paths.ConfigPath, "Meadworks", "delve", "localization");

        public static void Initialize(BepInPlugin metadata, ManualLogSource log)
        {
            var json = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var assembly = typeof(DelveText).Assembly;
            foreach (var language in MeadworksLanguages.Supported)
                using (var stream = assembly.GetManifestResourceStream("Delve.localization." + language + ".json"))
                    if (stream != null) using (var reader = new StreamReader(stream)) json.Add(language, reader.ReadToEnd());
            Translations = new ProductTranslations(DelveCore.TokenNamespace, json);
            catalog = MeadworksTranslations.Register(metadata, Translations, OverridesDirectory, log);
        }

        public static string Text(string key) => DelveTextKeys.FromCatalog(key, catalog?.Text(key) ?? key);
        public static string Format(string key, params object[] arguments) => catalog?.Format(key, arguments) ?? key;

        /// <summary>The server language (Notices / Server Language) for plain-text notices to every client (design D5).</summary>
        public static string ServerLanguage => Plugin.Settings?.Get(ExtraPotions.Delve.Config.DelveSettings.Notices.ServerLanguage) ?? MeadworksLanguages.English;

        /// <summary>A catalog text in the server language, server overrides included; history templates keep their named tokens.</summary>
        public static string ServerText(string key) =>
            Translations == null ? key : DelveTextKeys.FromCatalog(key, Translations.Text(key, ServerLanguage));

        public static string ServerFormat(string key, params object[] arguments) =>
            Translations == null ? key : Translations.Format(key, ServerLanguage, arguments);
        public static void Tick() => catalog?.Update();

        public static void Shutdown()
        {
            catalog?.Dispose();
            catalog = null;
            Translations = null;
        }
    }
}
