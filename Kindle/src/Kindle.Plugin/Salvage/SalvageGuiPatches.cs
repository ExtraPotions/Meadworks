// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Gerald Glasgow
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Pinned OpenKeep 3.0.0 native hook ordering, adapted to Kindle's kit presentation and lifecycle.
// Upstream GPL-3.0-or-later notice: THIRD_PARTY_NOTICES/OpenKeep-GPL-3.0-or-later.txt.
// Changed by expDARE, 2026: bounded Kindle controller callbacks.
using HarmonyLib;
namespace ExtraPotions.Kindle.Runtime.Salvage
{
    [HarmonyPatch]
    internal static class SalvageGuiPatches
    {
        [HarmonyPatch(typeof(InventoryGui),"Awake"),HarmonyPostfix]
        static void Awake(InventoryGui __instance)=>KindleSalvage.Guard(()=>KindleSalvage.Ensure(__instance));
        [HarmonyPatch(typeof(InventoryGui),"UpdateCraftingPanel"),HarmonyPrefix]
        static bool Panel(InventoryGui __instance,bool focusView)=>KindleSalvage.Guard(()=>KindleSalvage.BeforeCraftingPanel(__instance,focusView));
        [HarmonyPatch(typeof(InventoryGui),"UpdateCraftingPanel"),HarmonyPostfix]
        static void AfterPanel()=>KindleSalvage.Guard(()=>KindleSalvage.Tick());
        [HarmonyPatch(typeof(InventoryGui),"UpdateRecipe"),HarmonyPrefix]
        static bool BeforeRecipe(InventoryGui __instance)=>KindleSalvage.Guard(()=>KindleSalvage.BeforeRecipe(__instance));
        [HarmonyPatch(typeof(InventoryGui),"UpdateRecipe"),HarmonyPostfix,HarmonyAfter("randyknapp.mods.epicloot"),HarmonyPriority(Priority.Last)]
        static void Recipe(InventoryGui __instance,Player player)=>KindleSalvage.Guard(()=>KindleSalvage.Fill(__instance,player));
        [HarmonyPatch(typeof(InventoryGui),"OnCraftPressed"),HarmonyPrefix]
        static bool Craft(InventoryGui __instance)=>KindleSalvage.Guard(()=>KindleSalvage.Craft(__instance));
        [HarmonyPatch(typeof(InventoryGui),"OnTabCraftPressed"),HarmonyPrefix]
        static void CraftTab()=>KindleSalvage.Deselect();
        [HarmonyPatch(typeof(InventoryGui),"OnTabUpgradePressed"),HarmonyPrefix]
        static void UpgradeTab()=>KindleSalvage.Deselect();
        [HarmonyPatch(typeof(InventoryGui),"UpdateRecipeGamepadInput"),HarmonyPrefix]
        static bool Gamepad(InventoryGui __instance)=>KindleSalvage.Guard(()=>KindleSalvage.Gamepad(__instance));
        [HarmonyPatch(typeof(InventoryGui),"Update"),HarmonyPostfix]
        static void Update(InventoryGui __instance)=>KindleSalvage.Guard(()=>KindleSalvage.Hotkey(__instance));
    }
}
