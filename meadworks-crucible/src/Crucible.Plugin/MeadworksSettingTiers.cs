// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using BepInEx.Bootstrap;
using BepInEx.Logging;

namespace ExtraPotions.Meadworks.Crucible {
    /// <summary>
    /// Native source for <see cref="SettingTiers"/>: a MeadWorks plugin's (or a Crucible-registered product's) embedded
    /// <c>&lt;AssemblyName&gt;.settings-tiers.json</c>. Settings bound through Charter live in the plugin's own Config, so
    /// they are found under the same plugin GUID. Other plugins never ship tiers and stay Advanced.
    /// </summary>
    internal static class MeadworksSettingTiers {
        static IDisposable? lease;

        internal static void Install(ProductRegistry services,ManualLogSource log){
            lease?.Dispose();
            lease=SettingTiers.Install((string guid,out string? json)=>Read(services,guid,out json),message=>log.LogWarning(message));
        }

        internal static void Uninstall(){lease?.Dispose();lease=null;}

        static bool Read(ProductRegistry services,string guid,out string? json){
            json=null;
            if(!guid.StartsWith("com.meadworks.",StringComparison.Ordinal)&&!services.TryGetMetadata(guid,out _))return true;
            if(!Chainloader.PluginInfos.TryGetValue(guid,out var info)||info?.Instance==null)return false; // Not loaded yet: ask again later.
            var assembly=info.Instance.GetType().Assembly;
            using(var stream=assembly.GetManifestResourceStream(SettingTierTable.ResourceName(assembly.GetName().Name))){
                if(stream==null)return true;
                using(var reader=new StreamReader(stream))json=reader.ReadToEnd();
            }
            return true;
        }
    }
}
