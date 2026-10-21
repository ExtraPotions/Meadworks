// Burrow - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: the stow button row's per-module step-aside (C09) and Burrow's C05 catalog (seven languages embedded).
using System;
using System.Collections.Generic;
using System.IO;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Logging;
using ExtraPotions.Burrow;
using ExtraPotions.Meadworks.Crucible;
using CruciblePlugin = ExtraPotions.Meadworks.Crucible.Plugin;

namespace Burrow.Integration
{
    /// <summary>
    /// While a third-party stack/sort mod is loaded (<see cref="StowRowStepAside.Sources"/>), Burrow adds no stow button
    /// row and ignores the row's hotkeys; every other Burrow feature keeps running. Decided once, the first time it is
    /// asked (the inventory's Awake, after every plugin has loaded), and reported once through Crucible's per-module
    /// step-aside with the text from Burrow's catalog.
    /// </summary>
    public static class StowRowYield
    {
        private const string TranslationProduct = "burrow";
        private static MeadworksTranslations catalog;
        private static bool decided;
        private static bool reported;
        private static StowRowSource source;

        public static void Initialize(BepInPlugin metadata, ManualLogSource log)
        {
            var json = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var assembly = typeof(StowRowYield).Assembly;
            foreach (string language in MeadworksLanguages.Supported)
                using (Stream stream = assembly.GetManifestResourceStream("Burrow.localization." + language + ".json"))
                    if (stream != null) using (var reader = new StreamReader(stream)) json.Add(language, reader.ReadToEnd());
            catalog = MeadworksTranslations.Register(metadata, new ProductTranslations(TranslationProduct, json), log: log);
        }

        /// <summary>A text from Burrow's catalog in the game language; the key itself before the catalog loads.</summary>
        public static string Text(string key) => catalog?.Text(key) ?? key;

        public static void Tick() => catalog?.Update();

        public static void Shutdown()
        {
            catalog?.Dispose();
            catalog = null;
            decided = false;
            reported = false;
            source = null;
            reportedModules.Clear();
        }

        /// <summary>True while the row and its hotkeys stay off because a stack/sort mod is loaded.</summary>
        public static bool SteppedAside
        {
            get
            {
                if (!decided)
                {
                    decided = true;
                    source = StowRowStepAside.Match(Chainloader.PluginInfos.Keys);
                }
                return source != null;
            }
        }

        /// <summary>
        /// Reports the step-aside once to the step-aside registry (Almanac diagnostics) and the log. No HUD notice: the
        /// row simply is not there, and the reason stays in the log and in Almanac.
        /// </summary>
        public static void Report()
        {
            if (reported || !SteppedAside)
                return;
            reported = true;
            ReportModule(source, StowRowStepAside.ModuleId, StowRowStepAside.TextKey, hudNotice: false);
        }

        private static readonly HashSet<string> reportedModules = new HashSet<string>(StringComparer.Ordinal);

        /// <summary>Reports one Burrow module's step-aside once per session, with the text from Burrow's catalog.</summary>
        internal static void ReportModule(StowRowSource from, string moduleId, string textKey, bool hudNotice = true)
        {
            if (from == null || !reportedModules.Add(moduleId))
                return;
            var mod = new SourceMod(from.Guid, from.Name);
            string? translations = catalog != null ? TranslationProduct : null;
            if (hudNotice)
            {
                MeadworksStepAside.ReportModule(Plugin.PluginGuid, moduleId, mod, translations, textKey);
                return;
            }
            var aside = new ModuleStepAside(Plugin.PluginGuid, moduleId, mod, textKey);
            CruciblePlugin.Services.StepAside.ReportModule(aside);
            Plugin.Log?.LogInfo(MeadworksStepAside.ModuleNotice(aside, translations));
        }
    }
}
