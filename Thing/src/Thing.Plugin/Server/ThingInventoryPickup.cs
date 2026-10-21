// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace ExtraPotions.Thing.Runtime
{
    internal static class ThingInventoryPickup
    {
        static Harmony harmony;
        static readonly MethodInfo cloneShared = AccessTools.Method(typeof(object), "MemberwiseClone");

        internal static void Initialize()
        {
            if (harmony != null) return;
            var next = new Harmony(Plugin.Guid + ".inventory-escrow");
            try
            {
                next.Patch(AccessTools.Method(typeof(ItemDrop), "CanPickup"), prefix: Method(nameof(CanPickup)));
                next.Patch(AccessTools.Method(typeof(ItemDrop), "RPC_RequestOwn"), prefix: Method(nameof(RequestOwn)));
                next.Patch(AccessTools.Method(typeof(ItemDrop), "AutoStackItems"), prefix: Method(nameof(AutoStack)));
                next.Patch(AccessTools.Method(typeof(ItemDrop), "Load"), postfix: Method(nameof(Loaded)));
                next.Patch(AccessTools.Method(typeof(ItemDrop), "Pickup"), prefix: Method(nameof(BeginDropPickup)));
                next.Patch(AccessTools.Method(typeof(Humanoid), "Pickup"), prefix: Method(nameof(BeginPickup)));
                next.Patch(AccessTools.Method(typeof(ZDO), "SetOwner"), prefix: Method(nameof(KeepServerOwner)));
                harmony = next;
            }
            catch { next.UnpatchSelf(); throw; }
        }

        static HarmonyMethod Method(string name) => new HarmonyMethod(typeof(ThingInventoryPickup), name);
        static ZDO Data(ItemDrop drop)
        {
            var view = drop != null ? drop.GetComponent<ZNetView>() : null;
            return view != null && view.IsValid() ? view.GetZDO() : null;
        }
        static bool IsDistinct(ItemDrop drop) => Data(drop)?.GetBool(ThingInventory.Distinct) == true;
        static bool Sealed(ItemDrop drop) => !string.IsNullOrEmpty(Data(drop)?.GetString(ThingInventory.Seal));
        static bool CanPickup(ItemDrop __instance, ref bool __result)
        {
            if (!Sealed(__instance)) return true;
            __result = false; return false;
        }
        static bool RequestOwn(ItemDrop __instance) => !Sealed(__instance);
        static bool AutoStack(ItemDrop __instance) => !IsDistinct(__instance);
        static bool KeepServerOwner(ZDO __instance, ref long __0)
        {
            if (string.IsNullOrEmpty(__instance.GetString(ThingInventory.Seal))) return true;
            if (ZNet.instance == null || !ZNet.instance.IsServer()) return false;
            __0 = ZNet.GetUID(); return true;
        }
        static void Loaded(ItemDrop __instance) { if (IsDistinct(__instance)) Protect(__instance); }

        internal static void Protect(ItemDrop drop)
        {
            // Per-instance shared data prevents native neighboring drops from absorbing this stack.
            if (drop?.m_itemData?.m_shared == null || !drop.m_itemData.m_shared.m_autoStack) return;
            drop.m_itemData.m_shared = (ItemDrop.ItemData.SharedData)cloneShared.Invoke(drop.m_itemData.m_shared, null);
            drop.m_itemData.m_shared.m_autoStack = false;
        }

        static bool BeginPickup(Humanoid __instance, GameObject __0, ref bool __result)
        {
            var data = __0 != null ? Data(__0.GetComponent<ItemDrop>()) : null;
            var seal = data?.GetString(ThingInventory.Seal) ?? "";
            if (seal.Length == 0) return true;
            if (seal.StartsWith("available:", StringComparison.Ordinal) && ReferenceEquals(__instance, Player.m_localPlayer))
                ThingInventoryDelivery.RequestClaim(seal.Substring("available:".Length));
            __result = false;
            return false;
        }
        static bool BeginDropPickup(ItemDrop __instance, Humanoid __0)
        {
            var seal = Data(__instance)?.GetString(ThingInventory.Seal) ?? "";
            if (seal.Length == 0) return true;
            if (seal.StartsWith("available:", StringComparison.Ordinal) && ReferenceEquals(__0, Player.m_localPlayer))
                ThingInventoryDelivery.RequestClaim(seal.Substring("available:".Length));
            return false;
        }
        internal static bool PlaceDistinct(Inventory inventory, ItemDrop.ItemData item) => PlaceDistinct(inventory, item, out _);
        internal static bool PlaceDistinct(Inventory inventory, ItemDrop.ItemData item, out bool inserted)
        {
            inserted = false;
            var position = inventory.FindEmptySlot(inventory.TopFirst(item));
            if (position.x < 0) return false;
            var copy = item.Clone(); copy.m_gridPos = position; copy.m_equipped = false;
            inventory.m_inventory.Add(copy);
            inserted = true;
            inventory.Changed(true, false);
            return true;
        }
        internal static void Shutdown()
        {
            // Guards must continue protecting a saved sealed pickup when Thing is disabled.
            // They are removed only on process exit; releasing items still requires the journal.
        }
    }
}
