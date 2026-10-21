// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
using HarmonyLib;
using UnityEngine;

namespace ExtraPotions.Inscribe.Adopted.Effects
{
    /// <summary>
    /// Item-local affixes that act while the item is worn or held by the local player:
    /// Everlasting keeps the item's durability full (the game drains durability inline at many sites - attacks, blocks,
    /// armor hits, building, passive drain - so instead of patching each, every equipped Everlasting item is topped up
    /// on each rebuild and twice a second; an item never reaches zero between two checks), and Supple Fit refunds the
    /// item's movement penalty. Both on the wearer's own client, which owns the item data (durability is saved with the
    /// inventory, so every peer sees the result).
    /// </summary>
    internal static class Equipment
    {
        private const float Interval = 0.5f;
        private static float _next;

        /// <summary>The summed negative movement modifier of equipped Supple Fit items, as a positive refund.</summary>
        public static float MovementRefund { get; private set; }

        /// <summary>Rebuild: recompute the refund and top up at once.</summary>
        public static void Refresh(Player player)
        {
            MovementRefund = 0f;
            foreach (ItemDrop.ItemData? item in Worn(player))
            {
                ItemLocalSums? sums = item != null ? ItemLocalCache.Get(item) : null;
                if (sums != null && sums.Get(EffectKind.ItemNoMovePenalty) > 0f && item!.m_shared.m_movementModifier < 0f)
                {
                    MovementRefund -= item.m_shared.m_movementModifier;
                }
            }
            _next = 0f;
            Tick(player);
        }

        /// <summary>Twice a second on the local player's client.</summary>
        public static void Tick(Player player)
        {
            if (Time.time < _next)
            {
                return;
            }
            _next = Time.time + Interval;
            foreach (ItemDrop.ItemData? item in Worn(player))
            {
                TopUp(item);
            }
        }

        private static void TopUp(ItemDrop.ItemData? item)
        {
            if (item == null || !item.m_shared.m_useDurability)
            {
                return;
            }
            ItemLocalSums? sums = ItemLocalCache.Get(item);
            if (sums != null && sums.Get(EffectKind.ItemUnbreakable) > 0f)
            {
                float max = item.GetMaxDurability();
                if (item.m_durability < max)
                {
                    item.m_durability = max;
                }
            }
        }

        // The seven counted slots (effects-runtime.md 2); a fixed array reused, nulls skipped by the callers.
        private static readonly ItemDrop.ItemData?[] Slots = new ItemDrop.ItemData?[7];

        private static ItemDrop.ItemData?[] Worn(Player player)
        {
            Slots[0] = player.m_rightItem;
            Slots[1] = player.m_leftItem;
            Slots[2] = player.m_helmetItem;
            Slots[3] = player.m_chestItem;
            Slots[4] = player.m_legItem;
            Slots[5] = player.m_shoulderItem;
            Slots[6] = player.m_utilityItem;
            return Slots;
        }
    }

    /// <summary>Supple Fit: the local player's equipment movement modifier without the flagged items' penalties.</summary>
    [HarmonyPatch(typeof(Player), nameof(Player.GetEquipmentMovementModifier))]
    internal static class MovementPenaltyPatch
    {
        private static void Postfix(Player __instance, ref float __result)
        {
            if (Equipment.MovementRefund > 0f && ReferenceEquals(__instance, Player.m_localPlayer))
            {
                __result += Equipment.MovementRefund;
            }
        }
    }

    /// <summary>
    /// Lone Blade: with the off-hand empty, this one-handed weapon blocks with +X% of its own attack power added to its
    /// block armor, and parries with +X/2 % deflection force. Only while the local player holds it in the right hand
    /// with nothing in the left (the tooltip shows the raised numbers then, and only then). Blocking is resolved on
    /// the blocker's own client. Applied by the block and deflection postfixes in <c>ItemStatPatches</c>, before
    /// <c>item_block</c> and <c>item_deflection</c>.
    /// </summary>
    internal static class LoneBlade
    {
        /// <summary>The share in force for this item now: its Lone Blade value while held alone, else 0.</summary>
        public static float Share(ItemDrop.ItemData item, ItemLocalSums sums)
        {
            float share = sums.Get(EffectKind.LoneBlade);
            Player? player = share > 0f ? Player.m_localPlayer : null;
            return player != null && ReferenceEquals(player.m_rightItem, item) && player.m_leftItem == null ? share : 0f;
        }
    }
}
