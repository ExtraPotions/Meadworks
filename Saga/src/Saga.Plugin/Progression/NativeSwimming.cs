// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
// Behavior reference: GrindstoneSkills 0.18 Swimming; native/CS1 ownership and lifecycle written for Saga.
using System;
using System.Collections.Generic;
using ExtraPotions.Meadworks.Crucible;
using ExtraPotions.Saga.Skills;
using ExtraPotions.Saga.Runtime.Adopted.Grindstone;
using HarmonyLib;
using UnityEngine;
namespace ExtraPotions.Saga.Runtime {
    public static class NativeSwimming {
        static readonly BreathlessSpell breath=new BreathlessSpell();
        static bool Active(Player player)=>player!=null&&ReferenceEquals(player,Player.m_localPlayer)&&player.m_nview!=null&&player.m_nview.IsValid()&&player.m_nview.IsOwner()&&!player.IsDead()&&NativeSaga.Active("swimming");
        static float Level(Player player)=>MeadworksSkills.EffectiveLevel(player,global::Skills.SkillType.Swim);
        public static void Tick(){var player=Player.m_localPlayer;breath.Sync(player,Active(player)&&player.IsSwimming());}
        public static void Shutdown()=>breath.Sync(null,false);
        public static void Page(Player player,List<SkillPageLine> result){
            if(!Active(player))return;float level=Level(player);
            result.Add(new SkillPageLine(SagaText.Format("swimming_quick_dry",(50f*SkillCurve.Factor(level)).ToString("0.#"))));
            result.Add(new SkillPageLine(SagaText.Format("swimming_long_breath",SwimmingRules.BreathDelay(level).ToString("0.#"),(50f*SkillCurve.Factor(level)).ToString("0.#"))));
            result.Add(new SkillPageLine(SagaText.Format("swimming_salvage",(1f+SkillCurve.Factor(level)).ToString("0.##"))));
        }
        [HarmonyPatch(typeof(SEMan),nameof(SEMan.AddStatusEffect),new[]{typeof(StatusEffect),typeof(bool),typeof(int),typeof(float),typeof(short)})]
        static class Wet {
            static void Postfix(SEMan __instance,StatusEffect statusEffect,StatusEffect __result){
                if(__result==null||ReferenceEquals(__result,statusEffect)||__result.NameHash()!=SEMan.s_statusEffectWet||!(__instance.m_character is Player player)||!Active(player))return;
                __result.m_ttl=SwimmingRules.WetTime(__result.m_ttl,Level(player));
            }
        }
        [HarmonyPatch(typeof(Player),"OnSwimming",new[]{typeof(Vector3),typeof(float)})]
        static class Swim {
            static void Prefix(Player __instance,float dt){
                if(!Active(__instance))return;breath.Sync(__instance,true);
                if(dt>0&&!float.IsInfinity(dt)&&breath.Hold(Time.time,Level(__instance),__instance.HaveStamina()))__instance.m_drownDamageTimer-=dt;
            }
        }
        [HarmonyPatch(typeof(Character),nameof(Character.Damage),new[]{typeof(HitData)})]
        static class Drowning {
            static void Prefix(Character __instance,HitData hit){if(hit!=null&&hit.m_hitType==HitData.HitType.Drowning&&__instance is Player player&&Active(player))hit.m_damage.Modify(SwimmingRules.DrowningShare(Level(player)));}
        }
        [HarmonyPatch(typeof(Player),"AutoPickup",new[]{typeof(float)})]
        static class Salvage {
            // Magpie replaces the native body in its prefix, so widen before it reads the same field.
            [HarmonyPriority(Priority.Last),HarmonyBefore("com.meadworks.magpie")]
            static void Prefix(Player __instance,out float? __state){
                __state=null;if(!Active(__instance)||!__instance.IsSwimming())return;
                float original=__instance.m_autoPickupRange,changed=SwimmingRules.PickupReach(original,Level(__instance));if(changed==original)return;
                __state=original;__instance.m_autoPickupRange=changed;
            }
            [HarmonyPriority(Priority.First)]
            static Exception? Finalizer(Player __instance,float? __state,Exception? __exception){if(__state.HasValue)__instance.m_autoPickupRange=__state.Value;return __exception;}
        }
    }
}
