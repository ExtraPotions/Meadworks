// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
// Behavior reference: GrindstoneSkills 0.18 Dodging; native scopes and CS1 integration implemented for Saga.
using System;
using System.Collections.Generic;
using ExtraPotions.Meadworks.Crucible;
using ExtraPotions.Saga.Skills;
using ExtraPotions.Saga.Runtime.Adopted.Grindstone;
using HarmonyLib;
using UnityEngine;
namespace ExtraPotions.Saga.Runtime {
    public static class NativeDodging {
        static readonly RollWindow roll=new RollWindow();static readonly DodgeCounterWindow counter=new DodgeCounterWindow();
        static Player? actor,lifted;static bool counterCalled;
        public static bool Owned(Player player)=>player!=null&&ReferenceEquals(player,Player.m_localPlayer)&&player.m_nview!=null&&player.m_nview.IsValid()&&player.m_nview.IsOwner()&&!player.IsDead();
        static bool Active(Player player)=>Owned(player)&&NativeSaga.Active("dodging");
        static float Level(Player player)=>MeadworksSkills.EffectiveLevel(player,global::Skills.SkillType.Dodge);
        // InDodge is also owned by Edge dash; native animation state identifies this roll's shared flag.
        static bool NativeRoll(Player player)=>player!=null&&player.m_animator!=null&&(player.m_animator.GetBool(Player.s_animatorTagDodge)||player.GetNextOrCurrentAnimHash()==Player.s_animatorTagDodge);
        public static void Tick(){
            var player=Player.m_localPlayer;
            if(!ReferenceEquals(actor,player)||!Active(player)){Reset();actor=player;return;}
            if(roll.Expire(Time.time,!player.IsTeleporting(),NativeRoll(player)))player.m_dodgeInvincible=false;
        }
        static void Reset(){bool clear=roll.Reset(actor!=null&&NativeRoll(actor));if(clear&&actor!=null)actor.m_dodgeInvincible=false;counter.Reset();counterCalled=false;actor=null;}
        public static void Shutdown(){Reset();lifted=null;}
        public static void OnHit(Player player,HitData hit){if(!Active(player)||!counter.Applies(player.m_currentAttack,hit.m_ranged))return;hit.ApplyModifier(1.25f);if(!counterCalled)FloatingText.Show(hit.m_point,SagaText.Text("dodging_counter_callout"));counterCalled=true;}
        public static void Page(Player player,List<SkillPageLine> result){
            if(!Active(player))return;float level=Level(player);
            result.Add(new SkillPageLine(SagaText.Format("dodging_long_roll",(100*DodgingRules.ExtraShare(level)).ToString("0.#"))));
            if(roll.Last>0)result.Add(new SkillPageLine(SagaText.Format("dodging_last_roll",roll.Last.ToString("0.00"),(roll.Last*(1+DodgingRules.ExtraShare(level))).ToString("0.00"))));
            result.Add(new SkillPageLine(SagaText.Text(level>=DodgingRules.CounterLevel?"dodging_counter":"dodging_counter_locked")));
            result.Add(new SkillPageLine(SagaText.Text(level>=DodgingRules.HeavyLevel?"dodging_heavy":"dodging_heavy_locked")));
            if(level>=DodgingRules.HeavyLevel){float cost=player.GetDodgeStaminaUse();result.Add(new SkillPageLine(SagaText.Format("dodging_heavy_cost",(cost*2).ToString("0.#"),cost.ToString("0.#"))));}
        }
        struct Scope {public bool Entered,Pending;public Player? Previous;}
        static Scope Enter(Player player,bool pending){var state=new Scope{Entered=true,Pending=pending,Previous=lifted};if(pending&&Active(player)&&Level(player)>=DodgingRules.HeavyLevel&&player.IsEncumbered())lifted=player;return state;}
        static Exception? Leave(Scope state,Exception? error){if(state.Entered)lifted=state.Previous;return error;}
        [HarmonyPatch(typeof(Player),"Dodge",new[]{typeof(Vector3)})]
        static class Queue {
            // Edge keeps its own encumbered attack-cancel exclusion; only the following native body sees the lift.
            [HarmonyPriority(Priority.Last),HarmonyAfter("com.meadworks.edge")]
            static void Prefix(Player __instance,out Scope __state){__state=Enter(__instance,true);}
            [HarmonyPriority(Priority.First)]
            static Exception? Finalizer(Scope __state,Exception? __exception)=>Leave(__state,__exception);
        }
        [HarmonyPatch(typeof(Player),"UpdateDodge",new[]{typeof(float)})]
        static class Update {
            // A Quickstep replacement skips this stateful prefix and therefore never becomes Long Roll or Heavy Roll.
            [HarmonyPriority(Priority.Last),HarmonyAfter("com.meadworks.edge")]
            static void Prefix(Player __instance,float dt,out Scope __state){if(ReferenceEquals(__instance,Player.m_localPlayer))Tick();__state=Enter(__instance,__instance.m_queuedDodgeTimer-dt>0);}
            static void Postfix(Player __instance,Scope __state){if(__state.Entered&&__state.Pending&&Active(__instance)&&__instance.m_queuedDodgeTimer==0&&__instance.m_dodgeInvincible)roll.Start(Time.time);}
            [HarmonyPriority(Priority.First)]
            static Exception? Finalizer(Scope __state,Exception? __exception)=>Leave(__state,__exception);
        }
        [HarmonyPatch(typeof(Player),nameof(Player.IsEncumbered))]
        static class Load {static void Postfix(Player __instance,ref bool __result){if(ReferenceEquals(lifted,__instance))__result=false;}}
        [HarmonyPatch(typeof(Player),"GetDodgeStaminaUse")]
        static class Cost {
            [HarmonyPriority(Priority.Last)]
            static void Postfix(Player __instance,ref float __result){__result=DodgingRules.HeavyCost(__result,ReferenceEquals(lifted,__instance));}
        }
        [HarmonyPatch(typeof(Player),nameof(Player.OnDodgeMortal))]
        static class Mortal {
            static void Prefix(Player __instance,out bool __state){__state=__instance.m_dodgeInvincible;}
            static void Postfix(Player __instance,bool __state){if(ReferenceEquals(actor,__instance)&&Active(__instance)&&!__instance.IsTeleporting()&&NativeRoll(__instance)&&roll.Mortal(Time.time,Level(__instance),__state))__instance.m_dodgeInvincible=true;}
        }
        [HarmonyPatch(typeof(Player),"RPC_HitWhileDodging",new[]{typeof(long)})]
        static class Perfect {
            static void Prefix(Player __instance,out bool __state){__state=__instance.m_beenHitWhileDodging;}
            static void Postfix(Player __instance,bool __state){if(!__state&&__instance.m_beenHitWhileDodging&&Active(__instance))counter.Arm(Time.time,Level(__instance));}
        }
        [HarmonyPatch(typeof(Humanoid),nameof(Humanoid.StartAttack))]
        static class AttackStart {
            static void Postfix(Humanoid __instance,bool __result){
                if(!__result||!(__instance is Player player)||!Active(player))return;
                var attack=player.m_currentAttack;bool melee=attack!=null&&(attack.m_attackType==Attack.AttackType.Horizontal||attack.m_attackType==Attack.AttackType.Vertical||attack.m_attackType==Attack.AttackType.Area);
                counter.Started(attack,melee,Time.time);counterCalled=false;
            }
        }
    }
}
