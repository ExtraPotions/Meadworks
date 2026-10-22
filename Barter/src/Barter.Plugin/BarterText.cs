// Barter - ExtraPotions Meadworks trader overhaul.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using BepInEx;
using BepInEx.Logging;
using ExtraPotions.Meadworks.Crucible;

namespace ExtraPotions.Barter.Runtime
{
    /// <summary>
    /// Barter's text in the game language: the seven built-in catalogs (English keys, the other six languages are translated
    /// "[en] " placeholders) through Crucible's translation service, which also serves overrides from the config folder.
    /// </summary>
    public static class BarterText
    {
        private static MeadworksTranslations catalog;

        public static void Initialize(BepInPlugin metadata, ManualLogSource log)
        {
            var json = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var assembly = typeof(BarterText).Assembly;
            foreach (var language in MeadworksLanguages.Supported)
                using (var stream = assembly.GetManifestResourceStream("Barter.localization." + language + ".json"))
                    if (stream != null)
                        using (var reader = new StreamReader(stream))
                            json.Add(language, reader.ReadToEnd());
            catalog = MeadworksTranslations.Register(metadata, new ProductTranslations("barter", json), log: log);
        }

        public static string Text(string key) => catalog?.Text(key) ?? key;

        public static string Format(string key, params object[] arguments) => catalog?.Format(key, arguments) ?? key;

        /// <summary>The Jotunn token of a key ("$meadworks_barter_" + key) for text the game localizes on the receiving peer.</summary>
        public static string Token(string key) => "$" + (catalog?.Token(key) ?? "meadworks_barter_" + key);

        public static void Tick() => catalog?.Update();

        public static void Shutdown()
        {
            catalog?.Dispose();
            catalog = null;
        }
    }
}
