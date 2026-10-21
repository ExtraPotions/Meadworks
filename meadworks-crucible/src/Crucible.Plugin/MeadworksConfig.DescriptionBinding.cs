// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System;
using BepInEx.Configuration;

namespace ExtraPotions.Meadworks.Crucible {
    public static partial class MeadworksConfig {
        public static ConfigEntry<T> Bind<T>(ConfigFile config,string section,string key,T defaultValue,ConfigDescription description)
            => Bind(config,section,key,defaultValue,description,ConfigScope.Local);

        public static ConfigEntry<T> Bind<T>(ConfigFile config,string section,string key,T defaultValue,ConfigDescription description,ConfigScope scope){
            if(description==null)throw new ArgumentNullException(nameof(description));
            if(!Enum.IsDefined(typeof(ConfigScope),scope))throw new ArgumentException("Unknown configuration scope.",nameof(scope));
            // Jotunn's string-only binder drops arbitrary tags; retain its local marker ahead of the original tags.
            var original=description.Tags??Array.Empty<object>();
            var tags=new object[original.Length+1];
            bool synced=scope!=ConfigScope.Local;
            tags[0]=new ConfigurationManagerAttributes{IsAdminOnly=synced};
            Array.Copy(original,0,tags,1,original.Length);
            var entry=config.Bind(section,key,defaultValue,new ConfigDescription(description.Description+(synced?" [Synced with Server]":" [Not Synced with Server]"),description.AcceptableValues,tags));
            RecordBinding(entry,MeadworksMischief.OwnerOf(config),scope,defaultValue);
            return entry;
        }
    }
}
