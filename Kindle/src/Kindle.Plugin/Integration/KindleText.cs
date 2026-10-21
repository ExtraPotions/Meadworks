// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
// English words adapted from OpenKeep RecipeWords, TrackerWords and SalvageWords.
// Portions Copyright (C) 2026 Gerald Glasgow, GPL-3.0-or-later.
// Reference ad278ecf323901c3645eb1f9f6fac6833a0f8cb1; Changed by expDARE, 2026: product tokens and Crucible catalog.
using System;
using System.Collections.Generic;
using System.IO;
using BepInEx;
using BepInEx.Logging;
using ExtraPotions.Meadworks.Crucible;
using ExtraPotions.Meadworks.Crucible.UI;

namespace ExtraPotions.Kindle.Runtime
{
    public static class KindleText
    {
        static MeadworksTranslations catalog;
        public static void Initialize(BepInPlugin metadata, ManualLogSource log)
        {
            var json = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var assembly = typeof(KindleText).Assembly;
            foreach (var language in MeadworksLanguages.Supported)
                using (var stream = assembly.GetManifestResourceStream("Kindle.localization." + language + ".json"))
                    if (stream != null) using (var reader = new StreamReader(stream)) json.Add(language, reader.ReadToEnd());
            catalog = MeadworksTranslations.Register(metadata, new ProductTranslations("kindle", json), log: log);
        }
        public static void Notice(string text, bool top = false)
        {
            if (MessageHud.instance == null) return;
            var source = Plugin.Source;
            if (source != null && source.IsActive && CrucibleUiPlugin.Active)
                KitNotices.Show(source, new KitNotice(text, top ? KitNoticeKind.TopLeft : KitNoticeKind.Center));
            else MessageHud.instance.ShowMessage(top ? MessageHud.MessageType.TopLeft : MessageHud.MessageType.Center, text);
        }

        public static string Text(string key) => catalog?.Text(key) ?? key;
        public static string Format(string key, params object[] arguments) => catalog?.Format(key, arguments) ?? key;
        /// <summary>Changes when the texts change (an override file applied or removed); caches of shown words compare it.</summary>
        public static long Revision => catalog?.Translations.Revision ?? -1;
        public static string Token(string key) => "$meadworks_kindle_" + key;
        public static void Tick() => catalog?.Update();
        public static void Shutdown() { catalog?.Dispose(); catalog = null; }
    }
}
