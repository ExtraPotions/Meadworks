// Tempest - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: Tempest's C05 catalog (tokens meadworks_tempest_<key>, seven languages embedded); same shape as Keel and Edge.
using System;
using System.Collections.Generic;
using System.IO;
using BepInEx;
using BepInEx.Logging;
using ExtraPotions.Meadworks.Crucible;
using ExtraPotions.Meadworks.Crucible.UI;
using ExtraPotions.Tempest.Integration;

namespace ExtraPotions.Tempest.Runtime
{
    public static class TempestText
    {
        static MeadworksTranslations? catalog;

        /// <summary>The product catalog.</summary>
        public static ProductTranslations? Translations { get; private set; }

        public static void Initialize(BepInPlugin metadata, ManualLogSource log)
        {
            var json = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var assembly = typeof(TempestText).Assembly;
            foreach (var language in MeadworksLanguages.Supported)
                using (var stream = assembly.GetManifestResourceStream("Tempest.localization." + language + ".json"))
                    if (stream != null) using (var reader = new StreamReader(stream)) json.Add(language, reader.ReadToEnd());
            Translations = new ProductTranslations(TempestCore.TokenNamespace, json);
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

        /// <summary>
        /// A center message on a graphical client (kit notice when Crucible.UI is active); only a message-log entry while a
        /// player window is open; a log line on a headless process.
        /// </summary>
        public static void Centre(string text, MeadworksLog log)
        {
            var gameUi = MeadworksHeadless.AllowsUi && MessageHud.instance != null;
            var source = gameUi ? Plugin.Source : null;
            var kit = source != null && source.IsActive && CrucibleUiPlugin.Active;
            switch (NoticeRoutes.For(gameUi, kit, gameUi && TempestWindows.Open()))
            {
                case NoticeRoute.Log: log.Info(text); break;
                case NoticeRoute.Journal: MessageHud.instance.AddLog(text); break;
                case NoticeRoute.Kit: KitNotices.Show(source!, new KitNotice(text, KitNoticeKind.Center)); break;
                default: MessageHud.instance.ShowMessage(MessageHud.MessageType.Center, text); break;
            }
        }
    }
}