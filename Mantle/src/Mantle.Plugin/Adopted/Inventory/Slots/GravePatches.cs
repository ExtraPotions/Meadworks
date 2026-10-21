using System.Collections.Generic;
using System;
using HarmonyLib;
using Mantle.Backpacks;
using Mantle.Core;
using Mantle.Tackle;
using Mantle.Worn;
using UnityEngine;

namespace Mantle.Slots
{
    /// <summary>
    /// Death and the grave. While the game makes the tombstone it takes the armour off; the worn slots stay put, so the
    /// grave keeps every item at its cell (or, with Keep Slots On Death, the slots' items stay with the player,
    /// <see cref="KeptOnDeath"/>). Taking all from the player's own grave (<c>Inventory.MoveAll</c>, the game
    /// puts each item back at its old cell when it is free) brings the armour back into its slots, where it is put on
    /// again; the backpack worn at death goes back into its slot first, so its slots are there again and the cells
    /// line up (<see cref="BackpackGrave"/>), then the tacklebox that lay in its slot (<see cref="TackleboxGrave"/>). A
    /// take all from any other container keeps out of the slots (<see cref="ForeignTakeAll"/>), so a chest's items land
    /// in the grid rather than wherever their chest cell happens to fall.
    /// </summary>
    public static class GravePatches
    {
        /// <summary>A take all into the player's inventory from something that is not a grave is under way.</summary>
        public static bool ForeignTakeAll { get; private set; }

        [HarmonyPatch(typeof(Player), nameof(Player.CreateTombStone))]
        public static class Tombstone
        {
            /// <summary>Set for the local player: worn moves are suspended, and what Keep Slots On Death took out.</summary>
            public sealed class Death
            {
                public KeptOnDeath Kept;
            }

            [HarmonyPrefix]
            public static void Prefix(Player __instance, out Death __state)
            {
                __state = InventoryState.IsLocal(__instance) ? new Death() : null;
                if (__state == null)
                    return;
                WornPlacement.Suspend();
                __state.Kept = KeptOnDeath.Take(__instance);
            }

            /// <summary>Also after an exception, so kept items always come back.</summary>
            [HarmonyFinalizer]
            public static void Finalizer(Player __instance, Death __state)
            {
                if (__state == null)
                    return;
                __state.Kept?.Return(__instance);
                WornPlacement.Resume();
            }
        }

        [HarmonyPatch(typeof(Inventory), nameof(Inventory.MoveAll))]
        public static class TakeAll
        {
            [HarmonyPrefix]
            public static void Prefix(Inventory __instance, Inventory fromInventory, out bool __state)
            {
                __state = InventoryState.Manages(__instance) && IsGrave(fromInventory);
                ForeignTakeAll = InventoryState.Manages(__instance) && !__state;
                if (!__state)
                    return;
                BackpackGrave.WearFirst(__instance, fromInventory);
                TackleboxGrave.PutBackFirst(__instance, fromInventory);
            }

            [HarmonyPostfix]
            public static void Postfix(Inventory __instance, bool __state)
            {
                if (__state)
                    WearWhatCameBack(__instance);
            }

            [HarmonyFinalizer]
            public static void Finalizer() => ForeignTakeAll = false;
        }

        private static bool IsGrave(Inventory inventory)
        {
            foreach (TombStone grave in UnityEngine.Object.FindObjectsByType<TombStone>(FindObjectsSortMode.None))
            {
                Container container = grave.GetComponent<Container>();
                if (container != null && container.GetInventory() == inventory)
                    return true;
            }
            return false;
        }

        private static void WearWhatCameBack(Inventory inventory)
        {
            foreach (ItemDrop.ItemData item in new List<ItemDrop.ItemData>(inventory.GetAllItems()))
            {
                Slot slot = InventoryState.Layout.SlotAt(item.m_gridPos);
                if (slot != null && SlotRules.IsWorn(slot.Kind) && !item.m_equipped && SlotRules.WornKindOf(item) == slot.Kind)
                    InventoryState.Player.EquipItem(item, triggerEquipEffects: false);
            }
        }
    }
}
