// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
// Source-exposed behavior reference: GrindstoneSkills Crafting at f331465b3ed2551c89decc42ba81565a7b689543; portable rules only.
using System;
using System.Collections.Generic;
using System.Globalization;
namespace ExtraPotions.Saga.Skills {
    public static class CraftingMarks {
        public const string MakerKey="grindstone_maker",BuilderKey="grindstone_builder";
        public const float JourneymanLevel=75f,ThriftyChanceAt100=0.10f;
        public static float Share(float level)=>.25f*RunningRules.Factor(level);
        static bool Finite(float value)=>!float.IsNaN(value)&&!float.IsInfinity(value);
        public static string FormatLevel(float level)=>(Finite(level)&&level>0?level:0).ToString("0.##",CultureInfo.InvariantCulture);
        public static float ReadMakerShare(IDictionary<string,string>? data)=>data!=null&&data.TryGetValue(MakerKey,out var text)&&float.TryParse(text,NumberStyles.Float,CultureInfo.InvariantCulture,out var level)?Share(level):0f;
        public static Dictionary<string,string> WithMakerLevel(IDictionary<string,string>? data,float level){
            // Replace dictionary identity so readers with per-dictionary parse caches see the new mark.
            var marked=data==null?new Dictionary<string,string>():new Dictionary<string,string>(data);
            marked[MakerKey]=FormatLevel(level);return marked;
        }
        // The caller retains the unmarked instance base; feeding an already marked maximum would compound it.
        public static float MaximumFromBase(float maximum,float level){
            if(!Finite(maximum)||maximum<=0)return maximum;
            float result=maximum*(1f+Share(level));return Finite(result)?result:maximum;
        }
        public static float HealthFraction(float maximum,float? storedHealth){
            if(!Finite(maximum)||maximum<=0)return 0;
            if(!storedHealth.HasValue)return 1;
            float health=storedHealth.Value;if(!Finite(health))return 0;
            return Math.Max(0f,Math.Min(1f,health/maximum));
        }
        public static float ThriftyChance(float level)=>ThriftyChanceAt100*RunningRules.Factor(level);
        public static bool RollThrifty(float level,float roll)=>Finite(roll)&&roll>=0&&roll<ThriftyChance(level);
        public static bool JourneymanUnlocked(float level)=>RunningRules.Factor(level)>=0.75f;
    }
}
