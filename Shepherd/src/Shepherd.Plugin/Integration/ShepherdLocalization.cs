// Shepherd - ExtraPotions Meadworks animal husbandry.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using BepInEx;
using BepInEx.Logging;
using ExtraPotions.Meadworks.Crucible;

namespace ExtraPotions.Shepherd.Runtime.Integration
{
    /// <summary>
    /// Shepherd's C05 catalog (design 13): the seven embedded <c>assets/localization/&lt;Language&gt;.json</c> tables under
    /// namespace <c>shepherd</c>, overrides under <c>BepInEx/config/Meadworks/shepherd/localization</c>, tokens
    /// <c>$meadworks_shepherd_&lt;key&gt;</c> mirrored into the game's localization (piece names use the tokens).
    /// </summary>
    public static class ShepherdLocalization
    {
        private static MeadworksTranslations? catalog;

        public static void Initialize(BepInPlugin metadata, ManualLogSource log)
        {
            Shutdown();
            var json = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var assembly = typeof(ShepherdLocalization).Assembly;
            foreach (var language in MeadworksLanguages.Supported)
                using (var stream = assembly.GetManifestResourceStream("Shepherd.localization." + language + ".json"))
                    if (stream != null)
                        using (var reader = new StreamReader(stream)) json.Add(language, reader.ReadToEnd());
            catalog = MeadworksTranslations.Register(metadata, new ProductTranslations(ShepherdText.Namespace, json), log: log);
        }

        public static string Text(string key) => catalog?.Text(key) ?? key;
        public static string Format(string key, params object[] arguments) => catalog?.Format(key, arguments) ?? key;
        public static string Token(string key) => ShepherdText.Token(key);
        public static void Tick() => catalog?.Update();

        public static void Shutdown()
        {
            catalog?.Dispose();
            catalog = null;
        }
    }
}
