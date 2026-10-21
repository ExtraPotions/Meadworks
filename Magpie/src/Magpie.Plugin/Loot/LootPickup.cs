// Magpie - ExtraPotions Meadworks loot handling.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
using System;
using ExtraPotions.Magpie.Config;
using ExtraPotions.Magpie.Loot;
using ExtraPotions.Magpie.StepAside;
using HarmonyLib;
using UnityEngine;

namespace ExtraPotions.Magpie.Runtime.Loot
{
    /// <summary>
    /// Design D1, D3 to D5 and section 3.4 (G07, user decisions Q3 and Q4): after the game's own take of a loot-marked
    /// drop succeeded on the picking client, the extra = picked amount x (multiplier - 1) is added as clones through
    /// <c>Inventory.AddItem(ItemData)</c> (the Mantle-routed overload). Magpie adds no cheated mark to extras (Q3 revised
    /// 2026-10-07: drop the mark; a clone keeps the mark only when the taken item already had it); only
    /// stackable items are multiplied by default (Q4); what does not fit drops at the player, owned by that client and
    /// without the loot mark. Auto pickup and hand pickup both pass through <c>Humanoid.Pickup</c>. While the gate blocks
    /// the cheat the multiplier reads 1 and the player is told why once (C03).
    /// </summary>
    [HarmonyPatch(typeof(Humanoid), nameof(Humanoid.Pickup))]
    internal static class LootPickup
    {
        // Independently spawned sibling creature currency is outside CharacterDrop.DropItems. Reuse the same
        // gated multiplier and amount policy; native marked scopes retain their existing pickup-time multiplication.
        internal static int MultiplyExternalCreatureLoot(object opaque,int amount)
        {
            if(amount<=0||!(opaque is ItemDrop.ItemData data)||data.m_shared==null||!MagpieRuntime.ModuleOn(MagpieModule.Loot))return amount;
            var settings=MagpieRuntime.Settings;
            if(settings==null||(settings.Get<LootSources>(MagpieSettings.LootSources)&LootSources.Creatures)==0)return amount;
            // Currency only: never multiply nonstackable magic gear, even when the broader pickup option allows it.
            if(data.m_shared.m_maxStackSize<=1)return amount;
            int multiplier=settings.LootMultiplier();
            if(LootPolicy.ShouldMark(LootMarks.Current,settings.Get<LootSources>(MagpieSettings.LootSources),multiplier,1f))return amount;
            long total=(long)amount+LootPolicy.Extra(true,data.m_shared.m_maxStackSize,amount,multiplier,true).Amount;
            return total>int.MaxValue?int.MaxValue:(int)total;
        }

        internal sealed class Take
        {
            public ItemDrop.ItemData Data = null!;
            public int Before;
        }

        [HarmonyPrefix]
        private static void Prefix(Humanoid __instance, GameObject go, out Take? __state)
        {
            __state = null;
            try
            {
                if (__instance == null || __instance != Player.m_localPlayer || go == null || !MagpieRuntime.ModuleOn(MagpieModule.Loot)) return;
                var item = go.GetComponent<ItemDrop>();
                if (item == null || !LootMarks.IsMarked(item)) return;
                item.Load();
                var data = item.m_itemData;
                if (data == null) return;
                __state = new Take { Data = data.Clone(), Before = Count(__instance, data) };
            }
            catch (Exception e)
            {
                MagpieRuntime.Log?.Error("Loot pickup prefix failed: " + e);
            }
        }

        [HarmonyPostfix]
        private static void Postfix(Humanoid __instance, bool __result, Take? __state)
        {
            if (!__result || __state == null) return;
            try
            {
                var settings = MagpieRuntime.Settings!;
                var multiplier = settings.LootMultiplier();
                var data = __state.Data;
                var maxStack = data.m_shared.m_maxStackSize;
                var picked = LootPolicy.Picked(__state.Before, Count(__instance, data));
                var extra = LootPolicy.Extra(true, maxStack, picked, multiplier, settings.Get<bool>(MagpieSettings.StackableOnly));
                if (extra.Amount <= 0) return;
                Give(__instance, data, extra);
                MagpieRuntime.Log?.Debug($"Loot multiplier x{multiplier}: {data.m_shared.m_name} +{extra.Amount}.");
            }
            catch (Exception e)
            {
                MagpieRuntime.Log?.Error("Loot pickup postfix failed: " + e);
            }
        }

        private static void Give(Humanoid player, ItemDrop.ItemData template, LootExtra extra)
        {
            var inventory = player.GetInventory();
            var transform = player.transform;
            foreach (var amount in LootPolicy.SplitIntoStacks(extra.Amount, template.m_shared.m_maxStackSize))
            {
                var clone = template.Clone();
                clone.m_stack = amount;
                clone.m_cheated = LootPolicy.ExtraCheated(template.m_cheated);
                clone.m_equipped = false;
                if (inventory.CanAddItem(clone, amount) && inventory.AddItem(clone))
                    continue;
                // Overflow: dropped at the player, owned by this client, no loot mark (the scope is closed here).
                ItemDrop.DropItem(clone, amount, transform.position + transform.forward + Vector3.up, transform.rotation);
            }
        }

        private static int Count(Humanoid player, ItemDrop.ItemData data) =>
            player.GetInventory().CountItems(data.m_shared.m_name, data.m_quality, true);
    }
}
