// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using BepInEx;
using BepInEx.Logging;
using ExtraPotions.Meadworks.Crucible;

namespace ExtraPotions.Larder.Runtime {
    /// <summary>Larder's catalogs over the existing suite localization owner and language-change path.</summary>
    public static class LarderText {
        static MeadworksTranslations? catalog;
        /// <summary>The registered tables, for text in a language other than the current one (setting presentation).</summary>
        internal static ProductTranslations? Translations { get; private set; }
        public static void Initialize(BepInPlugin metadata,ManualLogSource log){
            var json=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
            var assembly=typeof(LarderText).Assembly;
            foreach(var language in MeadworksLanguages.Supported)
                using(var stream=assembly.GetManifestResourceStream("Larder.localization."+language+".json"))
                    if(stream!=null)using(var reader=new StreamReader(stream))json.Add(language,reader.ReadToEnd());
            var translations=new ProductTranslations("larder",json);
            catalog=MeadworksTranslations.Register(metadata,translations,log:log);
            Translations=translations;
        }
        public static string Text(string key)=>catalog?.Text(key)??key;
        public static string Format(string key,params object[] arguments)=>catalog?.Format(key,arguments)??key;
        public static string Token(string key)=>"$meadworks_larder_"+key;
        public static void Tick()=>catalog?.Update();
        public static void Shutdown(){catalog?.Dispose();catalog=null;Translations=null;}
    }
}
