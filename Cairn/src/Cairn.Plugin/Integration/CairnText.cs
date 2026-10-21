// Cairn - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: Cairn's C05 catalog (tokens meadworks_cairn_<key>, seven languages embedded); same shape as Edge.
using System;
using System.Collections.Generic;
using System.IO;
using BepInEx;
using BepInEx.Logging;
using ExtraPotions.Meadworks.Crucible;
using ExtraPotions.Meadworks.Crucible.UI;
using ExtraPotions.Cairn.Config;
using ExtraPotions.Cairn.Localization;
using ExtraPotions.Cairn.Visibility;
using System.Linq;

namespace ExtraPotions.Cairn.Runtime
{
    public static class CairnText
    {
        static MeadworksTranslations? catalog;

        /// <summary>The C05 catalog CS1 reads the Exploration skill's name and description from.</summary>
        public static ProductTranslations? Translations { get; private set; }

        public static void Initialize(BepInPlugin metadata, ManualLogSource log)
        {
            var json = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var assembly = typeof(CairnText).Assembly;
            foreach (var language in MeadworksLanguages.Supported)
                using (var stream = assembly.GetManifestResourceStream("Cairn.localization." + language + ".json"))
                    if (stream != null) using (var reader = new StreamReader(stream)) json.Add(language, reader.ReadToEnd());
            Translations = new ProductTranslations(CairnCore.TokenNamespace, json);
            catalog = MeadworksTranslations.Register(metadata, Translations, log: log);
        }

        /// <summary>The language the texts are in now, and the catalog's revision (a reloaded override): what the HUD's cached texts depend on.</summary>
        public static string Language => catalog?.Language ?? "";
        public static long Revision => Translations?.Revision ?? 0;
        public static string Text(string key) => catalog?.Text(key) ?? key;
        public static string Format(string key, params object[] arguments) => catalog?.Format(key, arguments) ?? key;
        public static string SettingName(CairnSetting setting)
        {
            if (setting.Section == CairnSettings.Visibility.Section || setting.Section == CairnSettings.VisibilityServer.Section)
            {
                var effect = VisibilityEffects.All.Single(e => setting.Key.EndsWith(e.Name, StringComparison.Ordinal));
                return Format(setting.Section == CairnSettings.Visibility.Section ? "setting_remove" : "setting_allow_removing", Text(CairnTextKeys.Effect(effect.Effect)));
            }
            return Text(CairnTextKeys.Setting(setting));
        }
        public static string SettingHelp(CairnSetting setting)
        {
            if (ReferenceEquals(setting, CairnSettings.Map.TidyPins)) return Text(CairnTextKeys.TidyPinsDescription);
            if (setting.Section == CairnSettings.Visibility.Section || setting.Section == CairnSettings.VisibilityServer.Section)
            {
                var effect = VisibilityEffects.All.Single(e => setting.Key.EndsWith(e.Name, StringComparison.Ordinal));
                return Format(setting.Section == CairnSettings.Visibility.Section ? "help_remove" : "help_allow_removing", Text(CairnTextKeys.Effect(effect.Effect)));
            }
            return Text(CairnTextKeys.SettingHelp(setting));
        }
        /// <summary>A readable name for a map pin type; a type the game adds later shows its own name.</summary>
        public static string PinTypeName(string type) => CairnTextKeys.PinTypes.Contains(type) ? Text(CairnTextKeys.PinType(type)) : type;
        public static void Tick() => catalog?.Update();
        public static void Shutdown() { catalog?.Dispose(); catalog = null; Translations = null; }

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
