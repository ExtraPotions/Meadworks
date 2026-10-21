// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using BepInEx;
using BepInEx.Logging;
using ExtraPotions.Meadworks.Crucible;

namespace Mantle.Accessories
{
    internal static class AccessoryText
    {
        static MeadworksTranslations catalog;
        static readonly Dictionary<string,string> messages = new Dictionary<string,string>(StringComparer.Ordinal);
        internal static void Initialize(BepInPlugin metadata, ManualLogSource log)
        {
            Shutdown();
            var tables = new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
            foreach (var language in MeadworksLanguages.Supported)
                using (var stream = typeof(AccessoryText).Assembly.GetManifestResourceStream("Mantle.localization."+language+".json"))
                    if (stream != null) using (var reader = new StreamReader(stream)) tables.Add(language,reader.ReadToEnd());
            catalog = MeadworksTranslations.Register(metadata,new ProductTranslations("mantle",tables),log:log);
            // Native planner refusals keep their established portable contract; this display adapter owns their C05 keys.
            foreach (var pair in catalog.Translations.Table("English")) messages[pair.Value] = pair.Key;
        }
        /// <summary>The language the catalogs answer in now (the game's, or the setting), or null before they are registered.</summary>
        internal static string Language => catalog?.Language;
        internal static string Key(string english) => messages.TryGetValue(english,out var key) ? key : english;
        internal static string Text(string key) => catalog?.Text(key) ?? key;
        internal static string Format(string key, params object[] args) => catalog?.Format(key,args) ?? key;
        internal static void Tick() => catalog?.Update();
        internal static void Shutdown() { catalog?.Dispose(); catalog=null; messages.Clear(); }
    }
}
