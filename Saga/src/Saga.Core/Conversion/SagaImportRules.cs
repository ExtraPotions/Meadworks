// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using ExtraPotions.Saga.Config;
using ExtraPotions.Saga.Powers;
namespace ExtraPotions.Saga.Conversion {
    public static class SagaImportRules {
        static readonly Dictionary<string,string> sections=new Dictionary<string,string>(StringComparer.Ordinal) {
            ["6 - Death"]=SagaSettings.Progression.Section,["6 - Skills"]=SagaSettings.Progression.Section,
            ["10 - Woodcutting"]=SagaSettings.Woodcutting.Section,["11 - Felling"]=SagaSettings.Woodcutting.Felling,["12 - Chopping"]=SagaSettings.Woodcutting.Chopping,["13 - Finds"]=SagaSettings.Woodcutting.Finds,
            ["14 - Pickaxes"]=SagaSettings.Mining.Section,["15 - Seams"]=SagaSettings.Mining.Seams,["16 - Veins"]=SagaSettings.Mining.Veins,["17 - Pickaxe Perks"]=SagaSettings.Mining.Perks,["18 - Mine Finds"]=SagaSettings.Mining.Finds,
            ["19 - Defense"]=SagaSettings.Defense.Section,["20 - Defense Experience"]=SagaSettings.Defense.Experience,["21 - Defense Milestones"]=SagaSettings.Defense.Milestones,["22 - Defense Guard"]=SagaSettings.Defense.Guard,
            ["33 - Foraging"]=SagaSettings.Foraging.Section,["34 - Forage Perks"]=SagaSettings.Foraging.Perks,["50 - Fishing"]=SagaSettings.Fishing.Section,["51 - Fishing Fight"]=SagaSettings.Fishing.Fight,["52 - Bites"]=SagaSettings.Bites.Section,["53 - Big Fish"]=SagaSettings.Fishing.BigFish,["54 - Catch And Tackle"]=SagaSettings.Fishing.Tackle
        };
        public static SagaSetting? Target(string source,string section,string key) {
            if(source=="GrindstoneSkills") {
                if(section=="38 - Sneaking"&&key=="Sneaking Enabled")return SagaSettings.General.SneakEnabled;
                if(section=="40 - Dodging"&&key=="Dodging Enabled")return SagaSettings.General.DodgingEnabled;
                if(section=="39 - Swimming"&&key=="Swimming Enabled")return SagaSettings.General.SwimmingEnabled;
                if(section=="41 - Riding"&&key=="Riding Enabled")return SagaSettings.General.RidingEnabled;
                if(section=="37 - Jumping"&&key=="Jumping Enabled")return SagaSettings.General.JumpingEnabled;
                if(section=="36 - Running"&&key=="Running Enabled")return SagaSettings.General.RunningEnabled;
                if(section=="43 - Weapon Mastery"&&key=="Weapon Mastery Enabled")return SagaSettings.General.WeaponMasteryEnabled;
                if(section=="42 - Crafting"&&key=="Crafting Enabled")return SagaSettings.General.CraftingEnabled;
                if(key=="Pickaxes Enabled")return SagaSettings.General.MiningEnabled;
                if(key.EndsWith(" Enabled",StringComparison.Ordinal)&&sections.ContainsKey(section))return SagaSettings.Find(SagaSettings.General.Section+"."+key);
                return sections.TryGetValue(section,out var mapped)?SagaSettings.Find(mapped+"."+key):null;
            }
            if(source=="ProgressivePowers") {
                if(section=="General")return key=="Enable Advanced Power Config"?SagaSettings.Powers.AdvancedPowerConfig:SagaSettings.Find(SagaSettings.Powers.Section+"."+key);
                if(section=="Client config")return SagaSettings.Find(SagaSettings.Powers.Section+"."+key);
                foreach(Boss boss in Enum.GetValues(typeof(Boss)))if(section=="Power "+((int)boss+1)+" - "+PowerCatalog.DisplayName(boss))return SagaSettings.Find(PowerCatalog.Section(boss)+"."+key);
            }
            if(source=="SNEAKer") {
                if(section=="2- General")return key=="Enable Mod"?SagaSettings.General.SneakEnabled:key=="Max Sneak Speed"?SagaSettings.Sneak.SpeedAt100:null;
                if(section=="3- Skill Exp"&&key=="Display Exp Gained")return SagaSettings.Progression.SkillGainNotices;
            }
            if(source=="SecondaryAttacks") {
                if(key=="Sneak Movement Speed Skill Factor")return SagaSettings.Sneak.SpeedAt100;
                if(key=="Sneak Visibility Skill Effect Factor")return SagaSettings.Sneak.VisibilitySkillFactor;
                if(key=="Backstab Sneak Skill Raise Amount")return SagaSettings.Sneak.BackstabExperience;
                if(key=="Keep Crouching During Elemental Damage Over Time")return SagaSettings.Sneak.KeepCrouching;
            }
            return null;
        }
        public static string Normalize(string source,SagaSetting target,string text,float crouchSpeed=2f) {
            if(source=="SNEAKer"&&target==SagaSettings.Sneak.SpeedAt100) {
                if(!float.TryParse(text,System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,out var speed)||float.IsNaN(speed)||float.IsInfinity(speed)||crouchSpeed<=0||float.IsNaN(crouchSpeed)||float.IsInfinity(crouchSpeed))throw new FormatException("Invalid sneak speed");
                return Convert.ToString(target.NormalizeBoxed(speed/crouchSpeed),System.Globalization.CultureInfo.InvariantCulture)!;
            }
            if(source=="SNEAKer"&&target==SagaSettings.Progression.SkillGainNotices)return text=="On"?"SagaSkills":"Off";
            if(target.ValueType==typeof(bool))return text=="On"?"true":text=="Off"?"false":text;
            return text;
        }
        public static IReadOnlyDictionary<string,string> CharacterKeys {get;}=new Dictionary<string,string>{["grindstone_felled"]="saga.felled",["grindstone_mined"]="saga.mined",["grindstone_foraged"]="saga.foraged",["grindstone_fish_log"]="saga.fish_log",["grindstone_fish_records"]="saga.fish_records"};
    }
}
