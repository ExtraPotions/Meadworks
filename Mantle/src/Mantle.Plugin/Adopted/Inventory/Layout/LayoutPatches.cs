using System;
using HarmonyLib;
using Mantle.Worn;
using UnityEngine;

namespace Mantle.Layout
{
    /// <summary>
    /// When the layout is applied. <c>Game.SpawnPlayer</c> sets the local player, loads the character
    /// (<c>Player.Load</c>) and then calls <c>Player.OnSpawned</c>, which sets the inventory size from the game's
    /// <c>invrows</c> key through <c>Player.SetInventorySize</c>; the trader's row and the <c>inventorysize</c> command
    /// go through the same method. The game's version drops every item outside its rows, which would throw the slots
    /// on the ground, so with the module on (or a record to undo) it is replaced: the key is written as the game does,
    /// then the layout is applied.
    /// </summary>
    public static class LayoutPatches
    {
        [HarmonyPatch(typeof(Player), nameof(Player.Load))]
        public static class Load
        {
            [HarmonyPrefix]
            public static void Prefix(Player __instance, out bool __state)
            {
                __state = __instance == Player.m_localPlayer && Plugin.WorldReady;
                if (__state)
                    LayoutApply.BeforeLoad(__instance);
            }

            [HarmonyPostfix]
            public static void Postfix(Player __instance, bool __state)
            {
                if (__state)
                    LayoutApply.AfterLoad(__instance);
            }

            [HarmonyFinalizer]
            public static void Finalizer(bool __state)
            {
                if (__state)
                    WornPlacement.Resume();
            }
        }

        [HarmonyPatch(typeof(Player), nameof(Player.SetInventorySize))]
        public static class Size
        {
            [HarmonyPrefix]
            public static bool Prefix(Player __instance, int rows)
            {
                if (__instance != Player.m_localPlayer || !LayoutApply.Wanted(__instance))
                    return true;
                __instance.AddUniqueKeyValue(Player.InventoryRowsKey, Mathf.Clamp(rows, 0, 9).ToString());
                LayoutApply.Apply(__instance, dropOverflow: true);
                return false;
            }
        }
    }
}
