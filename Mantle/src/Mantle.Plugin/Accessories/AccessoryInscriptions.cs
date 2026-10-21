// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: rings and belts roll rarities and inscriptions with Inscribe (batch 3, user decision 2026-10-19).
using System.Collections.Generic;
using ExtraPotions.Mantle.Embedded.EliteCraftingLink;
using Mantle.Core;
using Mantle.Slots;
using HarmonyLib;
using UnityEngine;

namespace Mantle.Accessories
{
    /// <summary>
    /// With Inscribe present: Mantle's rings and belts become two item classes of their own (<c>ring</c>, <c>belt</c>, group
    /// jewel: no sockets), each with a curated pool of Inscribe's existing inscriptions whose effects make sense on a ring or a
    /// belt; their item level follows their recipe's materials, as Inscribe works it out. They may drop pre-rolled (drop
    /// weight 0.25). The items lying in the Ring and Belt cells count as equipment for their inscriptions (provider
    /// <see cref="ProviderId"/>), as their stat bonus already follows the cell: a death, a logout or a world change takes both
    /// away. Existing rings and belts stay Common until something rolls them; their item data keeps Inscribe's usual keys, so
    /// an older Inscribe or Mantle reads them as before (an older Mantle simply does not count their inscriptions).
    /// Registrations are code, as the backpack's (<c>Mantle.Elite.EliteSetup</c>); a server's Inscribe economy file can still
    /// turn a class off (<c>classes: [ { id: ring, rolls: false } ]</c>).
    /// </summary>
    public static class AccessoryInscriptions
    {
        public const string RingClass = "ring";
        public const string BeltClass = "belt";
        public const string ProviderId = "mantle_accessories";

        public const string RingJson = "{ \"id\": \"ring\", \"group\": \"jewel\", \"name\": \"$mantle_ring\", \"rolls\": true, \"drop_weight\": 0.25 }";
        public const string BeltJson = "{ \"id\": \"belt\", \"group\": \"jewel\", \"name\": \"$mantle_belt\", \"rolls\": true, \"drop_weight\": 0.25 }";

        /// <summary>Ring pool: health, stamina and eitr pools and their recovery, skill gain, cleansing and coin find.</summary>
        public static readonly (string Id, bool Best)[] RingPool =
        {
            ("vigor", true), ("endurance", true), ("wellspring", true), ("hale", false),
            ("troll_blood", true), ("second_wind", true), ("seidr_flow", false), ("mimirs_insight", false), ("purity", false), ("hoardfinder", false),
        };

        /// <summary>Belt pool: carrying, movement and stamina on the move, pickup range and harvest.</summary>
        public static readonly (string Id, bool Best)[] BeltPool =
        {
            ("broad_back", true), ("fleet", false), ("steady_breath", false),
            ("pack_mule", true), ("long_wind", true), ("magpie", false), ("harvester", false), ("light_leap", false),
        };

        private static readonly List<ItemDrop.ItemData> answer = new List<ItemDrop.ItemData>(4);
        private static int lastSignature;

        public static void Register()
        {
            if (!CraftingLink.Present)
                return;
            var refused = new List<string>();
            Expect(CraftingClasses.RegisterItemClass(RingJson), "the ring class", refused);
            Expect(CraftingClasses.RegisterItemClass(BeltJson), "the belt class", refused);
            Expect(CraftingClasses.ClaimItems(RingClass, Ids(AccessorySlot.Ring)), "the rings' class", refused);
            Expect(CraftingClasses.ClaimItems(BeltClass, Ids(AccessorySlot.Belt)), "the belts' class", refused);
            foreach ((string id, bool best) in RingPool)
                Expect(CraftingInscriptions.AddToPool(RingClass, id, best), id + " on rings", refused);
            foreach ((string id, bool best) in BeltPool)
                Expect(CraftingInscriptions.AddToPool(BeltClass, id, best), id + " on belts", refused);
            Expect(CraftingHooks.RegisterEquipmentProvider(ProviderId, Provide), "the ring and belt cells as equipment", refused);
            Plugin.Log.LogInfo("Inscribe: rings and belts roll inscriptions" + (refused.Count > 0 ? ", refused: " + string.Join(", ", refused) : ""));
        }

        public static void Shutdown()
        {
            if (CraftingLink.Present)
                CraftingHooks.UnregisterEquipmentProvider(ProviderId);
            lastSignature = 0;
        }

        private static string[] Ids(AccessorySlot slot)
        {
            var ids = new List<string>();
            foreach (AccessoryKind kind in AccessoryKinds.All)
                if (kind.Defaults.Slot == slot)
                    ids.Add(kind.Id);
            return ids.ToArray();
        }

        // The local player's Ring and Belt cells. A game belt worn natively from the belt cell is already equipment.
        private static List<ItemDrop.ItemData> Provide(Player player)
        {
            answer.Clear();
            if (!InventoryState.Active || !InventoryState.IsLocal(player) || player.IsDead())
                return answer;
            Inventory inventory = player.GetInventory();
            foreach (SlotKind kind in new[] { SlotKind.Ring, SlotKind.Belt })
            {
                IReadOnlyList<Vector2i> cells = InventoryState.CellsOf(kind);
                for (int i = 0; i < cells.Count; i++)
                {
                    ItemDrop.ItemData item = inventory.GetItemAt(cells[i].x, cells[i].y);
                    if (item != null && !item.m_equipped && AccessoryItems.KindOf(item) == kind && !answer.Contains(item))
                        answer.Add(item);
                }
            }
            return answer;
        }

        /// <summary>The local inventory changed: when the items in the Ring and Belt cells did, Inscribe rebuilds the totals.</summary>
        internal static void Changed(Inventory inventory)
        {
            Player player = Player.m_localPlayer;
            if (!CraftingLink.Present || player == null || inventory != player.GetInventory())
                return;
            int signature = 17;
            foreach (ItemDrop.ItemData item in Provide(player))
                signature = signature * 31 + item.GetHashCode();
            if (signature == lastSignature)
                return;
            lastSignature = signature;
            CraftingHooks.InvalidatePlayer(player);
        }

        private static void Expect(bool accepted, string what, List<string> refused)
        {
            if (!accepted)
                refused.Add(what);
        }
    }

    [HarmonyPatch(typeof(Inventory), "Changed")]
    public static class AccessoryInscriptionsChangedPatch
    {
        [HarmonyPostfix]
        public static void Postfix(Inventory __instance) => AccessoryInscriptions.Changed(__instance);
    }
}
