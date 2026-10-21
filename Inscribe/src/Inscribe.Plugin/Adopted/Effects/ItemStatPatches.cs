// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
using System;
using HarmonyLib;

namespace ExtraPotions.Inscribe.Adopted.Effects
{
    // Item-local stat getters (effects-runtime.md section 4). Each postfix changes the number the game computes for
    // this item only, so the vanilla tooltip, the character panel and the real calculation all show the same value.
    // They run on whichever peer asks (the owner for combat, anyone for a tooltip) and read only the item's own
    // replicated data, so every peer gets the same answer. Hot: one cache lookup, then float arithmetic.

    /// <summary><c>item_armor</c>: the piece's armor. Read per incoming hit on the victim's client.</summary>
    [HarmonyPatch(typeof(ItemDrop.ItemData), nameof(ItemDrop.ItemData.GetArmor), new[] { typeof(int), typeof(float) })]
    internal static class ItemArmorPatch
    {
        private static void Postfix(ItemDrop.ItemData __instance, ref float __result)
        {
            ItemLocalSums? sums = ItemLocalCache.Get(__instance);
            if (sums != null)
            {
                __result *= 1f + sums.Get(EffectKind.ItemArmor);
            }
        }
    }

    /// <summary>
    /// Base block power: Lone Blade's share of attack power added first (<see cref="LoneBlade"/>), then <c>item_block</c>
    /// scales the sum, in one postfix (one cache lookup per call). The game derives the blocking value and the tooltip's
    /// block line from it, on the blocker's own client.
    /// </summary>
    [HarmonyPatch(typeof(ItemDrop.ItemData), nameof(ItemDrop.ItemData.GetBaseBlockPower), new[] { typeof(int) })]
    internal static class ItemBlockPatch
    {
        private static void Postfix(ItemDrop.ItemData __instance, ref float __result)
        {
            ItemLocalSums? sums = ItemLocalCache.Get(__instance);
            if (sums == null)
            {
                return;
            }
            float share = LoneBlade.Share(__instance, sums);
            if (share > 0f)
            {
                __result += __instance.GetDamage().GetTotalDamage() * share;
            }
            __result *= 1f + sums.Get(EffectKind.ItemBlock);
        }
    }

    /// <summary>
    /// The shield's block knockback force (used by the blocker's client in BlockAttack): Lone Blade's half share, then
    /// <c>item_deflection</c>, in one postfix.
    /// </summary>
    [HarmonyPatch(typeof(ItemDrop.ItemData), nameof(ItemDrop.ItemData.GetDeflectionForce), new[] { typeof(int) })]
    internal static class ItemDeflectionPatch
    {
        private static void Postfix(ItemDrop.ItemData __instance, ref float __result)
        {
            ItemLocalSums? sums = ItemLocalCache.Get(__instance);
            if (sums == null)
            {
                return;
            }
            __result *= 1f + LoneBlade.Share(__instance, sums) / 2f;
            __result *= 1f + sums.Get(EffectKind.ItemDeflection);
        }
    }

    /// <summary>
    /// <c>item_durability</c>: maximum durability. Hot: the inventory grid asks for every visible item every frame.
    /// Current durability is not touched; repairs fill to the raised maximum.
    /// </summary>
    [HarmonyPatch(typeof(ItemDrop.ItemData), nameof(ItemDrop.ItemData.GetMaxDurability), new[] { typeof(int) })]
    internal static class ItemDurabilityPatch
    {
        private static void Postfix(ItemDrop.ItemData __instance, ref float __result)
        {
            ItemLocalSums? sums = ItemLocalCache.Get(__instance);
            if (sums != null)
            {
                __result *= 1f + sums.Get(EffectKind.ItemDurability);
            }
        }
    }

    /// <summary><c>item_lighten</c> and <c>item_zero_weight</c> (Gossamer: nothing): the item's weight, in both getters the game uses (inventory total and tooltip).</summary>
    [HarmonyPatch]
    internal static class ItemWeightPatch
    {
        [HarmonyPostfix]
        [HarmonyPatch(typeof(ItemDrop.ItemData), nameof(ItemDrop.ItemData.GetWeight), new[] { typeof(int) })]
        private static void Weight(ItemDrop.ItemData __instance, ref float __result) => Lighten(__instance, ref __result);

        [HarmonyPostfix]
        [HarmonyPatch(typeof(ItemDrop.ItemData), nameof(ItemDrop.ItemData.GetNonStackedWeight))]
        private static void NonStackedWeight(ItemDrop.ItemData __instance, ref float __result) => Lighten(__instance, ref __result);

        private static void Lighten(ItemDrop.ItemData item, ref float weight)
        {
            ItemLocalSums? sums = ItemLocalCache.Get(item);
            if (sums != null)
            {
                weight = sums.Get(EffectKind.ItemZeroWeight) > 0f ? 0f : weight * Math.Max(0f, 1f - sums.Get(EffectKind.ItemLighten));
            }
        }
    }
}
