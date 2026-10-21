// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System;
using HarmonyLib;

namespace ExtraPotions.Edge.Runtime.Bows;

[HarmonyPatch(typeof(Attack), "UseAmmo")]
internal static class ArrowRecoveryUseAmmoPatch
{
    internal static void Prefix(Attack __instance, out (ItemDrop.ItemData? Ammo, int Stack) __state)
    {
        (ItemDrop.ItemData? Ammo, int Stack) state = default;
        ArrowRecoverySystem.Guard("ammo snapshot", () =>
        {
            if (!ArrowRecoverySystem.Enabled || Player.m_localPlayer == null ||
                !ReferenceEquals(__instance.m_character, Player.m_localPlayer) ||
                __instance.m_weapon?.m_shared.m_skillType != Skills.SkillType.Bows) return;
            var ammo = Attack.FindAmmo(__instance.m_character, __instance.m_weapon);
            if (ammo != null) state = (ammo, ammo.m_stack);
        });
        __state = state;
    }

    internal static void Postfix(Attack __instance, ref ItemDrop.ItemData ammoItem, bool __result,
        (ItemDrop.ItemData? Ammo, int Stack) __state)
    {
        var ammo = ammoItem;
        if (!__result || ammo == null || !ReferenceEquals(ammo, __state.Ammo)) return;
        ArrowRecoverySystem.Guard("fire", () =>
        {
            // UseAmmo also succeeds when Saga spares the arrow; only an actual inventory payment earns recovery.
            if (ammo.m_stack < __state.Stack || !__instance.m_character.GetInventory().ContainsItem(ammo))
                ArrowRecoverySystem.Paid(__instance, ammo);
        });
    }
}
