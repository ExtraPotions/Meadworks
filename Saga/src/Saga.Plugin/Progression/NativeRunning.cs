// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
// Behavior reference: GrindstoneSkills 0.18 Running; implemented through Saga's existing native hooks and CS1 levels.
using System;
using ExtraPotions.Meadworks.Crucible;
using ExtraPotions.Saga.Skills;
using ExtraPotions.Saga.Runtime.Adopted.Grindstone;
using HarmonyLib;
using UnityEngine;
namespace ExtraPotions.Saga.Runtime {
    public static class NativeRunning {
        static readonly RunningWind wind=new RunningWind();
        static bool Local(Player player)=>player!=null&&ReferenceEquals(player,Player.m_localPlayer);
        static bool Active(Player player)=>Local(player)&&player.m_nview!=null&&player.m_nview.IsValid()&&player.m_nview.IsOwner()&&!player.IsDead()&&NativeSaga.Active("running");
        static float Level(Player player)=>MeadworksSkills.EffectiveLevel(player,global::Skills.SkillType.Run);
        public static void Tick(){var player=Player.m_localPlayer;wind.Sync(player,Active(player));}
        public static void Shutdown()=>wind.Sync(null,false);
        public static float ReadyIn=>wind.Remaining(Time.time);
        public static void Page(Player player,System.Collections.Generic.List<SkillPageLine> result){
            if(!Active(player))return;
            float level=Level(player);
            result.Add(new SkillPageLine(SagaText.Format("running_pack_mule",RunningRules.Carry(level,Game.m_carryWeightRate).ToString("0.#"))));
            result.Add(new SkillPageLine(SagaText.Text(level>=RunningRules.BurdenLevel?"running_burden":"running_burden_locked")));
            result.Add(new SkillPageLine(level>=RunningRules.WindLevel?SagaText.Format("running_wind",Mathf.CeilToInt(ReadyIn)):SagaText.Text("running_wind_locked")));
        }
        [HarmonyPatch(typeof(Player),nameof(Player.GetMaxCarryWeight))]
        static class Carry {
            static void Postfix(Player __instance,ref float __result){if(Active(__instance))__result+=RunningRules.Carry(Level(__instance),Game.m_carryWeightRate);}
        }
        [HarmonyPatch(typeof(Player),"UpdateStats",new[]{typeof(float)})]
        static class Encumbered {
            [HarmonyPriority(Priority.Last)]
            static void Prefix(Player __instance,out float? __state){
                __state=null;if(!Active(__instance))return;float level=Level(__instance);if(level<RunningRules.BurdenLevel)return;
                __state=__instance.m_encumberedStaminaDrain;__instance.m_encumberedStaminaDrain=RunningRules.EncumberedDrain(__state.Value,level);
            }
            // Restore inside other mods' scopes: last prefix, first finalizer.
            [HarmonyPriority(Priority.First)]
            static Exception? Finalizer(Player __instance,float? __state,Exception? __exception){if(__state.HasValue)__instance.m_encumberedStaminaDrain=__state.Value;return __exception;}
        }
        [HarmonyPatch(typeof(Player),"CheckRun",new[]{typeof(Vector3),typeof(float)})]
        static class Exhausted {
            static void Prefix(Player __instance,out bool __state){__state=false;if(!Local(__instance))return;bool active=Active(__instance);wind.Sync(__instance,active);__state=active&&__instance.HaveStamina();}
            static void Postfix(Player __instance,bool __state,ref bool __result){
                if(!__state||!Active(__instance))return;
                if(!wind.TryUse(Time.time,Level(__instance),__state,__instance.HaveStamina(),__result))return;
                __instance.AddStamina(RunningRules.WindStamina);__result=true;
                FloatingText.Show(__instance.GetTopPoint()+Vector3.up*.3f,SagaText.Text("running_second_wind"));
            }
        }
    }
}
