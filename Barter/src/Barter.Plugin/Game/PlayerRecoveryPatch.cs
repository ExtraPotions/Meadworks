// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using HarmonyLib;

namespace ExtraPotions.Barter.Runtime
{
    [HarmonyPatch(typeof(Player), nameof(Player.Load))]
    internal static class Player_Load_BarterRecovery
    {
        private static void Prefix(Player __instance) => TradeAuthority.BeforeCharacterLoad(__instance);
        private static void Postfix(Player __instance) => TradeAuthority.CharacterLoaded(__instance);
    }
}
