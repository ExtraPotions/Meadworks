// Inscribe - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: Inscribe's C05 catalog (tokens meadworks_inscribe_<key>, seven languages embedded); same shape as Wildblood.
using System;
using System.Collections.Generic;
using System.IO;
using BepInEx;
using BepInEx.Logging;
using ExtraPotions.Meadworks.Crucible;
using ExtraPotions.Meadworks.Crucible.UI;

namespace ExtraPotions.Inscribe.Runtime
{
    public static class InscribeText
    {
        static MeadworksTranslations? catalog;

        public static void Initialize(BepInPlugin metadata, ManualLogSource log)
        {
            var json = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var assembly = typeof(InscribeText).Assembly;
            foreach (var language in MeadworksLanguages.Supported)
                using (var stream = assembly.GetManifestResourceStream("Inscribe.localization." + language + ".json"))
                    if (stream != null) using (var reader = new StreamReader(stream)) json.Add(language, reader.ReadToEnd());
            catalog = MeadworksTranslations.Register(metadata, new ProductTranslations(InscribeCore.TokenNamespace, json), log: log);
        }

        public static string Text(string key) => catalog?.Text(key) ?? key;
        internal static long Revision => catalog?.Translations.Revision ?? 0;
        /// <summary>Localization setup already supplies its language; do not resolve the initializing singleton.</summary>
        public static string TextForLanguage(string key, string language) =>
            catalog?.Translations.Text(key, MeadworksLanguages.Normalize(language)) ?? key;
        public static string Format(string key, params object[] arguments) => catalog?.Format(key, arguments) ?? key;
        public static void Tick() => catalog?.Update();
        public static void Shutdown() { catalog?.Dispose(); catalog = null; }

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

    /// <summary>The game's random source for Inscribe.Core rolls (main thread, on the client or owner that rolls).</summary>
    public sealed class UnityDice : IDice
    {
        public static readonly UnityDice Instance = new UnityDice();

        private UnityDice() { }

        public float Value()
        {
            var v = UnityEngine.Random.value;
            return v >= 1f ? 0.99999994f : v;
        }
    }
}
