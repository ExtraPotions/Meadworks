// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
// Behavior reference: GrindstoneSkills 0.18 melee milestones; one outgoing native hit path implemented for Saga.
using System.Collections.Generic;
using ExtraPotions.Meadworks.Crucible;
using ExtraPotions.Saga.Skills;
using ExtraPotions.Saga.Runtime.Adopted.Grindstone;
using HarmonyLib;
namespace ExtraPotions.Saga.Runtime {
    public static class NativeMelee {
        static readonly AxeChain axes=new AxeChain();static Player? actor;
        static bool Active(Player player)=>NativeDodging.Owned(player)&&NativeSaga.Active("weaponmastery");
        public static void Tick(){var player=Player.m_localPlayer;if(!ReferenceEquals(actor,player)||!Active(player)){axes.Reset();actor=player;}}
        public static void Shutdown(){axes.Reset();actor=null;}
        static string? Key(global::Skills.SkillType skill){switch(skill){
            case global::Skills.SkillType.Swords:return "melee_swords";case global::Skills.SkillType.Knives:return "melee_knives";
            case global::Skills.SkillType.Clubs:return "melee_clubs";case global::Skills.SkillType.Polearms:return "melee_polearms";
            case global::Skills.SkillType.Spears:return "melee_spears";case global::Skills.SkillType.Axes:return "melee_axes";case global::Skills.SkillType.Unarmed:return "melee_unarmed";
            default:return null;
        }}
        public static void Page(Player player,global::Skills.SkillType skill,List<SkillPageLine> result){var key=Key(skill);if(key==null||!Active(player))return;bool unlocked=MeadworksSkills.EffectiveLevel(player,skill)>=MeleeRules.Level;result.Add(new SkillPageLine((unlocked?"":"<color=#888888>")+SagaText.Text(key)+(unlocked?"":"  "+SagaText.Format("level_required",MeleeRules.Level)+"</color>")));}
        [HarmonyPatch(typeof(Character),nameof(Character.Damage))]
        static class Outgoing {
            // Existing Defense Riposte remains its own prefix; Counter and poison compose after that modifier.
            [HarmonyPriority(Priority.Last)]
            static void Prefix(Character __instance,HitData hit){
                var player=Player.m_localPlayer;if(hit==null||!NativeDodging.Owned(player)||ReferenceEquals(__instance,player)||hit.GetAttacker()!=player)return;
                NativeSneak.OnHit(player,__instance,hit);
                NativeDodging.OnHit(player,hit);
                if(!Active(player)||Key(hit.m_skill)==null||!(MeadworksSkills.EffectiveLevel(player,hit.m_skill)>=MeleeRules.Level))return;
                switch(hit.m_skill){
                    case global::Skills.SkillType.Swords:hit.ApplyModifier(MeleeRules.SwordShare(__instance.IsStaggering()));break;
                    case global::Skills.SkillType.Knives:hit.m_damage.m_poison+=MeleeRules.KnifePoison(hit.m_damage.GetTotalDamage(),hit.m_damage.m_chop,hit.m_damage.m_pickaxe);break;
                    case global::Skills.SkillType.Clubs:hit.m_staggerMultiplier=MeleeRules.ClubStagger(hit.m_staggerMultiplier);break;
                    case global::Skills.SkillType.Polearms:hit.m_pushForce=MeleeRules.PolearmPush(hit.m_pushForce);break;
                    case global::Skills.SkillType.Spears:hit.ApplyModifier(MeleeRules.SpearShare(hit.m_ranged));break;
                    case global::Skills.SkillType.Axes:hit.ApplyModifier(axes.Hit(__instance.GetInstanceID(),UnityEngine.Time.time));break;
                    case global::Skills.SkillType.Unarmed:hit.ApplyModifier(MeleeRules.UnarmedShare);break;
                }
            }
        }
    }
}
