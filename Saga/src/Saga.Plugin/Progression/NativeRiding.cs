// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
// Behavior reference: GrindstoneSkills 0.18 Riding; use the native saddle's forwarded rider factor on the mount owner.
using System.Collections.Generic;
using ExtraPotions.Meadworks.Crucible;
using ExtraPotions.Saga.Skills;
using ExtraPotions.Saga.Runtime.Adopted.Grindstone;
using HarmonyLib;
namespace ExtraPotions.Saga.Runtime {
    public static class NativeRiding {
        static float Factor(Character mount){
            if(mount==null||mount.IsPlayer()||mount.IsDead()||mount.m_nview==null||!mount.m_nview.IsValid()||!mount.m_nview.IsOwner()||!mount.HaveRider()||mount.m_baseAI==null||!NativeSaga.Active("riding"))return 0f;
            return mount.m_baseAI.GetRiderSkill();
        }
        public static void Page(Player player,List<SkillPageLine> result){
            if(!NativeSaga.Active("riding"))return;float factor=SkillCurve.Factor(MeadworksSkills.EffectiveLevel(player,global::Skills.SkillType.Ride));
            result.Add(new SkillPageLine(SagaText.Format("riding_gallop",(100f*(RidingRules.RunMultiplier(factor)-1f)).ToString("0.#"))));
            result.Add(new SkillPageLine(SagaText.Format("riding_steady_seat",(100f*(1f-RidingRules.DamageShare(factor))).ToString("0.#"))));
        }
        [HarmonyPatch(typeof(Character),"GetRunSpeedFactor")]
        static class Gallop {static void Postfix(Character __instance,ref float __result)=>__result*=RidingRules.RunMultiplier(Factor(__instance));}
        [HarmonyPatch(typeof(Character),"RPC_Damage",new[]{typeof(long),typeof(HitData)})]
        static class SteadySeat {static void Prefix(Character __instance,HitData hit){if(hit==null)return;float share=RidingRules.DamageShare(Factor(__instance));if(share<1f)hit.m_damage.Modify(share);}}
    }
}
