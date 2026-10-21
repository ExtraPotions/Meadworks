// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System;
namespace ExtraPotions.Saga.Skills {
    public static class JumpingRules {
        public const float RollLevel=50f;
        public static float CostShare(float level)=>1f-.5f*SkillCurve.Factor(level);
        public static float DamageShare(float level)=>1f-.5f*SkillCurve.Factor(level);
        public static float SafeHeight(float level)=>4f+4f*SkillCurve.Factor(level);
        public static float FallDamage(float height,float level)=>Math.Max(0f,Math.Min(1f,(height-SafeHeight(level))/16f))*100f*DamageShare(level);
        public static float FreeRollHeight(float level)=>2f*SafeHeight(level);
        public static bool Forward(float moveLength,float lookLengthSquared,float dot)=>moveLength>=.1f&&lookLengthSquared>=.0001f&&dot>=.5f;
        public static float ApplyFall(float nativeBase,float modified,float height,float level,bool rolled){
            if(nativeBase<=0f||modified<=0f)return modified;
            float replacement=FallDamage(height,level);
            if(rolled)replacement=height<=FreeRollHeight(level)?0f:replacement*.5f;
            return modified*(replacement/nativeBase);
        }
    }
}
