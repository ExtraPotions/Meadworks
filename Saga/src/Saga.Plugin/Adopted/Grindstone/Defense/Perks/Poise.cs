// Adapted from assets/references/source/ValheimMods-openkeep-3.0.0/GrindstoneSkills/GrindstoneSkills/Defense/Perks/Poise.cs at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Copyright (C) 2026 Gerald Glasgow. SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Saga namespace, configuration, CS1 and ownership boundaries.
using HarmonyLib;

namespace ExtraPotions.Saga.Runtime.Adopted.Grindstone
{
    /// <summary>
    /// Poise. The game staggers a character once its stagger damage reaches its stagger threshold,
    /// Character.GetStaggerTreshold: max health times m_staggerDamageFactor. A block that staggers its blocker breaks the
    /// guard (Humanoid.BlockAttack lets the damage through). Every stagger check on the local player runs on their own
    /// client (AddStaggerDamage, from RPC_Damage and BlockAttack), so raising the threshold there is all it takes. The
    /// stagger bar reads the threshold too, and follows. Max Health already raises it a little on its own.
    /// </summary>
    [HarmonyPatch(typeof(Character), nameof(Character.GetStaggerTreshold))]
    public static class Poise
    {
        [HarmonyPostfix]
        private static void Postfix(Character __instance, ref float __result)
        {
            if (DefenseSkill.IsLocal(__instance))
                __result *= 1f + DefenseSkill.LocalShare(DefenseSettings.Poise.Value);
        }
    }
}
