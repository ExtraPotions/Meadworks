// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;
using ExtraPotions.Meadworks.Crucible;

namespace ExtraPotions.Almanac.Runtime.UI {
    internal enum PresetBindingFailure { Missing, Ambiguous, MixedFile, Metadata }
    internal sealed class PresetBindingException : InvalidOperationException {
        internal PresetBindingFailure Failure { get; }
        internal PresetBindingException(PresetBindingFailure failure,string key):base("Preset binding "+failure+": "+key){Failure=failure;}
    }
    internal sealed class AlmanacPresetChoice {
        internal string Product { get; }
        internal string Id { get; }
        internal string Label { get; }
        internal ConfigScope Scope { get; }
        internal IReadOnlyList<string> Keys { get; }
        readonly IReadOnlyDictionary<string,string> overrides;
        internal AlmanacPresetChoice(string product,string id,string label,ConfigScope scope,string[] keys,IReadOnlyDictionary<string,string> overrides){
            Product=product;Id=id;Label=label;Scope=scope;Keys=Array.AsReadOnly((string[])keys.Clone());this.overrides=overrides;
        }
        internal ConfigPresetPreview Preview(IEnumerable<ConfigEntryBase> candidates){
            var entries=candidates.ToArray();var values=new Dictionary<ConfigDefinition,object>();ConfigFile? file=null;
            foreach(string key in Keys){
                int split=key.IndexOf('/');var definition=new ConfigDefinition(key.Substring(0,split),key.Substring(split+1));
                var matches=entries.Where(entry=>entry.Definition.Equals(definition)).ToArray();
                if(matches.Length==0)throw new PresetBindingException(PresetBindingFailure.Missing,key);
                if(matches.Length!=1)throw new PresetBindingException(PresetBindingFailure.Ambiguous,key);
                var entry=matches[0];
                if(file!=null&&!ReferenceEquals(file,entry.ConfigFile))throw new PresetBindingException(PresetBindingFailure.MixedFile,key);
                if(!MeadworksConfig.TryGetMetadata(entry,out var metadata)||metadata.ProductId!=Product||metadata.Scope!=Scope||metadata.Visibility!=ConfigVisibility.Shown)
                    throw new PresetBindingException(PresetBindingFailure.Metadata,key);
                file=entry.ConfigFile;
                object value=overrides.TryGetValue(key,out var serialized)?TomlTypeConverter.ConvertToValue(serialized,entry.SettingType):entry.DefaultValue;
                values.Add(definition,value);
            }
            return ConfigPresetPreview.Capture(Product,Scope,file!,values);
        }
    }

    internal static class AlmanacPresetCatalog {
        static readonly IReadOnlyList<AlmanacPresetChoice> choices=Create();
        internal static IReadOnlyList<AlmanacPresetChoice> All=>choices;
        internal static IEnumerable<AlmanacPresetChoice> For(string product)=>choices.Where(choice=>choice.Product==product);
        static IReadOnlyList<AlmanacPresetChoice> Create(){
            var result=new List<AlmanacPresetChoice>();
            void Pair(string product,ConfigScope scope,string variant,string[] keys,params string[] changed){
                string owner="com.meadworks."+product;
                var changes=new Dictionary<string,string>(StringComparer.Ordinal);
                for(int i=0;i<changed.Length;i+=2)changes.Add(changed[i],changed[i+1]);
                if(keys.Distinct(StringComparer.Ordinal).Count()!=keys.Length||changes.Keys.Any(key=>!keys.Contains(key)))
                    throw new InvalidOperationException("Preset catalog contains an undeclared member.");
                result.Add(new AlmanacPresetChoice(owner,product+":defaults","Preset: Defaults",scope,keys,new Dictionary<string,string>()));
                result.Add(new AlmanacPresetChoice(owner,product+":variant",variant,scope,keys,changes));
            }
            const ConfigScope local=ConfigScope.Local,host=ConfigScope.ServerAuthoritative;
            Pair("almanac",local,"Preset: Minimal display",new[]{"Window/Window Scale","Window/Window Opacity","Display/Highlight Changes"},"Display/Highlight Changes","false");
            Pair("barter",local,"Preset: Minimal display",new[]{"Interface/Price Tips","Interface/Sell Panel","Interface/Show Purse"},
                "Interface/Price Tips","false","Interface/Sell Panel","false","Interface/Show Purse","false");
            Pair("beacon",local,"Preset: Reconnect",new[]{"Auto Join/Auto Join","Auto Join/Auto Join Countdown Seconds","Reconnect/Reconnect On Disconnect","Reconnect/Reconnect Attempts"},
                "Auto Join/Auto Join","false","Reconnect/Reconnect On Disconnect","true");
            var creatures=new[]{"2 - Crypt Mimic/Enabled","3 - Greydwarf Slinger/Enabled","4 - Rime Giant/Enabled","5 - Kraken/Enabled","8 - Skeleton Arsenal/Enabled",
                "25 - Crypt Executioner/Enabled","28 - Bone Ballista/Enabled","41 - Bog Hook/Enabled","42 - Fuling Javelineer/Enabled","43 - Slag Charred/Enabled","44 - Marrow Lantern/Enabled"};
            Pair("bestiary",host,"Preset: Disable listed features",creatures,creatures.SelectMany(key=>new[]{key,"false"}).ToArray());
            Pair("burrow",local,"Preset: Minimal display",new[]{"5a. Chest Preview/Show Item Icons","5a. Chest Preview/Show Quality","5a. Chest Preview/Sort Descending"},
                "5a. Chest Preview/Show Item Icons","false","5a. Chest Preview/Show Quality","false");
            Pair("cairn",local,"Preset: Minimal display",new[]{"4 - HUD/Show Day","4 - HUD/Show Time","4 - HUD/Show Weather","4 - HUD/Show Forecast","4 - HUD/Show Compass"},
                "4 - HUD/Show Day","false","4 - HUD/Show Weather","false","4 - HUD/Show Forecast","false");
            Pair("crucible.ui",local,"Preset: Quiet",new[]{"Notices/Stacked Notices","Notices/Duration","Notices/Maximum Visible"},"Notices/Maximum Visible","1");
            var respawns=new[]{"Chests/Chests","Bosses/Queen Return","Bosses/Kall Return","Creature Respawn/Camps","Creature Respawn/Dungeon Spawners"};
            Pair("delve",host,"Preset: Disable listed features",respawns,respawns.SelectMany(key=>new[]{key,"false"}).ToArray());
            Pair("inscribe",local,"Preset: Minimal display",new[]{"6 - Display/Colored Item Names","6 - Display/Tooltip Detail","7 - Ground Glow/Ground Glow","7 - Ground Glow/Loot Beam"},
                "6 - Display/Colored Item Names","false","6 - Display/Tooltip Detail","Compact","7 - Ground Glow/Ground Glow","false","7 - Ground Glow/Loot Beam","false");
            Pair("keel",local,"Preset: Minimal display",new[]{"Display/Ship Panel","Display/Speed Unit","Display/Boat Camera Max Distance","Display/Sail Opacity"},"Display/Ship Panel","false");
            Pair("kindle",local,"Preset: Minimal display",new[]{"Recipe List/Search","Recipe List/Favorites","Recipe List/Favorites First"},"Recipe List/Favorites","false","Recipe List/Favorites First","false");
            Pair("larder",local,"Preset: Detailed",new[]{"3 - Eating/Food Timers","6 - Kitchen/Fermenter Timer"},"3 - Eating/Food Timers","Always","6 - Kitchen/Fermenter Timer","true");
            Pair("magpie",local,"Preset: Quiet",new[]{"2 - Pickup/Pickup Range","3 - Messages/Show Pickup Messages"},"3 - Messages/Show Pickup Messages","false");
            Pair("shepherd",local,"Preset: Minimal display",new[]{"2 - Feeding/Show Food On Troughs","4 - Husbandry/Lore Detail","4 - Husbandry/Show Callouts"},
                "2 - Feeding/Show Food On Troughs","false","4 - Husbandry/Lore Detail","Compact","4 - Husbandry/Show Callouts","false");
            Pair("solstice",local,"Preset: Minimal display",new[]{"Visuals/World Tint","Visuals/Snow Cover","Visuals/Frozen Water","Visuals/Minimap Tint","Visuals/Season Indicator","Visuals/Heat Meter","Music/Seasonal Music"},
                "Visuals/Season Indicator","false","Visuals/Heat Meter","false","Music/Seasonal Music","false");
            Pair("tempest",local,"Preset: Quiet",new[]{"Notices/Warning Message","Notices/HUD Badge","Notices/Warning Sound"},"Notices/Warning Sound","false");
            Pair("thing",host,"Preset: Manual",new[]{"General/Admin List Editable In Game","Remote Configuration/Remote Config Editing"},
                "General/Admin List Editable In Game","false","Remote Configuration/Remote Config Editing","false");
            Pair("threshold",local,"Preset: Minimal display",new[]{"2 - Hubs/Return To Hub","5 - Map/Show Portal Pins"},"2 - Hubs/Return To Hub","Off","5 - Map/Show Portal Pins","false");
            Pair("timber",local,"Preset: Slower camera",new[]{"Build Camera/Speed","Build Camera/Run Multiplier"},"Build Camera/Speed","5","Build Camera/Run Multiplier","1");
            Pair("wayfarer",local,"Preset: Quiet",new[]{"General/CloseMap","General/ShowMessages","General/TombstoneHints"},"General/ShowMessages","false","General/TombstoneHints","false");
            return Array.AsReadOnly(result.ToArray());
        }
    }
}
