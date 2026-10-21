// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Portions from aedenthorn/ValheimMods 83690e8ef9a37c4694604416667d252b4ff5f645 (public domain):
// Durability/Durability.cs BlockAttack_Patch lines 183-203, shield snapshot and reduced-durability delta.
// Changed by expDARE: actual local owner/C13 gates, exact inventory item identity, all native blockers,
// scoped native attack/tool contributions and prevention before destructive timed drain. No shared-data changes.
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
namespace ExtraPotions.Hex.Runtime.Native;

internal static class HexDurability
{
    private static bool On(Character actor) => HexMovement.On(actor, "disable_durability_loss");
    private readonly struct WearState
    {
        internal readonly ItemDrop.ItemData? Item;
        internal readonly float Before;
        internal WearState(Character actor, ItemDrop.ItemData? item)
        {
            Item = On(actor) && item != null && actor is Humanoid owner && owner.GetInventory().ContainsItem(item) ? item : null;
            Before = Item?.m_durability ?? 0f;
        }
        internal void Restore(Character actor)
        {
            // Repair increases remain untouched; consumed/removed items are never brought back.
            if (Item != null && On(actor) && actor is Humanoid owner && owner.GetInventory().ContainsItem(Item)
                && Before > Item.m_durability)
                Item.m_durability = UnityEngine.Mathf.Max(0f, Before - (Before - Item.m_durability) * 0f);
        }
    }
    [HarmonyPatch(typeof(Humanoid), "BlockAttack")]
    private static class Block
    {
        [HarmonyPriority(Priority.First)]
        private static void Prefix(Humanoid __instance, out WearState __state) => __state = new WearState(__instance, __instance.GetCurrentBlocker());
        [HarmonyPriority(Priority.Last)]
        private static void Finalizer(Humanoid __instance, WearState __state) => __state.Restore(__instance);
    }
    [HarmonyPatch(typeof(Player), "DamageArmorDurability")]
    private static class Armor
    {
        private static bool Prefix(Player __instance) => !On(__instance);
    }
    [HarmonyPatch(typeof(Humanoid), "DrainEquipedItemDurability")]
    private static class TimedWear
    {
        // Keep already broken items' native destruction/unequip handling. A live torch never reaches it through wear.
        private static bool Prefix(Humanoid __instance, ItemDrop.ItemData item) => !(On(__instance) && item.m_durability > 0f
            && __instance.GetInventory().ContainsItem(item));
    }
    [HarmonyPatch(typeof(Player), "UpdatePlacement")]
    private static class Placement
    {
        [HarmonyPriority(Priority.First)]
        private static void Prefix(Player __instance, out WearState __state) => __state = new WearState(__instance, __instance.m_rightItem);
        [HarmonyPriority(Priority.Last)]
        private static void Finalizer(Player __instance, WearState __state) => __state.Restore(__instance);
    }
    [HarmonyPatch(typeof(Player), "Repair")]
    private static class RepairTool
    {
        [HarmonyPriority(Priority.First)]
        private static void Prefix(Player __instance, ItemDrop.ItemData toolItem, out WearState __state) => __state = new WearState(__instance, toolItem);
        [HarmonyPriority(Priority.Last)]
        private static void Finalizer(Player __instance, WearState __state) => __state.Restore(__instance);
    }
    [HarmonyPatch]
    private static class AttackWear
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            foreach (var name in new[] { "OnAttackTrigger", "ProjectileAttackTriggered", "DoNonAttack", "DoAreaAttack", "DoMeleeAttack" })
                yield return AccessTools.DeclaredMethod(typeof(Attack), name);
        }
        [HarmonyPriority(Priority.First)]
        private static void Prefix(Attack __instance, out WearState __state) => __state = new WearState(__instance.m_character, __instance.m_weapon);
        [HarmonyPriority(Priority.Last)]
        private static void Finalizer(Attack __instance, WearState __state) => __state.Restore(__instance.m_character);
    }
}
