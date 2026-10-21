// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Established Meadworks C05 catalog/language-change owner; no independent UI or translation service.
using System;
using System.Collections.Generic;
using System.IO;
using BepInEx;
using BepInEx.Logging;
using ExtraPotions.Meadworks.Crucible;
namespace ExtraPotions.Timber.Runtime
{
    public static class TimberText
    {
        static MeadworksTranslations? catalog;
        public static void Initialize(BepInPlugin metadata, ManualLogSource log)
        {
            var json = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var language in MeadworksLanguages.Supported)
                using (var stream = typeof(TimberText).Assembly.GetManifestResourceStream("Timber.localization." + language + ".json"))
                    if (stream != null) using (var reader = new StreamReader(stream)) json.Add(language, reader.ReadToEnd());
            catalog = MeadworksTranslations.Register(metadata, new ProductTranslations("timber", json), log: log);
        }
        public static string Text(string key) => catalog?.Text(key) ?? key;
        public static string Format(string key, params object[] values) => catalog?.Format(key, values) ?? key;
        /// <summary>The game localization token ("$meadworks_timber_key") for texts the game draws itself, such as a tab label.</summary>
        public static string Token(string key) => "$" + (catalog?.Token(key) ?? key);
        public static void Tick() => catalog?.Update();
        public static void Shutdown() { catalog?.Dispose(); catalog = null; }
    }
}
