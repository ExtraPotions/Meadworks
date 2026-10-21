// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
using System;
using HarmonyLib;

namespace ExtraPotions.Inscribe.Adopted.Effects
{
    /// <summary>
    /// <c>brand_damage</c>: the weapon's damage block. Each brand adds its share of the item's own combat damage
    /// (every type except chop and pickaxe, measured once, so brands never compound on each other) as its type. A slash brand also adds the same share
    /// as chop when the item chops (keen_edge on axes). A brand on a group param splits its share evenly over the
    /// group's types (judgement call: the total added stays X% of the base).
    /// <para>
    /// Normal priority; <c>added_damage</c> has its own Low-priority postfix (<see cref="Combat3.AddedDamagePatch"/>), so
    /// the percent brands measure the item's own damage before anything flat is added, and another mod's postfix keeps
    /// its place between the two. One cache lookup per call (<see cref="ItemLocalCache"/>): this postfix hands the
    /// item's numbers to the later one (<see cref="Handed"/>), which looks them up itself only when another item's
    /// GetDamage ran in between.
    /// </para>
    /// <para>
    /// Runs on the attacker's client when the hit is built (Attack), so the numbers travel inside the HitData; and on
    /// any peer for the tooltip, from the same replicated item data. Hot: per hit and per tooltip frame.
    /// </para>
    /// </summary>
    [HarmonyPatch(typeof(ItemDrop.ItemData), nameof(ItemDrop.ItemData.GetDamage), new[] { typeof(int), typeof(float) })]
    internal static class ItemDamagePatch
    {
        private static ItemDrop.ItemData? _handedItem;
        private static ItemLocalSums? _handedSums;

        /// <summary>The item's local numbers when this call's first postfix just looked them up, else a lookup; handed once.</summary>
        public static ItemLocalSums? Handed(ItemDrop.ItemData item)
        {
            ItemLocalSums? sums = ReferenceEquals(item, _handedItem) ? _handedSums : ItemLocalCache.Get(item);
            _handedItem = null;
            _handedSums = null;
            return sums;
        }

        private static void Postfix(ItemDrop.ItemData __instance, ref HitData.DamageTypes __result)
        {
            ItemLocalSums? sums = ItemLocalCache.Get(__instance);
            _handedItem = __instance;
            _handedSums = sums;
            if (sums != null && sums.HasBrand)
            {
                Brand(ref __result, sums);
            }
        }

        private static void Brand(ref HitData.DamageTypes damage, ItemLocalSums sums)
        {
            float combat = DamageSlots.Combat(in damage);
            for (int t = 0; t < DamageSlots.Count; t++)
            {
                float share = sums.BrandShare(t);
                if (share != 0f)
                {
                    DamageSlots.AddTo(ref damage, t, combat * share);
                }
            }
            float slash = sums.BrandShare(DamageSlots.Slash);
            if (slash != 0f && damage.m_chop > 0f)
            {
                damage.m_chop += combat * slash;
            }
        }
    }

    /// <summary>
    /// <c>draw_stamina_cost</c>: stamina drained per second while this bow is held drawn. Read by the drawing player's
    /// own client (Player.UpdateAttackBowDraw). Item-local: "this bow".
    /// </summary>
    [HarmonyPatch(typeof(ItemDrop.ItemData), nameof(ItemDrop.ItemData.GetDrawStaminaDrain))]
    internal static class DrawStaminaPatch
    {
        private static void Postfix(ItemDrop.ItemData __instance, ref float __result)
        {
            ItemLocalSums? sums = ItemLocalCache.Get(__instance);
            if (sums != null && __result > 0f)
            {
                __result *= Math.Max(0f, 1f - sums.Get(EffectKind.DrawStaminaCost));
            }
        }
    }

    /// <summary>
    /// <c>reload_speed</c>: this crossbow reloads X% faster, i.e. the reload time is divided by <c>1 + X/100</c>
    /// (judgement call: "faster" as a rate, like move speed). Read by the reloading player's own client.
    /// </summary>
    [HarmonyPatch(typeof(ItemDrop.ItemData), nameof(ItemDrop.ItemData.GetWeaponLoadingTime))]
    internal static class ReloadSpeedPatch
    {
        private static void Postfix(ItemDrop.ItemData __instance, ref float __result)
        {
            if (!__instance.m_shared.m_attack.m_requiresReload)
            {
                return;
            }
            ItemLocalSums? sums = ItemLocalCache.Get(__instance);
            if (sums != null)
            {
                __result /= 1f + sums.Get(EffectKind.ReloadSpeed);
            }
        }
    }

    /// <summary>
    /// <c>attack_eitr_cost</c>: eitr per attack with this staff. The game checks and spends it on the attacker's own
    /// client (Attack.Start, the magic trigger feedback). Item-local: reads the weapon passed in.
    /// </summary>
    [HarmonyPatch(typeof(Attack), nameof(Attack.GetAttackEitr), new[] { typeof(Character), typeof(ItemDrop.ItemData) })]
    internal static class AttackEitrPatch
    {
        private static void Postfix(ItemDrop.ItemData weapon, ref float __result)
        {
            ItemLocalSums? sums = ItemLocalCache.Get(weapon);
            if (sums != null && __result > 0f)
            {
                __result *= Math.Max(0f, 1f - sums.Get(EffectKind.AttackEitrCost));
            }
        }
    }
}
