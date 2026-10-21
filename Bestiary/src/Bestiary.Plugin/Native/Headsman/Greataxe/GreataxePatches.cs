// Portions Copyright (C) 2026 Gerald Glasgow. EliteCreaturesPack 0.7.2 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Changed by expDARE, 2026: Bestiary namespace, owned Crucible settings, rebuilt-only bundles and lifecycle adapters.
// SPDX-License-Identifier: GPL-3.0-or-later
using ExtraPotions.Bestiary.Fixes;
using HarmonyLib;
using UnityEngine;

namespace ExtraPotions.Bestiary.Runtime.Native.Headsman
{
    /// <summary>
    /// A greataxe swing, on the attacker's machine as the game starts it: the second step of the combo, the spin, hits
    /// all round. Every other step uses the axe's configured arc. The angle is written on this item's own attack.
    /// Every swing sounds as the Battleaxe's do (the item's own trail and hit sounds, the game's).
    /// </summary>
    [HarmonyPatch(typeof(Attack), nameof(Attack.Start))]
    public static class GreataxeSwing
    {
        private const string Primary = "battleaxe_attack";
        private const string AxeName = "$item_" + GreataxeItems.AxeWord;

        private static void Postfix(Attack __instance, ItemDrop.ItemData weapon, bool __result)
        {
            if (!__result || weapon?.m_shared == null || weapon.m_shared.m_name != AxeName
                || __instance.m_attackAnimation != Primary)
            {
                return;
            }
            __instance.m_attackAngle = SharedAttackRules.SwingAngle(
                __instance.m_currentAttackCainLevel, ConfiguredAngle(weapon, __instance));
        }

        /// <summary>The arc on the prefab. A spin must not read the angle it already wrote onto this item.</summary>
        private static float ConfiguredAngle(ItemDrop.ItemData weapon, Attack attack)
        {
            GameObject? prefab = weapon.m_dropPrefab;
            Attack? source = prefab != null
                ? prefab.GetComponent<ItemDrop>()?.m_itemData?.m_shared?.m_attack
                : null;
            if (source != null && !ReferenceEquals(source, attack))
            {
                return source.m_attackAngle;
            }
            return attack.m_attackAngle;
        }
    }
}
