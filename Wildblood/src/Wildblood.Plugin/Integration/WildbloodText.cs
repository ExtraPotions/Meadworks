// Wildblood - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: Wildblood's C05 catalog (tokens meadworks_wildblood_<key>, seven languages embedded); same shape as Ember.
using System;
using System.Collections.Generic;
using System.IO;
using BepInEx;
using BepInEx.Logging;
using ExtraPotions.Meadworks.Crucible;
using ExtraPotions.Meadworks.Crucible.UI;

namespace ExtraPotions.Wildblood.Runtime
{
    public static class WildbloodText
    {
        static MeadworksTranslations? catalog;
        static string language = "";
        internal static int LanguageVersion { get; private set; }
        /// <summary>Changes when a translation override changes the catalog's texts.</summary>
        internal static long TextRevision => catalog?.Translations.Revision ?? 0;

        public static void Initialize(BepInPlugin metadata, ManualLogSource log)
        {
            var json = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var assembly = typeof(WildbloodText).Assembly;
            foreach (var language in MeadworksLanguages.Supported)
                using (var stream = assembly.GetManifestResourceStream("Wildblood.localization." + language + ".json"))
                    if (stream != null) using (var reader = new StreamReader(stream)) json.Add(language, reader.ReadToEnd());
            catalog = MeadworksTranslations.Register(metadata, new ProductTranslations(WildbloodCore.TokenNamespace, json), log: log);
        }

        public static string Text(string key) => catalog?.Text(key) ?? key;
        public static string Format(string key, params object[] arguments) => catalog?.Format(key, arguments) ?? key;
        public static void Tick()
        {
            catalog?.Update();
            string current = catalog?.Language ?? "English";
            if (current != language) { language = current; LanguageVersion++; }
        }
        public static void Shutdown() { catalog?.Dispose(); catalog = null; }

        /// <summary>A local top-left notice; no-op before the native HUD exists.</summary>
        public static void TopLeft(string text)
        {
            if (MessageHud.instance == null) return;
            var source = Plugin.Source;
            if (MeadworksHeadless.AllowsUi && source != null && source.IsActive && CrucibleUiPlugin.Active)
                KitNotices.Show(source, new KitNotice(text, KitNoticeKind.TopLeft));
            else MessageHud.instance.ShowMessage(MessageHud.MessageType.TopLeft, text);
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

    /// <summary>The game's random source for Wildblood.Core rolls (main thread, on the creature's owner).</summary>
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
