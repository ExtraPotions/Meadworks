// Larder - ExtraPotions Meadworks food, eating, recovery and kitchen.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks).
// Adapted from ValheimMods at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1: FeastMaster/FeastMaster/FeastMasterCore/Display.cs.
// Copyright (C) 2026 Gerald Glasgow. GPL-3.0-or-later.
// See THIRD_PARTY_NOTICES/ValheimMods-GPL-3.0-or-later.txt and licenses/GPL-3.0.txt.
// Changed by expDARE, 2026: Larder namespace/names/tokens; shared lifecycle/catalog integration;
// native food-root copies, frames, timers and tooltip insertion algorithms retained.
// SPDX-License-Identifier: GPL-3.0-or-later
using ExtraPotions.Larder.Runtime.Adopted.FeastMaster;
using System;
using FMSettings = ExtraPotions.Larder.Runtime.Adopted.FeastMaster.Settings;
using HarmonyLib;
using TMPro;
using UnityEngine;

namespace ExtraPotions.Larder.Runtime.Adopted.FoodHud
{
    /// <summary>Hides the numbers on the health, stamina and eitr bars for this client when the display settings say so.</summary>
    public static class HudNumbers
    {
        public static void Show(TMP_Text text, bool show)
        {
            if (text == null)
                return;
            GameObject go = text.gameObject;
            if (go.activeSelf != show)
                go.SetActive(show);
        }

        /// <summary>A hide patch was removed: the game never hides the number itself, so it is shown once here.</summary>
        public static void ShowAgain(Func<Hud, TMP_Text> text)
        {
            if (Hud.instance != null)
                Show(text(Hud.instance), true);
        }
    }

    [HarmonyPatch(typeof(Hud), nameof(Hud.UpdateHealth))]
    public static class HideHealthNumberPatch
    {
        public static bool Prepare() => NativeFeastMaster.Value(FMSettings.HideHealthNumber);

        public static void Removed() => HudNumbers.ShowAgain(hud => hud.m_healthText);

        [HarmonyPostfix]
        public static void Postfix(Hud __instance) => HudNumbers.Show(__instance.m_healthText, !NativeFeastMaster.Value(FMSettings.HideHealthNumber));
    }

    [HarmonyPatch(typeof(Hud), nameof(Hud.UpdateStamina))]
    public static class HideStaminaNumberPatch
    {
        public static bool Prepare() => NativeFeastMaster.Value(FMSettings.HideStaminaNumber);

        public static void Removed() => HudNumbers.ShowAgain(hud => hud.m_staminaText);

        [HarmonyPostfix]
        public static void Postfix(Hud __instance) => HudNumbers.Show(__instance.m_staminaText, !NativeFeastMaster.Value(FMSettings.HideStaminaNumber));
    }

    [HarmonyPatch(typeof(Hud), nameof(Hud.UpdateEitr))]
    public static class HideEitrNumberPatch
    {
        public static bool Prepare() => NativeFeastMaster.Value(FMSettings.HideEitrNumber);

        public static void Removed() => HudNumbers.ShowAgain(hud => hud.m_eitrText);

        [HarmonyPostfix]
        public static void Postfix(Hud __instance) => HudNumbers.Show(__instance.m_eitrText, !NativeFeastMaster.Value(FMSettings.HideEitrNumber));
    }

    /// <summary>
    /// Food Timers: the game always shows the remaining time under each active food icon (Hud.UpdateFood enables
    /// m_foodTime[i] for every active food), so Vanilla and Always both show it and Never hides it.
    /// </summary>
    [HarmonyPatch(typeof(Hud), nameof(Hud.UpdateFood))]
    public static class FoodTimersPatch
    {
        public static bool Prepare() => Customized.Any(FMSettings.FoodTimersMode);

        public static void Removed()
        {
            Hud hud = Hud.instance;
            if (hud?.m_foodTime == null) return;
            int active = Player.m_localPlayer == null ? 0 : Player.m_localPlayer.GetFoods().Count;
            for (int i = 0; i < hud.m_foodTime.Length; i++) HudNumbers.Show(hud.m_foodTime[i], i < active);
        }

        [HarmonyPostfix]
        public static void Postfix(Hud __instance, Player player)
        {
            FoodTimers mode = NativeFeastMaster.Value(FMSettings.FoodTimersMode);
            if (mode == FoodTimers.Vanilla || __instance.m_foodTime == null)
                return;
            int active = Mathf.Min(player.GetFoods().Count, __instance.m_foodTime.Length);
            for (int i = 0; i < active; i++)
                HudNumbers.Show(__instance.m_foodTime[i], mode == FoodTimers.Always);
        }
    }

    /// <summary>
    /// Adds "Vigor: +X% stamina regen" and "Eitr Vigor: +X% eitr regen" to a food's tooltip after the game's regen
    /// line (or the duration line when the food has no regen), in the orange the game uses for its own regen line.
    /// </summary>
    [HarmonyPatch(typeof(ItemDrop.ItemData), nameof(ItemDrop.ItemData.GetTooltip),
        typeof(ItemDrop.ItemData), typeof(int), typeof(bool), typeof(float), typeof(int), typeof(bool))]
    public static class VigorTooltipPatch
    {
        public static bool Prepare()
        {
            return Customized.Any(FMSettings.VigorPerStaminaPoint, FMSettings.EitrVigorPerEitrPoint)
                || Customized.AnyFood(FeastMasterData.Vigor) || Customized.AnyFood(FeastMasterData.EitrVigor);
        }

        public const string VigorKey = "vigor";
        public const string VigorUnitKey = "vigor_regen";
        public const string EitrVigorKey = "eitr_vigor";
        public const string EitrVigorUnitKey = "eitr_vigor_regen";

        [HarmonyPostfix]
        public static void Postfix(ItemDrop.ItemData item, ref string __result)
        {
            if (item == null || !IsFood(item.m_shared))
                return;
            string lines = Line(VigorKey, VigorUnitKey, ItemValues.VigorOf(item))
                + Line(EitrVigorKey, EitrVigorUnitKey, ItemValues.EitrVigorOf(item));
            if (lines.Length > 0)
                __result = __result.Insert(InsertAt(__result), lines);
        }

        private static string Line(string key, string unitKey, float percent)
        {
            if (percent == 0f)
                return "";
            return $"\n{LarderText.Token(key)}: <color=orange>{percent.ToString("+0.#;-0.#")}% {LarderText.Token(unitKey)}</color>";
        }

        private static bool IsFood(ItemDrop.ItemData.SharedData shared)
        {
            return shared.m_food > 0f || shared.m_foodStamina > 0f || shared.m_foodEitr > 0f;
        }

        /// <summary>End of the regen line, else end of the duration line, else end of the text.</summary>
        private static int InsertAt(string tooltip)
        {
            int start = tooltip.IndexOf("$item_food_regen", StringComparison.Ordinal);
            if (start < 0)
                start = tooltip.IndexOf("$item_food_duration", StringComparison.Ordinal);
            if (start < 0)
                return tooltip.Length;
            int end = tooltip.IndexOf('\n', start);
            return end < 0 ? tooltip.Length : end;
        }
    }

}
