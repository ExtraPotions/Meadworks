// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Globalization;
using ExtraPotions.Saga.Conversion;
using BepInEx;
using BepInEx.Logging;
using ExtraPotions.Meadworks.Crucible;
namespace ExtraPotions.Saga.Runtime {
    public static class SagaText {
        static MeadworksTranslations? catalog;
        public static ProductTranslations Catalog {get;private set;}=null!;
        public static void Initialize(ManualLogSource log){var json=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);var assembly=typeof(SagaText).Assembly;
            foreach(var language in MeadworksLanguages.Supported)using(var stream=assembly.GetManifestResourceStream("Saga.localization."+language+".json")??throw new InvalidOperationException("Missing Saga language "+language))using(var reader=new StreamReader(stream))json.Add(language,reader.ReadToEnd());
            Catalog=new ProductTranslations("saga",json);catalog=MeadworksTranslations.Register(new BepInPlugin(Plugin.Guid,Plugin.Name,Plugin.Version),Catalog,log:log);
        }
        public static string Text(string key)=>catalog?.Text(key)??key;
        public static string Language=>catalog?.Language??MeadworksLanguages.English;
        public static string Format(string key,params object[] args)=>catalog?.Format(key,args)??key;
        public static string Packet(string key,params object[] args)=>CalloutText.Encode(key,args.Select(value=>Convert.ToString(value,CultureInfo.InvariantCulture)??"").ToArray());
        public static string Resolve(string text){if(!CalloutText.TryDecode(text,out var key,out var arguments))return text.StartsWith(CalloutText.Prefix,StringComparison.Ordinal)?"":text;return Format(key,arguments.Select(value=>(object)(GameLocalization.instance?.Localize(value)??value)).ToArray());}
        public static void Tick()=>catalog?.Update();
        public static void Shutdown(){catalog?.Dispose();catalog=null;}
    }
}
