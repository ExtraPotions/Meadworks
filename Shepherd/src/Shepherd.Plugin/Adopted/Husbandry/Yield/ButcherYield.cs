// Adapted from ValheimMods GrindstoneSkills/GrindstoneSkills/Husbandry/Yield/ButcherYield.cs
// Copyright (C) 2026 Gerald Glasgow. GPL-3.0-or-later.
// Pin: ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: namespace; NativeHusbandry exception guard; the scaling decided by the portable YieldMath.
// SPDX-License-Identifier: GPL-3.0-or-later
using System.Collections.Generic;
using ExtraPotions.Shepherd.Yield;
using HarmonyLib;
using UnityEngine;

namespace ExtraPotions.Shepherd.Runtime.Adopted.Husbandry
{
    /// <summary>
    /// Butcher Yield, on the creature's owner. CharacterDrop.GenerateDropList rolls the creature's drops into a list of
    /// (prefab, amount), already multiplied for its stars and capped at 100 each; both drop paths call it inside
    /// Character.OnDeath. A postfix, only while a <see cref="Butchering"/> context for this creature is open with a player
    /// killer, multiplies every entry that is not a trophy by 1 + the killer's share of "Butcher Yield At 100"; the
    /// fraction is a chance of one more. The list's pairs are immutable, so each scaled entry is replaced.
    /// </summary>
    public static class ButcherYield
    {
        [HarmonyPatch(typeof(CharacterDrop), nameof(CharacterDrop.GenerateDropList))]
        private static class Generate
        {
            [HarmonyPostfix]
            private static void Postfix(CharacterDrop __instance, List<KeyValuePair<GameObject, int>> __result)
            {
                ButcherContext? butcher = Butchering.Open;
                if (butcher != null && butcher.Killer != null && __result != null && __instance.m_character == butcher.Creature)
                    NativeHusbandry.Run("butcher yield", () => Scale(__result, butcher));
            }
        }

        private static void Scale(List<KeyValuePair<GameObject, int>> drops, ButcherContext butcher)
        {
            float share = HusbandrySkill.Share(HusbandryYieldSettings.ButcherYield.Value, butcher.KillerLevel);
            if (share <= 0f)
                return;
            for (int i = 0; i < drops.Count; i++)
            {
                KeyValuePair<GameObject, int> drop = drops[i];
                if (drop.Value > 0 && Scales(drop.Key))
                    drops[i] = new KeyValuePair<GameObject, int>(drop.Key, YieldMath.Butchered(drop.Value, share, YieldDice.Instance));
            }
        }

        /// <summary>An item that is not a trophy; anything else in a drop list is left alone.</summary>
        private static bool Scales(GameObject prefab)
        {
            ItemDrop? item = prefab != null ? prefab.GetComponent<ItemDrop>() : null;
            return item != null && item.m_itemData?.m_shared != null
                && item.m_itemData.m_shared.m_itemType != ItemDrop.ItemData.ItemType.Trophy;
        }
    }
}
