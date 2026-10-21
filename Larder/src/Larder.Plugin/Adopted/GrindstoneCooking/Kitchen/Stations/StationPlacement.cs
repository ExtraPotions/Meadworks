// Copyright (C) 2026 Gerald Glasgow. SPDX-License-Identifier: GPL-3.0-or-later
// Adopted from ValheimMods 9da28eb58adbe065be1e4baef779a448acd38e0f: GrindstoneSkills/GrindstoneSkills/Kitchen/Stations/StationPlacement.cs
// Changed by expDARE, 2026: namespace, Crucible configuration/lifecycle, owned keys and Larder-only feature dispatch.
using HarmonyLib;

namespace ExtraPotions.Larder.Runtime.Adopted.GrindstoneCooking
{
    /// <summary>
    /// Putting raw food on a cooking station or the oven. Every way of doing it (Interact, using an item on the
    /// station, the oven's add-food switch) ends in CookingStation.CookItem on the cook's client. Kitchens reserve
    /// one ingredient in a saved escrow until the owner confirms placement or rejection. Other stations keep the
    /// game's own method.
    /// </summary>
    [HarmonyPatch(typeof(CookingStation), nameof(CookingStation.CookItem))]
    public static class StationPlacement
    {
        [HarmonyPrefix]
        private static bool Prefix(CookingStation __instance, Humanoid user, ItemDrop.ItemData item, ref bool __result)
        {
            if (!Kitchen.IsKitchen(__instance))
                return true;
            __result = Cook(__instance, user, item);
            return false;
        }

        private static bool Cook(CookingStation station, Humanoid user, ItemDrop.ItemData item)
        {
            ZNetView nview = station.m_nview;
            if (nview == null || !nview.IsValid()) return false;
            if (!nview.HasOwner())
                nview.ClaimOwnership();
            if (RefuseIncompatible(station, user, item))
                return true;
            if (!station.IsItemAllowed(item) || station.GetFreeSlot() == -1)
                return false;
            return StationAddTransactions.Begin(station, user, item);
        }

        /// <summary>The game's message for an item this station never takes; true when the item is one of those.</summary>
        private static bool RefuseIncompatible(CookingStation station, Humanoid user, ItemDrop.ItemData item)
        {
            foreach (CookingStation.ItemMessage incompatible in station.m_incompatibleItems)
            {
                string name = incompatible.m_item.m_itemData.m_shared.m_name;
                if (name != item.m_shared.m_name)
                    continue;
                user.Message(MessageHud.MessageType.Center, incompatible.m_message + " " + name);
                return true;
            }
            return false;
        }
    }
}
