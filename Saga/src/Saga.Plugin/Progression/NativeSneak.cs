// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
// Behavior reference: GrindstoneSkills 0.18 Soft Steps, Assassin and Vanish; existing Saga ownership and outgoing dispatch.
using System.Collections.Generic;
using ExtraPotions.Meadworks.Crucible;
using ExtraPotions.Saga.Skills;
using ExtraPotions.Saga.Runtime.Adopted.Grindstone;
using HarmonyLib;
using UnityEngine;

namespace ExtraPotions.Saga.Runtime {
    public static class NativeSneak {
        static bool Active(Player player)=>NativeDodging.Owned(player)&&NativeSaga.SneakEffectsActive;
        static float Level(Player player)=>MeadworksSkills.EffectiveLevel(player,global::Skills.SkillType.Sneak);

        const float VanishLevel=75f,VanishQuietTime=3f,VanishClearRadius=8f,VanishReach=40f,VanishCooldown=60f;
        static float vanishQuiet,vanishReadyAt=float.NegativeInfinity;

        public static void OnHit(Player player,Character target,HitData hit){
            if(Active(player))hit.m_backstabBonus=SneakRules.BackstabBonus(hit.m_backstabBonus,Level(player),target.GetBaseAI()!=null);
        }

        public static void Page(Player player,List<SkillPageLine> result){
            if(!Active(player))return;float level=Level(player);
            result.Add(new SkillPageLine(SagaText.Format("sneak_soft_steps",100*(1-SneakRules.NoiseRange(1,level)))));
            result.Add(new SkillPageLine(SagaText.Format("sneak_assassin",SneakRules.BackstabBonus(2,level,true)/2)));
            result.Add(new SkillPageLine(SagaText.Text(level>=VanishLevel?"sneak_vanish":"sneak_vanish_locked")));
        }

        [HarmonyPatch(typeof(Character),"RPC_AddNoise",new[]{typeof(long),typeof(float)})]
        static class Noise {
            static void Prefix(Character __instance,ref float range){
                if(__instance is Player player&&Active(player)&&(player.m_crouchToggled||player.IsCrouching()))range=SneakRules.NoiseRange(range,Level(player));
            }
        }

        [HarmonyPatch(typeof(Player),nameof(Player.Update))]
        static class PlayerUpdate {
            static void Postfix(Player __instance){
                if(!Active(__instance)||!ReferenceEquals(__instance,Player.m_localPlayer))return;
                if(!__instance.IsCrouching()||__instance.IsDead()||__instance.IsTeleporting()||Level(__instance)<VanishLevel||Time.time<vanishReadyAt){
                    vanishQuiet=0f;
                    return;
                }
                Vector3 pos=__instance.transform.position;
                float clearSqr=VanishClearRadius*VanishClearRadius;
                var all=BaseAI.BaseAIInstances;
                for(int i=0;i<all.Count;i++){
                    if(all[i] is MonsterAI m&&m.m_character!=null&&!m.m_character.IsDead()&&BaseAI.IsEnemy(__instance,m.m_character)){
                        if((m.transform.position-pos).sqrMagnitude<=clearSqr){
                            vanishQuiet=0f;
                            return;
                        }
                    }
                }
                vanishQuiet+=Time.deltaTime;
                if(vanishQuiet>=VanishQuietTime&&__instance.IsTargeted()){
                    float reachSqr=VanishReach*VanishReach;
                    int told=0;
                    for(int i=0;i<all.Count;i++){
                        if(all[i] is MonsterAI m&&m.m_character!=null&&!m.m_character.IsDead()&&!m.m_character.IsBoss()&&BaseAI.IsEnemy(__instance,m.m_character)){
                            if((m.transform.position-pos).sqrMagnitude<=reachSqr&&m.m_targetCreature==__instance){
                                m.SetAlerted(false);
                                m.m_targetCreature=null;
                                m.m_targetStatic=null;
                                m.m_timeSinceAttacking=0f;
                                m.m_updateTargetTimer=5f;
                                told++;
                            }
                        }
                    }
                    if(told>0){
                        vanishReadyAt=Time.time+VanishCooldown;
                        vanishQuiet=0f;
                        FloatingText.Show(__instance.GetTopPoint(),"Vanished");
                    }
                }
            }
        }
    }
}
