// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
namespace ExtraPotions.Saga.Integration {
    public readonly struct SagaOverlap {
        public SagaOverlap(string module,string source,string name){Module=module;Source=source;Name=name;}
        public string Module {get;} public string Source {get;} public string Name {get;}
    }
    public static class SourceOverlap {
        public const string Powers="MidnightsFX.ProgressivePowers",Sneaker="blacks7ar.SNEAKer",Secondary="sighsorry.SecondaryAttacks",Grindstone="com.GrindstoneSkills";
        public static IReadOnlyList<SagaOverlap> Decide(ISet<string> installed,Func<string,string,bool?> sourceSwitch) {
            var list=new List<SagaOverlap>();
            if(installed.Contains(Powers))list.Add(new SagaOverlap("powers",Powers,"ProgressivePowers"));
            // Unreadable installed settings conservatively yield only the affected part; do not duplicate the source.
            if(installed.Contains(Sneaker)&&sourceSwitch(Sneaker,"Enable Mod")!=false)list.Add(new SagaOverlap("sneak_speed",Sneaker,"SNEAKer"));
            if(installed.Contains(Secondary))list.Add(new SagaOverlap("sneak_effects",Secondary,"SecondaryAttacks"));
            if(installed.Contains(Grindstone)) {
                foreach(var pair in new[]{("defense","Defense Enabled"),("woodcutting","Woodcutting Enabled"),("mining","Pickaxes Enabled"),("foraging","Foraging Enabled"),("fishing","Fishing Enabled"),("running","Running Enabled"),("jumping","Jumping Enabled"),("swimming","Swimming Enabled"),("riding","Riding Enabled"),("dodging","Dodging Enabled"),("crafting","Crafting Enabled")})
                    if(sourceSwitch(Grindstone,pair.Item2)!=false)list.Add(new SagaOverlap(pair.Item1,Grindstone,"GrindstoneSkills"));
                if(sourceSwitch(Grindstone,"Sneaking Enabled")!=false)
                    foreach(var module in new[]{"sneak_speed","sneak_effects"})
                        if(!list.Exists(overlap=>overlap.Module==module))list.Add(new SagaOverlap(module,Grindstone,"GrindstoneSkills"));
                if(sourceSwitch(Grindstone,"Catch Up")!=false)list.Add(new SagaOverlap("catchup",Grindstone,"GrindstoneSkills"));
                if(sourceSwitch(Grindstone,"Weapon Mastery Enabled")!=false)list.Add(new SagaOverlap("weaponmastery",Grindstone,"GrindstoneSkills"));
                list.Add(new SagaOverlap("progression",Grindstone,"GrindstoneSkills"));
            }
            return list;
        }
    }
}
