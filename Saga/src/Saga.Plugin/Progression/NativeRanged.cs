// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
// Source-exposed behavior reference: GrindstoneSkills 0.18 BowRecover, SteadyReload, Attunement, LifeTap, ShieldXp, ShieldCaster.
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using ExtraPotions.Meadworks.Crucible;
using ExtraPotions.Saga.Skills;
using ExtraPotions.Saga.Runtime.Adopted.Grindstone;
using HarmonyLib;
using UnityEngine;

namespace ExtraPotions.Saga.Runtime {
    public static class NativeRanged {
        static bool Active(Player player)=>NativeDodging.Owned(player)&&NativeSaga.Active("weaponmastery");
        static float Level(Player player,global::Skills.SkillType skill)=>MeadworksSkills.EffectiveLevel(player,skill);
        static bool Trains(ItemDrop.ItemData? item,global::Skills.SkillType skill)=>item?.m_shared!=null&&item.m_shared.m_skillType==skill;

        // Bow Recover
        static bool sparing;

        [HarmonyPatch(typeof(Attack),nameof(Attack.UseAmmo))]
        static class BowShot {
            [HarmonyPrefix]
            static void Prefix(Attack __instance){
                if(__instance.m_character is Player player&&Active(player)&&Trains(__instance.m_weapon,global::Skills.SkillType.Bows)){
                    float level=Level(player,global::Skills.SkillType.Bows);
                    sparing=RangedRules.FreeArrow(level,UnityEngine.Random.value);
                }
            }
            [HarmonyFinalizer]
            static void Finalizer()=>sparing=false;
        }

        [HarmonyPatch(typeof(Inventory),nameof(Inventory.RemoveItem),new[]{typeof(ItemDrop.ItemData),typeof(int)})]
        static class BowTaken {
            [HarmonyPrefix]
            static bool Prefix(ref bool __result){
                if(!sparing)return true;
                sparing=false;__result=true;return false;
            }
        }

        // Steady Reload
        static readonly ReloadMemo reloadMemo=new ReloadMemo();

        [HarmonyPatch(typeof(Player),nameof(Player.ClearActionQueue))]
        static class CrossbowCleared {
            [HarmonyPrefix]
            static void Prefix(Player __instance){
                if(!Active(__instance)||__instance.m_actionQueue.Count==0)return;
                Player.MinorActionData? reload=null;
                for(int i=0;i<__instance.m_actionQueue.Count;i++){
                    if(__instance.m_actionQueue[i].m_type==Player.MinorActionData.ActionType.Reload){reload=__instance.m_actionQueue[i];break;}
                }
                if(reload!=null&&reload.m_time>0&&Trains(reload.m_item,global::Skills.SkillType.Crossbows)){
                    reloadMemo.Remember(reload.m_item,reload.m_time,Time.time,Level(__instance,global::Skills.SkillType.Crossbows),true);
                }
            }
        }

        [HarmonyPatch(typeof(Player),nameof(Player.QueueReloadAction))]
        static class CrossbowQueued {
            [HarmonyPostfix]
            static void Postfix(Player __instance){
                if(!reloadMemo.HasValue||!Active(__instance))return;
                Player.MinorActionData? reload=null;
                for(int i=0;i<__instance.m_actionQueue.Count;i++){
                    if(__instance.m_actionQueue[i].m_type==Player.MinorActionData.ActionType.Reload){reload=__instance.m_actionQueue[i];break;}
                }
                if(reload!=null){
                    reload.m_time=reloadMemo.Resume(reload.m_item,reload.m_time,reload.m_duration,Time.time,Level(__instance,global::Skills.SkillType.Crossbows),true);
                }
            }
        }

        // Attunement (Elemental Magic eitr regen)
        [HarmonyPatch(typeof(SEMan),nameof(SEMan.ModifyEitrRegen))]
        static class ElementalRegen {
            [HarmonyPrefix]
            static void Prefix(SEMan __instance,ref float eitrMultiplier){
                if(__instance.m_character is Player player&&Active(player)&&Trains(player.GetCurrentWeapon(),global::Skills.SkillType.ElementalMagic)){
                    eitrMultiplier+=RangedRules.ExtraEitrRegen(Level(player,global::Skills.SkillType.ElementalMagic),true);
                }
            }
        }

        // Life Tap (Blood Magic health cost)
        [HarmonyPatch(typeof(Attack),nameof(Attack.GetAttackHealth))]
        static class BloodHealthCost {
            [HarmonyPostfix]
            static void Postfix(Attack __instance,ref float __result){
                if(__result>0&&__instance.m_character is Player player&&Active(player)&&Trains(__instance.m_weapon,global::Skills.SkillType.BloodMagic)){
                    __result=RangedRules.BloodHealthCost(__result,Level(player,global::Skills.SkillType.BloodMagic),true);
                }
            }
        }

        // Shield Caster & Shield XP
        sealed class CasterRef { public ZDOID Id; }
        static readonly ConditionalWeakTable<SE_Shield,CasterRef> casters=new ConditionalWeakTable<SE_Shield,CasterRef>();

        [HarmonyPatch(typeof(StatusEffect),nameof(StatusEffect.SetAttacker))]
        static class ShieldHanded {
            [HarmonyPostfix]
            static void Postfix(StatusEffect __instance,Character attacker){
                if(__instance is SE_Shield shield&&attacker is Player player&&Active(player)){
                    casters.GetOrCreateValue(shield).Id=player.GetZDOID();
                }
            }
        }

        [HarmonyPatch(typeof(SE_Shield),nameof(SE_Shield.OnDamaged))]
        static class ShieldAbsorb {
            [HarmonyPrefix]
            static void Prefix(SE_Shield __instance,HitData? hit,Character? attacker){
                if(hit==null||attacker==null||attacker.IsPlayer()||attacker.IsTamed()||attacker==__instance.m_character)return;
                if(!casters.TryGetValue(__instance,out var c)||c.Id.IsNone())return;
                float absorbed=Mathf.Min(hit.GetTotalDamage(),__instance.m_totalAbsorbDamage-__instance.m_damage);
                if(absorbed>0){
                    Player local=Player.m_localPlayer;
                    if(local!=null&&local.GetZDOID()==c.Id&&Active(local)&&!local.IsDead()){
                        float xp=Mathf.Min(absorbed/40f,2f);
                        local.RaiseSkill(global::Skills.SkillType.BloodMagic,xp);
                    }
                }
            }
        }

        public static void Page(Player player,global::Skills.SkillType type,List<SkillPageLine> result){
            if(!Active(player))return;
            float level=Level(player,type);
            if(type==global::Skills.SkillType.Bows)result.Add(new SkillPageLine(SagaText.Format("ranged_bow_recover",25)));
            else if(type==global::Skills.SkillType.Crossbows)result.Add(new SkillPageLine(SagaText.Text(level>=50?"ranged_steady_reload":"ranged_steady_reload_locked")));
            else if(type==global::Skills.SkillType.ElementalMagic)result.Add(new SkillPageLine(SagaText.Format("ranged_eitr_regen",25)));
            else if(type==global::Skills.SkillType.BloodMagic){
                result.Add(new SkillPageLine(SagaText.Format("ranged_blood_cost",25)));
                result.Add(new SkillPageLine(SagaText.Text("ranged_shield_xp")));
            }
        }
    }
}
