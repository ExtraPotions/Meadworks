// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
// Source-exposed behavior reference: GrindstoneSkills 0.18 Crafting (MakersMark, SturdyBuild, Thrifty, Journeyman).
using System;
using System.Collections.Generic;
using System.Globalization;
using ExtraPotions.Meadworks.Crucible;
using ExtraPotions.Saga.Skills;
using ExtraPotions.Saga.Runtime.Adopted.Grindstone;
using HarmonyLib;
using UnityEngine;

namespace ExtraPotions.Saga.Runtime {
    public static class NativeCrafting {
        static bool Active(Player player)=>NativeDodging.Owned(player)&&NativeSaga.Active("crafting");
        static float Level(Player player)=>MeadworksSkills.EffectiveLevel(player,global::Skills.SkillType.Crafting);

        static readonly int BuilderHash=CraftingMarks.BuilderKey.GetStableHashCode();
        static readonly int WorkbenchHash="piece_workbench".GetStableHashCode();
        static readonly int ForgeHash="forge".GetStableHashCode();

        static readonly HashSet<ItemDrop.ItemData> beforeCrafting=new HashSet<ItemDrop.ItemData>();
        static readonly List<(ItemDrop Prefab,int Quality)> paidMaterials=new List<(ItemDrop,int)>();
        static Player? currentCrafter;
        static Recipe? currentRecipe;
        static string? currentMarkName;
        static float currentLevel;
        static int currentBatches;
        static bool discovering;

        // Maker's Mark & Thrifty StationCraft integration
        [HarmonyPatch(typeof(InventoryGui),nameof(InventoryGui.DoCrafting))]
        static class DoCraftingPatch {
            [HarmonyPrefix]
            static void Prefix(InventoryGui __instance,Player player){
                Clear();
                if(!Active(player)||__instance.m_craftRecipe==null||__instance.m_craftRecipe.m_item==null)return;
                CraftingStation station=player.GetCurrentCraftingStation();
                if(station==null||station.m_craftingSkill!=global::Skills.SkillType.Crafting)return;
                float level=Level(player);
                if(level<=0)return;
                currentCrafter=player;
                currentRecipe=__instance.m_craftRecipe;
                currentLevel=level;
                currentBatches=__instance.m_multiCrafting?__instance.m_multiCraftAmount:1;
                ItemDrop.ItemData itemData=currentRecipe.m_item.m_itemData;
                if(itemData?.m_shared!=null&&itemData.m_shared.m_useDurability&&itemData.IsEquipable()){
                    currentMarkName=itemData.m_shared.m_name;
                    foreach(var item in player.GetInventory().GetAllItems()){
                        if(item.m_shared.m_name==currentMarkName)beforeCrafting.Add(item);
                    }
                }
            }

            [HarmonyPostfix]
            [HarmonyPriority(Priority.Low)]
            static void Postfix(){
                if(currentCrafter==null)return;
                Player player=currentCrafter;
                if(currentMarkName!=null){
                    foreach(var item in player.GetInventory().GetAllItems()){
                        if(item.m_shared.m_name==currentMarkName&&!beforeCrafting.Contains(item)){
                            item.m_customData=CraftingMarks.WithMakerLevel(item.m_customData,currentLevel);
                            item.m_durability=item.GetMaxDurability();
                        }
                    }
                }
                // Thrifty roll per batch
                if(paidMaterials.Count>0&&CraftingMarks.ThriftyChance(currentLevel)>0){
                    for(int b=0;b<currentBatches;b++){
                        if(CraftingMarks.RollThrifty(currentLevel,UnityEngine.Random.value)){
                            var paid=paidMaterials[UnityEngine.Random.Range(0,paidMaterials.Count)];
                            ItemDrop.ItemData shown=paid.Prefab.m_itemData;
                            int quality=paid.Quality>0?paid.Quality:shown.m_quality;
                            if(player.GetInventory().AddItem(paid.Prefab.gameObject.name,1,quality,shown.m_variant,0L,"",false)!=null){
                                string name=global::Localization.instance.Localize(shown.m_shared.m_name);
                                player.Message(MessageHud.MessageType.TopLeft,"Thrifty: 1 "+name+" saved",0,shown.GetIcon());
                            }
                        }
                    }
                }
                Clear();
            }

            [HarmonyFinalizer]
            static void Finalizer()=>Clear();
        }

        [HarmonyPatch(typeof(Inventory),nameof(Inventory.RemoveItem),new[]{typeof(string),typeof(int),typeof(int),typeof(bool)})]
        static class RemoveMaterialPatch {
            [HarmonyPostfix]
            static void Postfix(Inventory __instance,string name,int itemQuality){
                if(currentCrafter==null||currentRecipe==null||__instance!=currentCrafter.GetInventory())return;
                foreach(var req in currentRecipe.m_resources){
                    if(req.m_resItem!=null&&req.m_resItem.m_itemData.m_shared.m_name==name){
                        paidMaterials.Add((req.m_resItem,itemQuality));
                        return;
                    }
                }
            }
        }

        static void Clear(){
            currentCrafter=null;currentRecipe=null;currentMarkName=null;
            currentLevel=0;currentBatches=0;
            beforeCrafting.Clear();paidMaterials.Clear();
        }

        // Maker's Mark Durability
        [HarmonyPatch(typeof(ItemDrop.ItemData),nameof(ItemDrop.ItemData.GetMaxDurability),typeof(int))]
        static class DurabilityPatch {
            [HarmonyPostfix]
            static void Postfix(ItemDrop.ItemData __instance,ref float __result){
                if(__instance.m_customData!=null&&__instance.m_customData.Count>0){
                    float share=CraftingMarks.ReadMakerShare(__instance.m_customData);
                    if(share>0)__result*=(1f+share);
                }
            }
        }

        // Maker's Mark Tooltip
        [HarmonyPatch(typeof(ItemDrop.ItemData),nameof(ItemDrop.ItemData.GetTooltip),
            typeof(ItemDrop.ItemData),typeof(int),typeof(bool),typeof(float),typeof(int),typeof(bool))]
        static class TooltipPatch {
            const string DurabilityKey="\n$item_durability:";

            [HarmonyPostfix]
            static void Postfix(ItemDrop.ItemData item,bool crafting,bool appending,ref string __result){
                if(appending||item==null||__result==null)return;
                float share=0;
                if(crafting){
                    Player player=Player.m_localPlayer;
                    if(player!=null&&Active(player)){
                        CraftingStation station=player.GetCurrentCraftingStation();
                        if(station!=null&&station.m_craftingSkill==global::Skills.SkillType.Crafting&&item.m_shared!=null&&item.m_shared.m_useDurability&&item.IsEquipable()){
                            share=CraftingMarks.Share(Level(player));
                        }
                    }
                } else if(item.m_customData!=null){
                    share=CraftingMarks.ReadMakerShare(item.m_customData);
                }
                if(share>0){
                    string localizedKey="\n"+(global::Localization.instance!=null?global::Localization.instance.Localize("$item_durability:"):DurabilityKey);
                    int idx=__result.IndexOf(localizedKey,StringComparison.Ordinal);
                    if(idx<0)idx=__result.IndexOf(DurabilityKey,StringComparison.Ordinal);
                    string line="\nMaker's Mark: <color=orange>+"+(share*100f).ToString("0.#",CultureInfo.InvariantCulture)+"% durability</color>";
                    int end=idx<0?-1:__result.IndexOf('\n',idx+1);
                    __result=end<0?__result+line:__result.Insert(end,line);
                }
            }
        }

        // Sturdy Build: Piece placement
        [HarmonyPatch(typeof(Piece),nameof(Piece.SetCreator))]
        static class PieceCreatorPatch {
            [HarmonyPostfix]
            static void Postfix(Piece __instance,long uid){
                Player player=Player.m_localPlayer;
                if(player==null||player.GetPlayerID()!=uid||!Active(player))return;
                PieceTable table=player.GetBuildTool()??player.GetRightItem()?.m_shared?.m_buildPieces;
                if(table==null||(table.m_skill!=global::Skills.SkillType.Crafting&&table.m_skill!=global::Skills.SkillType.None))return;
                ZNetView nview=__instance.m_nview;
                WearNTear wear=__instance.GetComponent<WearNTear>();
                if(wear==null||nview==null||!nview.IsValid()||!nview.IsOwner()||__instance.GetComponent<Ship>()!=null)return;
                float level=Level(player);
                if(CraftingMarks.Share(level)<=0)return;
                ZDO zdo=nview.GetZDO();
                if(zdo.GetFloat(BuilderHash,-1f)>=0)return;
                zdo.Set(BuilderHash,level);
                if(wear.m_health>0){
                    wear.m_health=CraftingMarks.MaximumFromBase(wear.m_health,level);
                    wear.m_healthPercentage=Mathf.Clamp01(zdo.GetFloat(ZDOVars.s_health,wear.m_health)/wear.m_health);
                }
            }
        }

        // Sturdy Build: Piece loaded
        [HarmonyPatch(typeof(WearNTear),nameof(WearNTear.Awake))]
        static class WearNTearAwakePatch {
            [HarmonyPostfix]
            static void Postfix(WearNTear __instance){
                if(__instance.m_nview==null)return;
                ZDO zdo=__instance.m_nview.GetZDO();
                if(zdo==null)return;
                float level=zdo.GetFloat(BuilderHash,0f);
                if(CraftingMarks.Share(level)<=0||__instance.GetComponent<Ship>()!=null)return;
                if(__instance.m_health>0){
                    __instance.m_health=CraftingMarks.MaximumFromBase(__instance.m_health,level);
                    __instance.m_healthPercentage=Mathf.Clamp01(zdo.GetFloat(ZDOVars.s_health,__instance.m_health)/__instance.m_health);
                    __instance.UpdateVisual(false);
                }
            }
        }

        // Journeyman
        [HarmonyPatch(typeof(CraftingStation),nameof(CraftingStation.GetLevel))]
        static class StationLevelPatch {
            [HarmonyPostfix]
            static void Postfix(CraftingStation __instance,ref int __result){
                if(discovering)return;
                Player player=Player.m_localPlayer;
                if(player!=null&&Active(player)&&CraftingMarks.JourneymanUnlocked(Level(player))){
                    ZNetView nview=__instance.m_nview!=null?__instance.m_nview:__instance.GetComponentInParent<ZNetView>();
                    ZDO zdo=nview!=null?nview.GetZDO():null;
                    if(zdo!=null){
                        int prefab=zdo.GetPrefab();
                        if(prefab==WorkbenchHash||prefab==ForgeHash)__result+=1;
                    }
                }
            }
        }

        [HarmonyPatch(typeof(Player),nameof(Player.AddKnownStation))]
        static class DiscoveryPatch {
            [HarmonyPrefix]
            static void Prefix()=>discovering=true;
            [HarmonyFinalizer]
            static void Finalizer()=>discovering=false;
        }

        public static void Page(Player player,List<SkillPageLine> result){
            if(!Active(player))return;
            float level=Level(player);
            result.Add(new SkillPageLine(SagaText.Format("crafting_makers_mark",25)));
            result.Add(new SkillPageLine(SagaText.Format("crafting_sturdy_build",25)));
            result.Add(new SkillPageLine(SagaText.Format("crafting_thrifty",10)));
            result.Add(new SkillPageLine(SagaText.Text(CraftingMarks.JourneymanUnlocked(level)?"crafting_journeyman":"crafting_journeyman_locked")));
        }
    }
}
