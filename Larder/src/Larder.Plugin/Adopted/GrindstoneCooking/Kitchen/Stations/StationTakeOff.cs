// Copyright (C) 2026 Gerald Glasgow. SPDX-License-Identifier: GPL-3.0-or-later
// Adopted from ValheimMods 9da28eb58adbe065be1e4baef779a448acd38e0f: GrindstoneSkills/GrindstoneSkills/Kitchen/Stations/StationTakeOff.cs
// Changed by expDARE, 2026: namespace, Crucible configuration/lifecycle, owned keys and Larder-only feature dispatch.
using HarmonyLib;

namespace ExtraPotions.Larder.Runtime.Adopted.GrindstoneCooking
{
    /// <summary>
    /// Taking a dish off a cooking station or the oven. The taker's client runs OnInteract: with a done slot it rolls
    /// the game's bonus yield (chance InventoryGui.m_craftBonusChance times the taker's skill factor, adding
    /// m_craftBonusAmount) and sends "RPC_RemoveDoneItem"(position, amount) to the owner. At a kitchen OnInteract runs
    /// with GrindstoneSkills' Extra Food chance and amount in those two fields (<see cref="ExtraFood"/>).
    /// </summary>
    public static class StationTakeOff
    {
        [HarmonyPatch(typeof(CookingStation), nameof(CookingStation.OnInteract))]
        private static class Interact
        {
            [HarmonyPrefix]
            private static void Prefix(CookingStation __instance, out ExtraFood.State __state) =>
                __state = Kitchen.IsKitchen(__instance) ? ExtraFood.Apply() : default;

            [HarmonyFinalizer]
            private static void Finalizer(ExtraFood.State __state) => ExtraFood.Restore(__state);
        }
    }
}
