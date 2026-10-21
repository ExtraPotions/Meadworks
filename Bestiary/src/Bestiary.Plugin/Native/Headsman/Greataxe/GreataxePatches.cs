// Portions Copyright (C) 2026 Gerald Glasgow. EliteCreaturesPack 0.7.2 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Changed by expDARE, 2026: Bestiary namespace, owned Crucible settings, rebuilt-only bundles and lifecycle adapters.
// SPDX-License-Identifier: GPL-3.0-or-later
using HarmonyLib;

namespace ExtraPotions.Bestiary.Runtime.Native.Headsman
{
    /// <summary>
    /// A greataxe swing, on the attacker's machine as the game starts it: the second step of the combo, the spin, hits
    /// all round. Every swing sounds as the Battleaxe's do (the item's own trail and hit sounds, the game's).
    /// </summary>
    [HarmonyPatch(typeof(Attack), nameof(Attack.Start))]
    public static class GreataxeSwing
    {
        private const string Primary = "battleaxe_attack";
        private const string AxeName = "$item_" + GreataxeItems.AxeWord;

        private static void Postfix(Attack __instance, ItemDrop.ItemData weapon, bool __result)
        {
            if (__result && weapon?.m_shared.m_name == AxeName
                && __instance.m_attackAnimation == Primary && __instance.m_currentAttackCainLevel == 1)
            {
                __instance.m_attackAngle = 360f;
            }
        }
    }
}
