// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System;
namespace ExtraPotions.Saga.Skills {
    public static class SwimmingRules {
        public static float WetTime(float duration,float level)=>duration>0f?duration*(1f-.5f*SkillCurve.Factor(level)):duration;
        public static float BreathDelay(float level)=>3f*SkillCurve.Factor(level);
        public static float DrowningShare(float level)=>1f-.5f*SkillCurve.Factor(level);
        public static float PickupReach(float range,float level)=>range>0f?range*(1f+SkillCurve.Factor(level)):range;
    }
    public sealed class BreathlessSpell {
        object? actor;
        float since=float.NegativeInfinity,last=float.NegativeInfinity;
        public void Sync(object? current,bool active){if(!active||!ReferenceEquals(actor,current)){actor=active?current:null;since=last=float.NegativeInfinity;}}
        public bool Hold(float now,float level,bool hasStamina){
            if(actor==null||float.IsNaN(now)||float.IsInfinity(now))return false;
            if(hasStamina){since=last=float.NegativeInfinity;return false;}
            if(now-last>1f)since=now;
            last=now;return now-since<SwimmingRules.BreathDelay(level);
        }
    }
    public static class RidingRules {
        static float Factor(float value)=>float.IsNaN(value)||float.IsInfinity(value)?0f:Math.Max(0f,Math.Min(1f,value));
        public static float RunMultiplier(float riderFactor)=>1f+.15f*Factor(riderFactor);
        public static float DamageShare(float riderFactor)=>1f-.25f*Factor(riderFactor);
    }
}
