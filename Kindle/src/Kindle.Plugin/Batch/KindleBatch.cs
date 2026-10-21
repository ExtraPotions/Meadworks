// Kindle - ExtraPotions Meadworks crafting conveniences.
// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
// Native hook/drive behavior adapted from OpenKeep 3.0.0 ad278ecf323901c3645eb1f9f6fac6833a0f8cb1,
// OpenKeep/OpenKeep/src/Batch/BatchPatches.cs and BatchStepper.cs; Core/InventoryChanges.cs.
// Copyright (C) 2026 Gerald Glasgow. GNU GPL version 3 or later; see THIRD_PARTY_NOTICES.
// Changed by expDARE, 2026: Crucible KitStepper replaces the reference UI; scoped Kindle lifecycle/settings.
using System;
using HarmonyLib;
using TMPro;
using UnityEngine;
using ExtraPotions.Meadworks.Crucible;
using ExtraPotions.Meadworks.Crucible.UI;

namespace ExtraPotions.Kindle.Runtime
{
    public static class KindleBatch
    {
        internal static KindleConfig Config;
        internal static int InventoryChanges;
        static MeadworksLog log;
        static Harmony patches;
        static InventoryGui owner;
        static KitStepper stepper;
        static RectTransform craft;
        static Vector2 originalMin,originalMax;
        static float trim;
        static bool laidOut;
        /// <summary>The row is as high as the Craft caption plus this padding, never below <see cref="MinRowHeight"/> or above the game's button.</summary>
        const float CaptionPadding=12f,MinRowHeight=30f;
        public static bool Active => Config!=null && Plugin.GameplayAvailable && Plugin.AllowsUi && Player.m_localPlayer && ZNet.instance;
        public static int Amount => BatchAmount.Value;
        public static bool Shown => stepper!=null && stepper.Shown;
        public static int NextCraft(InventoryGui gui,Player player) => gui && player ? BatchAmount.NextCraft(gui,player) : 1;

        public static void Initialize(KindleConfig config,MeadworksLog logger)
        {
            Shutdown();Config=config;log=logger;
            patches=new Harmony(Plugin.Guid+".batch");
            patches.CreateClassProcessor(typeof(KindleBatchPatches)).Patch();
            if(InventoryGui.instance)Remember(InventoryGui.instance);
        }
        internal static void Guard(Action action)
        {
            try{action();}catch(Exception error){log?.Warning("Batch crafting: "+error.GetType().Name+": "+error.Message);}
        }
        internal static void Remember(InventoryGui gui)
        {
            ClearUi();owner=gui;BatchDrive.Remember(gui);CraftSpeed.Remember(gui);BatchAmount.Reset();
        }
        internal static void BeforeRecipe(InventoryGui gui,Player player)
        {
            if(owner!=gui)Remember(gui);
            if(!Active){BatchDrive.Release(gui);CraftSpeed.ApplySafe(gui);return;}
            CraftSpeed.Apply(gui);
            if(!BatchAmount.Applies(gui,player)){BatchDrive.Release(gui);return;}
            BatchAmount.Follow(player,gui.m_selectedRecipe.Recipe);BatchDrive.Drive(gui,BatchAmount.Value);
        }
        internal static void Refresh(InventoryGui gui,Player player)
        {
            if(!Active){RestoreCraft();return;}
            if(owner!=gui)Remember(gui);
            if(stepper==null && gui.m_craftButton)
            {
                craft=(RectTransform)gui.m_craftButton.transform;originalMin=craft.offsetMin;originalMax=craft.offsetMax;
                float native=craft.rect.height,row=RowHeight(craft,native);trim=(native-row)*0.5f;
                stepper=KitStepper.Create((RectTransform)craft.parent,"Kindle_batch",Vector2.zero,row);
                if(stepper==null)return;
                stepper.Changed+=BatchAmount.Set;
            }
            bool show=BatchAmount.Applies(gui,player) && gui.m_craftTimer<0f;
            Show(show);if(!show || stepper==null)return;
            bool selected=gui.m_selectedRecipe.Recipe;
            int cap=selected?BatchAmount.Limit(player,gui.m_selectedRecipe.Recipe):1;
            stepper.Follow(cap);stepper.Set(BatchAmount.Value);
            stepper.Less.interactable=selected && BatchAmount.Value>1;
            stepper.More.interactable=selected && BatchAmount.CanStepUp(player);
            stepper.Field.SetUsable(selected);
            stepper.PadSteps=ZInput.IsGamepadActive() && gui.m_activeGroup==3;
            if(selected && gui.m_craftButton.GetComponentInChildren<TMP_Text>(true) is TMP_Text label)
                label.text=Localization.instance.Localize("$inventory_craftbutton");
        }
        /// <summary>
        /// The amount row (-, amount, + and Craft) fits its text: Craft keeps its place and width but is trimmed to the
        /// caption's height plus padding, centered where the game's taller button was, and the stepper takes the same
        /// height beside it. The game's offsets come back when Kindle's batch crafting is off or unloads.
        /// </summary>
        static float RowHeight(RectTransform button,float native)
        {
            var label=button.GetComponentInChildren<TMP_Text>(true);
            // fontSize is the drawn size, auto-sized captions included.
            float size=label==null?0f:label.fontSize;
            float row=Mathf.Max(MinRowHeight,size+CaptionPadding);
            return native>0f?Mathf.Min(row,native):row;
        }
        static void Show(bool show)
        {
            if(stepper==null || !craft)return;
            float left=show?stepper.Root.sizeDelta.x+KitStepperLayout.CraftGap:0f;
            var min=new Vector2(originalMin.x+left,originalMin.y+trim);var max=new Vector2(originalMax.x,originalMax.y-trim);
            if(craft.offsetMin!=min)craft.offsetMin=min;
            if(craft.offsetMax!=max)craft.offsetMax=max;
            laidOut=true;
            var rect=stepper.Root;rect.anchorMin=rect.anchorMax=craft.anchorMin;rect.pivot=new Vector2(0f,0f);
            rect.anchoredPosition=new Vector2(originalMin.x,originalMin.y+trim);rect.sizeDelta=new Vector2(rect.sizeDelta.x,craft.rect.height);
            stepper.Show(show);
        }
        static void RestoreCraft()
        {
            if(laidOut && craft){craft.offsetMin=originalMin;craft.offsetMax=originalMax;}
            laidOut=false;stepper?.Show(false);
        }
        public static void Tick()
        {
            if(!Active){RestoreCraft();return;}
            if(owner!=InventoryGui.instance && InventoryGui.instance)Guard(()=>Remember(InventoryGui.instance));
        }
        static void ClearUi(){RestoreCraft();stepper?.Destroy();stepper=null;craft=null;trim=0f;}
        public static void Shutdown()
        {
            if(owner){BatchDrive.Release(owner);CraftSpeed.ApplySafe(owner);}
            ClearUi();owner=null;BatchDrive.Forget();BatchAmount.Reset();patches?.UnpatchSelf();patches=null;Config=null;log=null;
        }
    }

    [HarmonyPatch]
    internal static class KindleBatchPatches
    {
        [HarmonyPatch(typeof(InventoryGui),nameof(InventoryGui.Awake)),HarmonyPostfix]
        static void Awake(InventoryGui __instance)=>KindleBatch.Guard(()=>KindleBatch.Remember(__instance));
        [HarmonyPatch(typeof(InventoryGui),nameof(InventoryGui.UpdateRecipe)),HarmonyPrefix]
        static void BeforeRecipe(InventoryGui __instance,Player player)=>KindleBatch.Guard(()=>KindleBatch.BeforeRecipe(__instance,player));
        [HarmonyPatch(typeof(InventoryGui),nameof(InventoryGui.UpdateRecipe)),HarmonyPostfix]
        static void AfterRecipe(InventoryGui __instance,Player player)=>KindleBatch.Guard(()=>KindleBatch.Refresh(__instance,player));
        [HarmonyPatch(typeof(InventoryGui),nameof(InventoryGui.OnCraftPressed)),HarmonyPrefix]
        static void BeforePressed(InventoryGui __instance,out int __state)
        {
            __state=0;BatchDrive.Forget();
            if(!KindleBatch.Active || __instance.m_craftTimer>=0f || !BatchAmount.Applies(__instance,Player.m_localPlayer))return;
            BatchAmount.Follow(Player.m_localPlayer,__instance.m_selectedRecipe.Recipe);
            BatchDrive.Drive(__instance,BatchAmount.Value);__state=BatchAmount.Value;
        }
        [HarmonyPatch(typeof(InventoryGui),nameof(InventoryGui.OnCraftPressed)),HarmonyPostfix]
        static void AfterPressed(InventoryGui __instance,int __state){if(__state>0 && __instance.m_craftTimer>=0f)BatchDrive.Started(__instance,__state);}
        [HarmonyPatch(typeof(InventoryGui),nameof(InventoryGui.DoCrafting)),HarmonyPrefix,HarmonyPriority(Priority.First)]
        static void BeforeCraft(InventoryGui __instance)=>BatchDrive.Restore(__instance);
        [HarmonyPatch(typeof(InventoryGui),nameof(InventoryGui.DoCrafting)),HarmonyFinalizer]
        static void AfterCraft()=>BatchDrive.Forget();
        [HarmonyPatch(typeof(Inventory),nameof(Inventory.Changed)),HarmonyPrefix]
        static void Changed(){unchecked{KindleBatch.InventoryChanges++;}}
    }
}
