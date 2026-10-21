// Burrow - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using Burrow.Core;
using Burrow.Reach;
using Burrow.Resources;
using ExtraPotions.Burrow;
using ExtraPotions.Meadworks.Crucible;

namespace Burrow.Integration
{
    /// <summary>
    /// KB3, provided by Burrow for Kindle's salvage from nearby chests. Kindle lists the gear in the chests Reach crafts from
    /// and takes one item through Burrow's durable hold. Nothing here changes Burrow's trash can or its Shared Materials payments.
    /// <list type="bullet">
    /// <item><c>meadworks.storage.chest_items</c> 1.0, <c>Func&lt;IReadOnlyList&lt;Tuple&lt;object, object&gt;&gt;&gt;</c>: every
    /// (<c>Container</c>, <c>ItemDrop.ItemData</c>) in the chests Reach crafting may use now, nearest chest first. Empty while
    /// Reach crafting is off for this player. Chests in use by another player, held chests and items the container's
    /// allow/deny lists keep are left out.</item>
    /// <item><c>meadworks.storage.chest_take</c> 1.0, <c>Func&lt;object, object, Action, int&gt;</c>: takes the listed item
    /// (container, item) and runs the action once after the removal is decided; answers a <see cref="ChestItemTake"/> code.</item>
    /// </list>
    /// Unity main thread only. Both delegates never throw.
    /// </summary>
    internal static class ChestSalvageLink
    {
        public const string ItemsId = "meadworks.storage.chest_items";
        public const string TakeId = "meadworks.storage.chest_take";
        public static readonly System.Version ContractVersion = new System.Version(1, 0);
        private static readonly IReadOnlyList<Tuple<object, object>> None = new Tuple<object, object>[0];

        public static void Register(ProductRegistration registration, Func<NativeResourceProvider> resources)
        {
            if (registration == null) throw new ArgumentNullException(nameof(registration));
            if (resources == null) throw new ArgumentNullException(nameof(resources));
            registration.AddCapability<Func<IReadOnlyList<Tuple<object, object>>>>(ItemsId, ContractVersion, () =>
            {
                try { return Items(); }
                catch (Exception) { return None; }
            });
            registration.AddCapability<Func<object, object, Action, int>>(TakeId, ContractVersion, (container, item, deliver) =>
            {
                try
                {
                    var provider = resources();
                    return provider != null && container is Container chest && item is ItemDrop.ItemData listed && deliver != null
                        ? provider.TakeItem(chest, listed, deliver) : ChestItemTake.Unavailable;
                }
                catch (Exception error)
                {
                    Plugin.Log.LogWarning("Burrow chest take refused: " + error.GetType().Name);
                    return ChestItemTake.Unavailable;
                }
            });
        }

        /// <summary>The chest is one Reach crafting may use now: the same range, ward, privacy, in-use and prefab rules.</summary>
        public static bool Reachable(Container chest)
        {
            if (chest == null || !ReachRules.Active(ReachMode.Crafting))
                return false;
            foreach (Container reachable in ReachCount.Containers())
                if (reachable == chest)
                    return true;
            return false;
        }

        private static IReadOnlyList<Tuple<object, object>> Items()
        {
            if (Player.m_localPlayer == null || !ReachRules.Active(ReachMode.Crafting))
                return None;
            var found = new List<Tuple<object, object>>();
            foreach (Container chest in ReachCount.Containers())
            {
                Inventory inventory = chest.GetInventory();
                if (inventory == null || NativeInventoryGuards.Held(inventory) || NativeInventoryGuards.ForeignHold(chest.m_nview))
                    continue;
                ContainerRule rule = ReachRules.RuleFor(chest);
                foreach (ItemDrop.ItemData item in inventory.GetAllItems())
                    if (item != null && item.m_stack > 0 && rule.Accepts(item))
                        found.Add(Tuple.Create<object, object>(chest, item));
            }
            return found;
        }
    }
}
