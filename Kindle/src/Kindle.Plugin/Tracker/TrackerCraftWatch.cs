// Changed by expDARE, 2026: retained source adaptation notices.
// Kindle - Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
// Adapted from OpenKeep 3.0.0, ad278ecf323901c3645eb1f9f6fac6833a0f8cb1:
// OpenKeep/OpenKeep/src/Tracker/{CraftWatch,TrackerPatches}.cs. Copyright (C) 2026 Gerald Glasgow. GPL-3.0-or-later.
// See THIRD_PARTY_NOTICES/OpenKeep-GPL-3.0-or-later.txt and licenses/GPL-3.0.txt.
// Changed by expDARE: Kindle hooks and captured character/upgrader identity; native successful-count proof unchanged.
using HarmonyLib;
namespace ExtraPotions.Kindle.Runtime
{
    [HarmonyPatch(typeof(InventoryGui),nameof(InventoryGui.DoCrafting))]
    internal static class TrackerCraftWatch
    {
        internal sealed class Watch
        {
            internal Player Actor=null!;
            internal Recipe Recipe=null!;
            internal int Quality,Crafts,Before;
            internal bool Upgrader;
        }
        [HarmonyPrefix,HarmonyPriority(Priority.Low)]
        private static void Prefix(InventoryGui __instance,Player player,out Watch? __state)
        {
            __state=null;var recipe=__instance.m_craftRecipe;
            if(!KindleTracker.Active || player!=Player.m_localPlayer || !recipe || !recipe.m_item)return;
            int quality=__instance.m_craftUpgradeItem==null ? 1 : __instance.m_craftUpgradeItem.m_quality+1;
            var station=player.GetCurrentCraftingStation();bool upgrader=station && station.m_upgrader;
            if(!KindleTracker.Settings.Get(ExtraPotions.Kindle.Config.KindleSettings.Tracker.Enabled) || !KindleTracker.IsTracked(recipe,quality,upgrader))return;
            __state=new Watch{Actor=player,Recipe=recipe,Quality=quality,Upgrader=upgrader,
                Crafts=__instance.m_craftUpgradeItem==null && __instance.m_multiCrafting ? __instance.m_multiCraftAmount : 1,
                Before=player.GetInventory().CountItems(recipe.m_item.m_itemData.m_shared.m_name,quality,false)};
        }
        [HarmonyPostfix]
        private static void Postfix(Player player,Watch? __state)
        {
            if(__state!=null && player==__state.Actor && player==Player.m_localPlayer
                && player.GetInventory().CountItems(__state.Recipe.m_item.m_itemData.m_shared.m_name,__state.Quality,false)>__state.Before)
                KindleTracker.Crafted(__state.Recipe,__state.Quality,__state.Crafts,__state.Upgrader);
        }
    }
}
