// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
// Independently implemented from the documented GrindstoneSkills 0.18 recovery behavior; no upstream XP hook copied.
using System;
using System.Collections.Generic;
using System.Linq;
using ExtraPotions.Meadworks.Crucible;
using ExtraPotions.Saga.Config;
using ExtraPotions.Saga.Progression;
using ExtraPotions.Saga.Skills;
using ExtraPotions.Saga.Runtime.Adopted.Grindstone;
using HarmonyLib;
namespace ExtraPotions.Saga.Runtime {
    public static class RecoveryXp {
        static readonly RecoveryProviders providers=new RecoveryProviders();
        static Player? owner;
        static SkillRecovery history=new SkillRecovery();
        static long revision=-1;
        static bool Local(Player player)=>player!=null&&player==Player.m_localPlayer;
        static bool Local(global::Skills skills)=>Player.m_localPlayer!=null&&ReferenceEquals(skills,Player.m_localPlayer.GetSkills());
        static void Save(Player player)=>player.m_customData[SkillRecovery.Key]=history.Save();
        static Player? rejected;
        public static bool Take(Player player){
            if(!Local(player)||ReferenceEquals(rejected,player))return false;
            if(ReferenceEquals(owner,player))return true;
            try{
                var aliases=new Dictionary<int,int>();
                foreach(var definition in MeadworksSkills.Registry.All)foreach(var identity in definition.ImportFrom){
                    int source=SkillId.FromIdentity(identity).Value;
                    if(aliases.TryGetValue(source,out var previous)&&previous!=definition.Id.Value)throw new InvalidOperationException("Ambiguous recovery history alias.");
                    aliases[source]=definition.Id.Value;
                }
                var next=SkillRecovery.Load(player.m_customData,player.GetSkills().m_skillData.Select(p=>new KeyValuePair<int,float>((int)p.Key,p.Value.m_level)),aliases);
                history=next;owner=player;rejected=null;return true;
            }catch(Exception error){rejected=player;owner=null;NativeSaga.Report(error,"recovery history preserved; recovery disabled for this actor");return false;}
        }
        public static void NoteCurrent(Player player){if(!Take(player))return;bool changed=false;foreach(var skill in player.GetSkills().m_skillData)changed|=history.Note((int)skill.Key,skill.Value.m_level);if(changed)Save(player);}
        static float Base(global::Skills skills,int id)=>skills.m_skillData.TryGetValue((global::Skills.SkillType)id,out var skill)?skill.m_level:0;
        public static float Rate(Player player,global::Skills.SkillType type){
            if(!Take(player))return 1;
            var skills=player.GetSkills();int highest=0;
            bool switching=NativeSaga.Active("weaponmastery")&&Plugin.Settings!.Get(SagaSettings.Progression.WeaponSwitch);
            if(switching&&SkillRecovery.IsWeapon((int)type))foreach(var skill in skills.m_skillData)if(SkillRecovery.IsWeapon((int)skill.Key))highest=Math.Max(highest,SkillRecovery.Base(skill.Value.m_level));
            return history.Rate((int)type,Base(skills,(int)type),highest,NativeSaga.Active("catchup")&&Plugin.Settings!.Get(SagaSettings.Progression.CatchUp),switching);
        }
        static float Scale(Player player,int id,float factor){
            var type=(global::Skills.SkillType)id;
            if(type==global::Skills.SkillType.WoodCutting)factor=SwingScope.ScaleFor(player,factor);
            else if(type==global::Skills.SkillType.Pickaxes)factor=MineSwing.ScaleFor(player,factor);
            else if(type==global::Skills.SkillType.Fishing)factor=FloatScope.ScaleFor(player,factor);
            else if(id==SagaSkills.ForagingType)factor=ForageXp.ScaleFor(player,factor);
            return factor*Rate(player,type);
        }
        public static void Tick(){
            long current=MeadworksSkills.Registry.Revision;
            if(revision!=current){
                var ids=Enum.GetValues(typeof(global::Skills.SkillType)).Cast<global::Skills.SkillType>().Where(t=>t!=global::Skills.SkillType.None&&t!=global::Skills.SkillType.All).Select(t=>(int)t).Concat(MeadworksSkills.Registry.All.Select(d=>d.Id.Value));
                providers.Reconcile(ids,id=>MeadworksSkills.RegisterExperience(Plugin.Guid,(global::Skills.SkillType)id,(player,factor)=>Scale(player,id,factor)));revision=current;
            }
            var local=Player.m_localPlayer;if(local!=null)Take(local);else {owner=null;rejected=null;}
        }
        public static void Shutdown(){providers.Dispose();revision=-1;owner=null;rejected=null;history=new SkillRecovery();}
        [HarmonyPatch(typeof(Player),nameof(Player.OnSpawned))]static class Spawn {static void Postfix(Player __instance){if(Local(__instance))Take(__instance);}}
        [HarmonyPatch(typeof(Player),nameof(Player.OnSkillLevelup))]static class LevelUp {static void Postfix(Player __instance,global::Skills.SkillType skill,float level){if(!Take(__instance))return;if(history.Note((int)skill,level))Save(__instance);}}
        [HarmonyPatch(typeof(global::Skills),nameof(global::Skills.CheatRaiseSkill))]static class Cheat { [HarmonyPriority(Priority.Last)]static void Postfix(global::Skills __instance){if(Local(__instance))NoteCurrent(Player.m_localPlayer);}}
        [HarmonyPatch(typeof(global::Skills),nameof(global::Skills.LowerAllSkills))]static class BeforeLoss {static void Prefix(global::Skills __instance){if(Local(__instance))NoteCurrent(Player.m_localPlayer);}}
        [HarmonyPatch(typeof(global::Skills),nameof(global::Skills.ResetSkill))]static class Reset {static void Postfix(global::Skills __instance,global::Skills.SkillType skillType){if(!Local(__instance))return;var player=Player.m_localPlayer;if(!Take(player))return;history.Forget((int)skillType);Save(player);}}
        [HarmonyPatch(typeof(Player),nameof(Player.ResetCharacter))]static class ResetCharacter {static void Postfix(Player __instance){if(!Take(__instance))return;history.ForgetAll();Save(__instance);}}
    }
}
