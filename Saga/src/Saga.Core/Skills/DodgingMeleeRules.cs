// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System;
namespace ExtraPotions.Saga.Skills {
    public static class DodgingRules {
        public const float CounterLevel=50f,HeavyLevel=75f;
        public static float ExtraShare(float level)=>.3f*RunningRules.Factor(level);
        public static float HeavyCost(float current,bool lifted)=>lifted?current*2f:current;
    }
    public sealed class RollWindow {
        float start=float.NegativeInfinity,until=float.NegativeInfinity;
        public float Last {get;private set;}
        public bool Extended=>until>float.NegativeInfinity;
        public void Start(float now){start=now;until=float.NegativeInfinity;}
        public bool Mortal(float now,float level,bool wasInvincible){
            float measured=now-start;start=float.NegativeInfinity;
            if(!wasInvincible||measured<=0||measured>1.5f||float.IsNaN(measured)||float.IsInfinity(measured))return false;
            Last=measured;float extra=measured*DodgingRules.ExtraShare(level);if(extra<=0)return false;
            until=now+extra;return true;
        }
        public bool Expire(float now,bool active,bool nativeRolling){
            if(now-start>1.5f)start=float.NegativeInfinity;
            if(!Extended||active&&nativeRolling&&now<until)return false;
            until=float.NegativeInfinity;return nativeRolling;
        }
        public bool Reset(bool nativeRolling=false){bool clear=Extended&&nativeRolling;start=until=float.NegativeInfinity;Last=0;return clear;}
    }
    public sealed class DodgeCounterWindow {
        float until=float.NegativeInfinity;object? chosen;
        public void Arm(float now,float level){if(level>=DodgingRules.CounterLevel)until=now+2f;}
        public void Started(object? attack,bool melee,float now){chosen=melee&&attack!=null&&now<until?attack:null;if(melee)until=float.NegativeInfinity;}
        public bool Applies(object? current,bool ranged)=>chosen!=null&&ReferenceEquals(chosen,current)&&!ranged;
        public void Reset(){until=float.NegativeInfinity;chosen=null;}
    }
    public static class MeleeRules {
        public const float Level=50f;
        public static float SwordShare(bool staggering)=>staggering?1.25f:1f;
        public static float KnifePoison(float total,float chop,float pickaxe)=>Math.Max(0,total-chop-pickaxe)*.2f;
        public static float ClubStagger(float current)=>current*1.3f<100f?current*1.3f:current;
        public static float PolearmPush(float current)=>current*1.3f;
        public static float SpearShare(bool ranged)=>ranged?1.25f:1f;
        public const float UnarmedShare=1.5f;
    }
    public sealed class AxeChain {
        readonly int[] targets=new int[8],steps=new int[8];readonly float[] times=new float[8];readonly bool[] used=new bool[8];
        public float Hit(int target,float now){
            int slot=-1,oldest=0;
            for(int i=0;i<targets.Length;i++){if(used[i]&&targets[i]==target){slot=i;break;}if(!used[i]){oldest=i;}else if(used[oldest]&&times[i]<times[oldest])oldest=i;}
            if(slot<0)slot=oldest;
            bool holds=used[slot]&&targets[slot]==target&&now>=times[slot]&&now-times[slot]<=3f;
            steps[slot]=holds?Math.Min(5,steps[slot]+1):0;used[slot]=true;targets[slot]=target;times[slot]=now;
            return 1f+.05f*steps[slot];
        }
        public void Reset(){Array.Clear(used,0,used.Length);}
    }
}
