// Magpie - ExtraPotions Meadworks loot handling.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using BepInEx;
using BepInEx.Logging;
using ExtraPotions.Magpie.Text;
using ExtraPotions.Meadworks.Crucible;

namespace ExtraPotions.Magpie.Runtime
{
    /// <summary>
    /// Magpie's C05 catalog: embedded <c>assets/localization/&lt;Language&gt;.json</c> for the seven suite languages,
    /// namespace <c>magpie</c>, player overrides under <c>BepInEx/config/Meadworks/magpie/localization</c> (Crucible default).
    /// </summary>
    internal static class MagpieText
    {
        private static MeadworksTranslations? catalog;

        public static void Initialize(BepInPlugin metadata, ManualLogSource log)
        {
            var json = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var assembly = typeof(MagpieText).Assembly;
            foreach (var language in MeadworksLanguages.Supported)
                using (var stream = assembly.GetManifestResourceStream("Magpie.localization." + language + ".json"))
                    if (stream != null)
                        using (var reader = new StreamReader(stream))
                            json.Add(language, reader.ReadToEnd());
            catalog = MeadworksTranslations.Register(metadata, new ProductTranslations(MagpieTextKeys.Namespace, json), log: log);
        }

        public static string Text(string key) => catalog?.Text(key) ?? key;

        public static void Tick() => catalog?.Update();

        public static void Shutdown()
        {
            catalog?.Dispose();
            catalog = null;
        }
    }
}
