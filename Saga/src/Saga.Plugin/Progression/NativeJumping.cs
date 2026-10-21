// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
// Behavior reference: GrindstoneSkills 0.18 Jumping; native action and CS1 integration written for Saga.
using System;
using System.Collections.Generic;
using System.Reflection;
using ExtraPotions.Meadworks.Crucible;
using ExtraPotions.Saga.Skills;
using ExtraPotions.Saga.Runtime.Adopted.Grindstone;
using HarmonyLib;
using UnityEngine;
namespace ExtraPotions.Saga.Runtime {
    public static class NativeJumping {
        static readonly HashSet<Character> costScopes=new HashSet<Character>();
        static bool Active(Player player)=>player!=null&&ReferenceEquals(player,Player.m_localPlayer)&&player.m_nview!=null&&player.m_nview.IsValid()&&player.m_nview.IsOwner()&&!player.IsDead()&&NativeSaga.Active("jumping");
        static float Level(Player player)=>MeadworksSkills.EffectiveLevel(player,global::Skills.SkillType.Jump);
        public static void Page(Player player,List<SkillPageLine> result){
            if(!Active(player))return;float level=Level(player);
            result.Add(new SkillPageLine(SagaText.Format("jumping_light_feet",(100f*(1f-JumpingRules.CostShare(level))).ToString("0.#"))));
            result.Add(new SkillPageLine(SagaText.Format("jumping_soft_landing",JumpingRules.SafeHeight(level).ToString("0.#"),(100f*(1f-JumpingRules.DamageShare(level))).ToString("0.#"))));
            result.Add(new SkillPageLine(level>=JumpingRules.RollLevel?SagaText.Format("jumping_roll",JumpingRules.FreeRollHeight(level).ToString("0.#")):SagaText.Text("jumping_roll_locked")));
        }
        [HarmonyPatch]
        static class Cost {
            static IEnumerable<MethodBase> TargetMethods(){yield return AccessTools.Method(typeof(Character),nameof(Character.Jump),new[]{typeof(bool)});yield return AccessTools.Method(typeof(Character),nameof(Character.ForceJump),new[]{typeof(Vector3),typeof(bool)});}
            [HarmonyPriority(Priority.Last)]
            static void Prefix(Character __instance,out float? __state){
                __state=null;if(!(__instance is Player player)||!Active(player)||costScopes.Contains(__instance))return;
                float share=JumpingRules.CostShare(Level(player));if(share>=1f)return;
                __state=__instance.m_jumpStaminaUsage;costScopes.Add(__instance);__instance.m_jumpStaminaUsage=__state.Value*share;
            }
            // The outer Jump also calls ForceJump; only its owning scope lowers and restores the native field.
            [HarmonyPriority(Priority.First)]
            static Exception? Finalizer(Character __instance,float? __state,Exception? __exception){if(__state.HasValue){__instance.m_jumpStaminaUsage=__state.Value;costScopes.Remove(__instance);}return __exception;}
        }
        static bool Roll(Player player,float level){
            if(level<JumpingRules.RollLevel||player.InAttack()||player.InDodge()||player.IsStaggering()||player.IsKnockedBack()||player.IsEncumbered())return false;
            var move=player.m_moveDir;move.y=0;var look=player.m_lookDir;look.y=0;
            if(!JumpingRules.Forward(move.magnitude,look.sqrMagnitude,Vector3.Dot(move.normalized,look.normalized)))return false;
            player.ClearActionQueue();player.OnDodgeMortal();player.m_dodgeInvincibleCached=false;player.m_nview.GetZDO().Set(ZDOVars.s_dodgeinv,false);
            player.transform.rotation=Quaternion.LookRotation(move.normalized);player.m_body.rotation=player.transform.rotation;
            player.m_zanim.SetTrigger("dodge");player.AddNoise(5f);player.m_dodgeEffects.Create(player.transform.position,Quaternion.identity,player.transform,1f,-1,player.GetZDOID());return true;
        }
        [HarmonyPatch(typeof(SEMan),nameof(SEMan.ModifyFallDamage))]
        static class Landing {
            static void Postfix(SEMan __instance,float baseDamage,ref float damage){
                var player=Player.m_localPlayer;if(!Active(player)||!ReferenceEquals(__instance.m_character,player)||baseDamage<=0||damage<=0)return;
                float height=Mathf.Max(0,player.m_maxAirAltitude-player.transform.position.y),level=Level(player);
                bool rolled=JumpingRules.ApplyFall(baseDamage,damage,height,level,false)>0&&Roll(player,level);
                damage=JumpingRules.ApplyFall(baseDamage,damage,height,level,rolled);
            }
        }
    }
}
