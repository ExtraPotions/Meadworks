// Beacon - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: Beacon's C05 catalog (tokens meadworks_beacon_<key>, seven languages embedded); same shape as Thing and Tempest.
using System;
using System.Collections.Generic;
using System.IO;
using BepInEx;
using BepInEx.Logging;
using ExtraPotions.Meadworks.Crucible;
using ExtraPotions.Meadworks.Crucible.UI;

namespace ExtraPotions.Beacon.Runtime
{
    public static class BeaconText
    {
        static MeadworksTranslations? catalog;

        /// <summary>The product catalog.</summary>
        public static ProductTranslations? Translations { get; private set; }

        public static void Initialize(BepInPlugin metadata, ManualLogSource log)
        {
            var json = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var assembly = typeof(BeaconText).Assembly;
            foreach (var language in MeadworksLanguages.Supported)
                using (var stream = assembly.GetManifestResourceStream("Beacon.localization." + language + ".json"))
                    if (stream != null) using (var reader = new StreamReader(stream)) json.Add(language, reader.ReadToEnd());
            Translations = new ProductTranslations(BeaconCore.TokenNamespace, json);
            catalog = MeadworksTranslations.Register(metadata, Translations, log: log);
        }

        public static string Text(string key) => catalog?.Text(key) ?? key;
        public static string Format(string key, params object[] arguments) => catalog?.Format(key, arguments) ?? key;
        public static void Tick() => catalog?.Update();

        public static void Shutdown()
        {
            catalog?.Dispose();
            catalog = null;
            Translations = null;
        }

        /// <summary>A center message on a graphical client; a log line on a headless process.</summary>
        public static void Centre(string text, MeadworksLog log)
        {
            if (MeadworksHeadless.AllowsUi && MessageHud.instance != null)
            {
                var source = Plugin.Source;
                if (source != null && source.IsActive && CrucibleUiPlugin.Active)
                    KitNotices.Show(source, new KitNotice(text, KitNoticeKind.Center));
                else MessageHud.instance.ShowMessage(MessageHud.MessageType.Center, text);
            }
            else log.Info(text);
        }
    }
}