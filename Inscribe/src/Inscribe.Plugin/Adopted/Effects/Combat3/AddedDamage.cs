// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
using HarmonyLib;

namespace ExtraPotions.Inscribe.Adopted.Effects.Combat3
{
    /// <summary>
    /// <c>added_damage</c> (the brands Emberbrand to Spiritbrand, Bonebreaker, Keen Edge, Needlepoint): this weapon's
    /// damage block gets X more of the param type, after quality and world level (the game computes those inside
    /// GetDamage, this postfix adds on the result). Like the percent brands, a slash amount also adds to chop when the
    /// item chops (Keen Edge on an axe), and a group param splits its amount over the group's types. Fire and poison
    /// then land partly over time, the game's own way, on the target's owner.
    /// <para>
    /// Runs wherever the number is asked for: on the attacker's client when the hit is built (so the added damage
    /// travels inside the HitData), and on any peer for the tooltip, from the item's replicated data. Low priority, so
    /// the percent brands measure the item's own damage before anything flat is added. The item's local numbers sit on
    /// its cached state (<see cref="ItemLocalCache"/>), handed over by the brand postfix of the same call.
    /// </para>
    /// </summary>
    [HarmonyPatch(typeof(ItemDrop.ItemData), nameof(ItemDrop.ItemData.GetDamage), new[] { typeof(int), typeof(float) })]
    internal static class AddedDamagePatch
    {
        [HarmonyPriority(Priority.Low)]
        private static void Postfix(ItemDrop.ItemData __instance, ref HitData.DamageTypes __result)
        {
            ItemLocalSums? sums = ItemDamagePatch.Handed(__instance);
            if (sums != null && sums.HasAdded)
            {
                Add(ref __result, sums);
            }
        }

        private static void Add(ref HitData.DamageTypes damage, ItemLocalSums sums)
        {
            for (int t = 0; t < DamageSlots.Count; t++)
            {
                float flat = sums.AddedFlat(t);
                if (flat != 0f)
                {
                    DamageSlots.AddTo(ref damage, t, flat);
                }
            }
            float slash = sums.AddedFlat(DamageSlots.Slash);
            if (slash != 0f && damage.m_chop > 0f)
            {
                damage.m_chop += slash;
            }
        }
    }
}
