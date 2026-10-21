// Adapted from SecondaryAttacks SneakVisibilitySystem.cs at 4375a2b17297052a773c5b143b0cae21dc655ed8.
// Copyright (C) sighsorry. SPDX-License-Identifier: GPL-3.0-only
// Changed by expDARE, 2026: Unity-free formulas, configured Saga speed and portable tick classification.
// Noise/backstab formulas implement source-exposed GrindstoneSkills 0.18 behavior; see docs/skills-sneak-local-2026-10-15.md.
using System;
namespace ExtraPotions.Saga.Skills {
    public enum SneakTick { Direct, Burning, Poison }
    public static class SneakRules {
        static float Clamp(float value,float min,float max)=>float.IsNaN(value)||float.IsInfinity(value)?min:Math.Max(min,Math.Min(max,value));
        public static float SpeedMultiplier(float level,float at100=1.5f)=>1f+(Clamp(at100,1f,2.5f)-1f)*Clamp(level/100f,0f,1f);
        public static float Visibility(float level,float light,float effect=2f) {
            light=Clamp(light,0,1);
            return Clamp(0.5f+light*0.5f-(0.3f+light*0.1f)*Clamp(level/100f,0,1)*Clamp(effect,1,2),0.1f,1f);
        }
        public static float NoiseRange(float range,float level) {
            if(!(range>0)||float.IsInfinity(range))return range;
            return range*(1f-.5f*Clamp(level/100f,0,1));
        }
        public static float BackstabBonus(float bonus,float level,bool hasAi) {
            if(!hasAi||!(bonus>1)||float.IsInfinity(bonus))return bonus;
            float result=bonus*(1f+.5f*Clamp(level/100f,0,1));
            return float.IsInfinity(result)?bonus:result;
        }
        public static bool KeepCrouching(SneakTick tick,bool hasAttacker,bool otherDamage,bool positiveTick,bool lethal,bool stagger,bool knockback)=>
            tick!=SneakTick.Direct&&!hasAttacker&&!otherDamage&&positiveTick&&!lethal&&!stagger&&!knockback;
    }
}
