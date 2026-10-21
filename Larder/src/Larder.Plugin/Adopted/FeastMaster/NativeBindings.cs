// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
// Native boundary only: the adopted source retains its binding/default behavior through Crucible C01.
using System;
using BepInEx.Configuration;
using ExtraPotions.Meadworks.Crucible;
namespace ExtraPotions.Larder.Runtime.Adopted.FeastMaster
{
    public sealed class NativeBindings
    {
        public ConfigFile Config { get; }
        public NativeBindings(ConfigFile config) => Config = config;
        /// <param name="presentation">Optional translated display text (<see cref="ConfigPresentation{T}"/>) for the per-item entries.</param>
        public ConfigEntry<T> Bind<T>(string section,string key,T value,string description,bool synced=true,AcceptableValueBase acceptableValues=null,object? presentation=null)
        {
            if(acceptableValues==null && typeof(T)==typeof(float)) acceptableValues=FloatRange(section,key);
            var scope=synced?ConfigScope.ServerAuthoritative:ConfigScope.Local;
            var entry=presentation==null
                ?MeadworksConfig.Bind(Config,section,key,value,description,scope,acceptableValues)
                :MeadworksConfig.Bind(Config,section,key,value,new ConfigDescription(description,acceptableValues,presentation),scope);
            NativeFeastMaster.Note(entry,Module(section));
            return entry;
        }
        internal static string Module(string section)
        {
            if(section=="2 - Foods"||section.StartsWith("Food: ",StringComparison.Ordinal)||section.StartsWith("Mead: ",StringComparison.Ordinal))return "Foods";
            if(section=="3 - Eating")return "Eating";
            if(section=="4 - Recovery")return "Recovery";
            if(section=="5 - Exertion")return "Exertion";
            if(section=="6 - Kitchen"||section.StartsWith("Station: ",StringComparison.Ordinal))return "Kitchen";
            return section=="9 - Cheats"?"Cheats":"Cooking";
        }
        static AcceptableValueRange<float> FloatRange(string section,string key)
        {
            float min=0,max=10;
            if(section.StartsWith("Food: ",StringComparison.Ordinal))
            {max=key=="Duration"?86400:key=="HealthRegen"?100:key=="Vigor"||key=="EitrVigor"?500:1000;min=key=="Duration"?1:0;}
            else if(section.StartsWith("Mead: ",StringComparison.Ordinal))
            {max=key=="Duration"?86400:key.EndsWith("OverTime",StringComparison.Ordinal)?1000:key.EndsWith("Modifier",StringComparison.Ordinal)?1:10;min=key=="Duration"?1:key.EndsWith("Modifier",StringComparison.Ordinal)?-1:0;}
            else if(section=="2 - Foods" && key=="Duration Modifier") min=.1f;
            else if(section=="3 - Eating") { max=key=="Degradation Curve"?5:1; }
            else if(section=="4 - Recovery")
            {
                if(key=="Vigor Per Stamina Point"||key=="Eitr Vigor Per Eitr Point"||key.EndsWith("Fraction",StringComparison.Ordinal)||key.EndsWith("Pivot",StringComparison.Ordinal))max=1;
                else if(key=="Regen Per Extra Stamina Point")max=5;
                else if(key=="Sneak Skill Regen Bonus")max=500;
                else if(key=="Swimming Regen Delay")max=60;
                else if(key.EndsWith("Delay",StringComparison.Ordinal))max=30;
                else if(key.StartsWith("Blocking",StringComparison.Ordinal))max=2;
                else if(key=="Rested Duration")max=86400;
                else if(key=="Rested Duration Per Comfort")max=3600;
                if(key.EndsWith("Curve Strength",StringComparison.Ordinal))min=.1f;
            }
            else if(section=="5 - Exertion")
            {
                if(key=="Base Health"||key=="Base Stamina"){min=1;max=1000;}
                else if(key.EndsWith("Skill Stamina",StringComparison.Ordinal))max=500;
                else if(key=="Skill Discount")max=100;
            }
            return new AcceptableValueRange<float>(min,max);
        }
    }
}
