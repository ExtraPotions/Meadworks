// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;
using ExtraPotions.Meadworks.Crucible;
namespace ExtraPotions.Almanac.Runtime.Editing
{
    public readonly struct EntryDependencyState
    {
        public bool Visible { get; }
        public bool Enabled { get; }
        public string Reason { get; }
        internal EntryDependencyState(bool visible,bool enabled,string reason){Visible=visible;Enabled=enabled;Reason=reason;}
    }
    public static class EntryDependencies
    {
        static IEnumerable<ConfigDependency> Rules(ConfigEntryBase entry)
        {
            foreach(var rule in (entry.Description.Tags??Array.Empty<object>()).OfType<ConfigDependency>())yield return rule;
            // These are the actual KitLook parents, not a section-wide or third-party master-switch heuristic.
            if(MeadworksConfig.TryGetMetadata(entry,out var metadata) && metadata.ProductId=="com.meadworks.crucible.ui" && entry.Definition.Section=="Look")
            {
                string key=entry.Definition.Key;
                if(key=="Panel Theme"||key=="Timber Border Width"||key=="Timber Border Jaggedness"||key=="Night Shade")
                {
                    bool Parent(){return entry.ConfigFile.TryGetEntry<bool>(new ConfigDefinition("Look","Styled Panels"),out var parent) && parent.Value;}
                    yield return new ConfigDependency(Parent,()=>"Turn on Styled Panels to use this setting.");
                }
            }
        }
        public static EntryDependencyState Evaluate(ConfigEntryBase entry)
        {
            bool visible=true,enabled=true;string reason="";
            try
            {
                foreach(var rule in Rules(entry))
                {
                    bool shown=rule.Visible?.Invoke()??true;
                    bool usable=shown && rule.Enabled();
                    visible &= shown;enabled &= usable;
                    if(!usable && reason.Length==0)reason=rule.Reason()?.Trim()??"";
                }
                if(!enabled && reason.Length==0)reason="This setting depends on another setting.";
                return new EntryDependencyState(visible,enabled,reason);
            }
            catch(Exception error){return new EntryDependencyState(true,false,"Setting dependency unavailable: "+error.GetBaseException().Message);}
        }
    }
}
