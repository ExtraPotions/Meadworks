// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
// Source-exposed behavior reference: GrindstoneSkills 0.18 Mastery/Ranged at fdd1e1ad1c0462adb42abf25572cbc8f0245635a; portable rules only.
using System;
namespace ExtraPotions.Saga.Skills {
    public static class RangedRules {
        public const float Level=50f,FreeArrowChanceAt100=.25f,EitrRegenBonus=.25f,HealthShare=.75f;
        public static bool Unlocked(float level)=>RunningRules.Factor(level)>=.5f;
        internal static bool Finite(float value)=>!float.IsNaN(value)&&!float.IsInfinity(value);
        public static float FreeArrowChance(float level)=>FreeArrowChanceAt100*RunningRules.Factor(level);
        public static bool FreeArrow(float level,float roll)=>Finite(roll)&&roll>=0&&roll<FreeArrowChance(level);
        public static float ExtraEitrRegen(float level,bool holdsElementalStaff)=>holdsElementalStaff&&Unlocked(level)?EitrRegenBonus:0f;
        public static float BloodHealthCost(float current,float level,bool trainsBloodMagic)=>current>0&&Finite(current)&&trainsBloodMagic&&Unlocked(level)?current*HealthShare:current;
    }
    public sealed class ReloadMemo {
        public const float Window=30f;
        object? weapon;float progress,at;
        public bool HasValue=>weapon!=null;
        public bool Remember(object? currentWeapon,float currentProgress,float now,float level,bool trainsCrossbows){
            if(currentWeapon==null||!trainsCrossbows||!RangedRules.Unlocked(level)||!RangedRules.Finite(currentProgress)||currentProgress<=0||!RangedRules.Finite(now))return false;
            weapon=currentWeapon;progress=currentProgress;at=now;return true;
        }
        public float Resume(object? queuedWeapon,float queuedProgress,float duration,float now,float level,bool trainsCrossbows){
            // A blocked queue attempt has no reload yet; retain the one pending weapon until a real queue or reset.
            if(queuedWeapon==null)return queuedProgress;
            var kept=weapon;float saved=progress,elapsed=now-at;Reset();
            if(kept==null||!ReferenceEquals(queuedWeapon,kept)||!trainsCrossbows||!RangedRules.Unlocked(level)||!RangedRules.Finite(now)||!RangedRules.Finite(elapsed)||elapsed<0||elapsed>Window||!RangedRules.Finite(queuedProgress)||queuedProgress>0||!RangedRules.Finite(duration)||duration<0)return queuedProgress;
            return Math.Min(saved,duration);
        }
        public void Reset(){weapon=null;progress=at=0;}
    }
}
