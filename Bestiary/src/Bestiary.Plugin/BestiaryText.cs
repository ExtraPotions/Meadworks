// Bestiary - ExtraPotions Meadworks creatures and bone arsenal.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using BepInEx;
using BepInEx.Logging;
using ExtraPotions.Bestiary.Text;
using ExtraPotions.Meadworks.Crucible;

namespace ExtraPotions.Bestiary.Runtime
{
    /// <summary>
    /// Bestiary's C05 catalog: embedded <c>assets/localization/&lt;Language&gt;.json</c> for the seven suite languages,
    /// namespace <c>bestiary</c>, player overrides under <c>BepInEx/config/Meadworks/bestiary/localization</c> (Crucible default).
    /// </summary>
    internal static class BestiaryText
    {
        private static MeadworksTranslations? catalog;

        public static void Initialize(BepInPlugin metadata, ManualLogSource log)
        {
            var json = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var assembly = typeof(BestiaryText).Assembly;
            foreach (var language in MeadworksLanguages.Supported)
                using (var stream = assembly.GetManifestResourceStream("Bestiary.localization." + language + ".json"))
                    if (stream != null)
                        using (var reader = new StreamReader(stream))
                            json.Add(language, reader.ReadToEnd());
            catalog = MeadworksTranslations.Register(metadata, new ProductTranslations(BestiaryTextKeys.Namespace, json), log: log);
        }

        public static string Text(string key) => catalog?.Text(key) ?? key;

        // SetupLanguage can run before the singleton constructor completes. Use the supplied language directly.
        public static string TextForLanguage(string key, string language) =>
            catalog?.Translations.Text(key, MeadworksLanguages.Normalize(language)) ?? key;

        public static void Tick() => catalog?.Update();

        public static void Shutdown()
        {
            catalog?.Dispose();
            catalog = null;
        }
    }
}
