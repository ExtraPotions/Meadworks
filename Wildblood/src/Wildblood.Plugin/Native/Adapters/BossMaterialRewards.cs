// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
// Independently adapted ECR f331465 boss material behavior; use the one native death/drop path.
// Changed by expDARE, 2026: owned Wildblood integration, bounded authority receipts and native emission guards.
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn, GPL-3.0-or-later
// Boss progression and material behavior adapted from ECR f331465b3ed2551c89decc42ba81565a7b689543
using System.Collections.Generic;
using BossMaterials = ExtraPotions.Wildblood.Bosses.BossMaterials;
using WildbloodModules = ExtraPotions.Wildblood.Runtime.WildbloodModules;
using Random = UnityEngine.Random;
using HarmonyLib;
using System;
using System.Runtime.CompilerServices;
using ExtraPotions.Wildblood.Adopted.Loot;
using ExtraPotions.Wildblood.Adopted.Rules;
using ExtraPotions.Wildblood.Adopted.Traits;
using ExtraPotions.Wildblood.Integration;
using UnityEngine;
namespace ExtraPotions.Wildblood.Adopted.Runtime
{
    internal static class BossMaterialRewards
    {
        internal sealed class Context
        {
            internal Character Creature=null!;
            internal GameObject Prefab=null!;
            internal int Amount;
            internal bool Issued;
            internal int NativeDepth;
        }
        static readonly Dictionary<Character,Context> active=new Dictionary<Character,Context>();
        static ConditionalWeakTable<Character,Context> plans=new ConditionalWeakTable<Character,Context>();
        internal static Context? Begin(Character creature,EliteController controller)
        {
            if(active.ContainsKey(creature)||!WildbloodModules.Active(WildbloodModule.Bosses)||!WildbloodModules.Active(WildbloodModule.Loot)||!RuleState.Active.Boss.Rewards
                ||!BossProgress.Eligible(creature,controller.Traits,true))return null;
            if(plans.TryGetValue(creature,out var saved))
            {
                if(saved.Issued)return null;
                active.Add(creature,saved);return saved;
            }
            var row=BossMaterials.For(Utils.GetPrefabName(creature.gameObject));
            var prefab=row!=null?DropRoller.FindPrefab(row.Prefab):null;
            if(row==null||prefab==null)return null;
            float factor=AspectLoot.Factor(controller);
            if(RuleState.Active.Loot.Mode!=LootMode.Vanilla)factor*=RuleState.Active.Loot.GlobalMultiplier*RuleState.Active.Loot.BossMultiplier;
            var context=new Context{Creature=creature,Prefab=prefab,Amount=row.Roll(System.Math.Max(0,controller.Traits.Stars),
                Player.GetPlayersInRangeXZ(creature.transform.position,BossTrophies.PlayerRange),factor,Random.Range)};
            plans.Add(creature,context);active.Add(creature,context);return context;
        }
        internal static void Append(EliteController controller,List<KeyValuePair<GameObject,int>> result)
        {
            var creature=controller.Creature;
            if(creature==null||!active.TryGetValue(creature,out var context)||context.Issued||context.NativeDepth==0)return;
            context.Issued=true;
            // A separate native row leaves an existing same-prefab row (including another mod's) unchanged.
            result.Add(new KeyValuePair<GameObject,int>(context.Prefab,context.Amount));
        }
        internal static string AltarLine(string boss,int stars,float aspectFactor)
        {
            if(!WildbloodModules.Active(WildbloodModule.Loot)||!RuleState.Active.Boss.Rewards)return "";
            var row=BossMaterials.For(boss);var prefab=row!=null?DropRoller.FindPrefab(row.Prefab):null;
            if(row==null||prefab==null)return "";
            float factor=aspectFactor;
            if(RuleState.Active.Loot.Mode!=LootMode.Vanilla)factor*=RuleState.Active.Loot.GlobalMultiplier*RuleState.Active.Loot.BossMultiplier;
            int min=row.Roll(stars,1,factor,(a,b)=>a),max=row.Roll(stars,1,factor,(a,b)=>b-1);
            var item=prefab.GetComponent<ItemDrop>();string name=item!=null?item.m_itemData.m_shared.m_name:prefab.name;
            if(global::Localization.instance!=null)name=global::Localization.instance.Localize(name);
            return "\n<color=orange>"+ExtraPotions.Wildblood.Runtime.WildbloodText.Format("boss_material_reward",min,max,name)+"</color>";
        }
        internal static void End(Context? context)
        {
            if(context!=null&&active.TryGetValue(context.Creature,out var held)&&ReferenceEquals(context,held))active.Remove(context.Creature);
        }
        internal static Context? Enter(CharacterDrop drop)
        {
            var creature=drop.GetComponent<Character>();
            if(creature==null||!active.TryGetValue(creature,out var context))return null;
            context.NativeDepth++;return context;
        }
        internal static void Leave(Context? context){if(context!=null)context.NativeDepth--;}
        internal static void Reset(){active.Clear();plans=new ConditionalWeakTable<Character,Context>();}
    }
    [HarmonyPatch(typeof(CharacterDrop),"OnDeath")]
    internal static class BossRewardDropScope
    {
        static void Prefix(CharacterDrop __instance,out BossMaterialRewards.Context? __state)=>__state=BossMaterialRewards.Enter(__instance);
        static Exception? Finalizer(Exception? __exception,BossMaterialRewards.Context? __state){BossMaterialRewards.Leave(__state);return __exception;}
    }
    [HarmonyPatch(typeof(Ragdoll),"Setup")]
    internal static class BossRewardRagdollScope
    {
        static void Prefix(CharacterDrop characterDrop,out BossMaterialRewards.Context? __state)=>__state=characterDrop!=null?BossMaterialRewards.Enter(characterDrop):null;
        static Exception? Finalizer(Exception? __exception,BossMaterialRewards.Context? __state){BossMaterialRewards.Leave(__state);return __exception;}
    }
}
