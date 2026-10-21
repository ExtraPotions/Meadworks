// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System;
namespace ExtraPotions.Saga.Skills {
    public static class RunningRules {
        public const float BurdenLevel=50f,WindLevel=75f,WindStamina=30f,WindCooldown=180f;
        public static float Factor(float level)=>float.IsNaN(level)||float.IsInfinity(level)?0f:Math.Max(0f,Math.Min(1f,level/100f));
        public static float Carry(float level,float worldRate)=>50f*Factor(level)*worldRate;
        public static float EncumberedDrain(float current,float level)=>level>=BurdenLevel?current*.5f:current;
    }
    public sealed class RunningWind {
        object? actor;
        float readyAt=float.NegativeInfinity;
        public void Sync(object? current,bool active){if(!active||!ReferenceEquals(actor,current)){readyAt=float.NegativeInfinity;actor=active?current:null;}}
        public float Remaining(float now)=>Math.Max(0f,readyAt-now);
        public bool TryUse(float now,float level,bool hadStamina,bool hasStamina,bool running){
            if(actor==null||float.IsNaN(level)||float.IsInfinity(level)||level<RunningRules.WindLevel||!hadStamina||hasStamina||running||now<readyAt||float.IsNaN(now)||float.IsInfinity(now))return false;
            readyAt=now+RunningRules.WindCooldown;return true;
        }
    }
}
