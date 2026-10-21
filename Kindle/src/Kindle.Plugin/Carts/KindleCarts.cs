// Changed by expDARE, 2026: retained source adaptation notices.
// Kindle - Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
// Adapted from OpenKeep 3.0.0, ad278ecf323901c3645eb1f9f6fac6833a0f8cb1:
// OpenKeep/OpenKeep/src/Carts/{CartStation,CartInteractPatch,CartHoverPatch,CartLevelPatch,CartsModule}.cs.
// OpenKeep - Copyright (C) 2026 Gerald Glasgow. GPL-3.0-or-later.
// See THIRD_PARTY_NOTICES/OpenKeep-GPL-3.0-or-later.txt and licenses/GPL-3.0.txt.
// Changed by expDARE: Kindle config/cheat authority, lifecycle cleanup, owned-component removal and words.
using System;
using System.Collections.Generic;
using ExtraPotions.Kindle.Config;
using ExtraPotions.Meadworks.Crucible;
using HarmonyLib;
using UnityEngine;

namespace ExtraPotions.Kindle.Runtime
{
    public static class KindleCarts
    {
        private static readonly HashSet<CraftingStation> added = new HashSet<CraftingStation>();
        private static KindleConfig? settings;
        private static MeadworksLog? log;
        private static Harmony? patches;
        private static float nextScan;
        private static bool previousEnabled;
        private static float previousRange;
        internal static bool Enabled => Plugin.Ready && settings!=null && settings.Get(KindleSettings.Cart.CartWorkbench);

        public static void Initialize(KindleConfig config,MeadworksLog logger)
        {
            Shutdown();settings=config;log=logger;nextScan=0;
            settings.Changed+=OnSettingsChanged;
            
            patches=new Harmony(Plugin.Guid+".carts");
            foreach(var type in new[]{typeof(AwakePatch),typeof(InteractPatch),typeof(HoverPatch),typeof(LevelPatch)})patches.CreateClassProcessor(type).Patch();
        }
        private static void OnSettingsChanged(object sender,EventArgs args)
        {
            try{foreach(var cart in new List<Vagon>(Vagon.m_instances))Apply(cart);}
            catch(Exception error){log?.Warning("Cart station refresh refused: "+error.GetType().Name);}
        }
        public static void Tick()
        {
            if(settings==null)return;
            bool enabled=Enabled;float range=settings.Get(KindleSettings.Cart.CartStationRange);
            if(enabled==previousEnabled && range==previousRange && Time.unscaledTime<nextScan)return;
            previousEnabled=enabled;previousRange=range;nextScan=Time.unscaledTime+0.5f;
            foreach(var cart in new List<Vagon>(Vagon.m_instances))Apply(cart);
            added.RemoveWhere(station=>station==null);
        }
        public static void Shutdown()
        {
            patches?.UnpatchSelf();patches=null;
            if(settings!=null)
            {
                settings.Changed-=OnSettingsChanged;
                
            }
            foreach(var station in added)if(station)UnityEngine.Object.Destroy(station);
            added.Clear();settings=null;log=null;previousEnabled=false;previousRange=0;nextScan=0;
        }
        internal static bool IsCart(CraftingStation station)=>station && added.Contains(station);
        internal static void Apply(Vagon cart)
        {
            if(settings==null || !cart || !cart.m_nview || cart.m_nview.GetZDO()==null)return;
            var station=cart.GetComponent<CraftingStation>();
            if(!Enabled)
            {
                // Only remove the runtime component Kindle owns; another mod's cart station stays its own.
                if(IsCart(station)){added.Remove(station);UnityEngine.Object.Destroy(station);}
                return;
            }
            if(station)
            {
                if(IsCart(station))station.m_rangeBuild=settings.Get(KindleSettings.Cart.CartStationRange);
                return;
            }
            var workbench=ZNetScene.instance ? ZNetScene.instance.GetPrefab("piece_workbench") : null;
            var template=workbench ? workbench.GetComponentInChildren<CraftingStation>(true) : null;
            if(!template){log?.Warning("The workbench prefab has no crafting station; carts stay plain carts.");return;}
            station=cart.gameObject.AddComponent<CraftingStation>();
            Copy(template,station,cart.transform);added.Add(station);
        }
        private static void Copy(CraftingStation from,CraftingStation to,Transform cart)
        {
            to.m_name=from.m_name;to.m_icon=from.m_icon;to.m_discoverRange=from.m_discoverRange;
            to.m_rangeBuild=settings!.Get(KindleSettings.Cart.CartStationRange);
            to.m_extraRangePerLevel=from.m_extraRangePerLevel;to.m_craftRequireRoof=false;to.m_craftRequireFire=false;
            to.m_roofCheckPoint=cart;to.m_connectionPoint=null;to.m_showBasicRecipies=from.m_showBasicRecipies;
            to.m_useDistance=Mathf.Max(from.m_useDistance,4f);to.m_craftingSkill=from.m_craftingSkill;
            to.m_hoverOffset=from.m_hoverOffset;to.m_hasCraftTab=from.m_hasCraftTab;to.m_canRepair=from.m_canRepair;to.m_upgrader=false;
            to.m_craftItemEffects=from.m_craftItemEffects;to.m_craftItemDoneEffects=from.m_craftItemDoneEffects;
            to.m_repairItemDoneEffects=from.m_repairItemDoneEffects;to.m_craftItemDoneFailEffects=from.m_craftItemDoneFailEffects;
        }
        [HarmonyPatch(typeof(Vagon),nameof(Vagon.Awake))]
        private static class AwakePatch
        {
            [HarmonyPostfix]private static void Postfix(Vagon __instance)=>Apply(__instance);
        }
        [HarmonyPatch(typeof(Vagon),nameof(Vagon.Interact))]
        private static class InteractPatch
        {
            [HarmonyPrefix]private static bool Prefix(Vagon __instance,Humanoid character,bool hold,bool alt,ref bool __result)
            {
                if(hold || !alt || !Enabled)return true;
                Apply(__instance);var station=__instance.GetComponent<CraftingStation>();
                if(!IsCart(station))return true;
                if(!settings!.TryUseCheat(KindleSettings.Cart.CartWorkbench))return true;
                __result=station.Interact(character,false,false);return false;
            }
        }
        [HarmonyPatch(typeof(Vagon),nameof(Vagon.GetHoverText))]
        private static class HoverPatch
        {
            [HarmonyPostfix]private static void Postfix(Vagon __instance,ref string __result)
            {
                if(Enabled && IsCart(__instance.GetComponent<CraftingStation>()))
                    __result+=Localization.instance.Localize("\n[<color=yellow><b>$KEY_AltPlace + $KEY_Use</b></color>] ")+KindleText.Text("cartcraft");
            }
        }
        [HarmonyPatch(typeof(CraftingStation),nameof(CraftingStation.GetLevel))]
        private static class LevelPatch
        {
            [HarmonyPostfix]private static void Postfix(CraftingStation __instance,ref int __result)
            {
                if(Enabled && IsCart(__instance))__result+=Math.Max(1,settings!.Get(KindleSettings.Cart.CartStationLevel))-1;
            }
        }
    }
}
