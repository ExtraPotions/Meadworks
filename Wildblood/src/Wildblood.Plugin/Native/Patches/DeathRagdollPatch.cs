// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using ExtraPotions.Wildblood.Adopted.Mutations;
using HarmonyLib;

namespace ExtraPotions.Wildblood.Adopted.Patches
{
    /// <summary>
    /// The owner of a dying creature, as the game sets up the ragdoll its death just made: noted, so a Bloated death names
    /// its own corpse (see <see cref="DeathRagdoll"/>). Runs inside the game's death, so a failure is reported and
    /// swallowed.
    /// </summary>
    [HarmonyPatch(typeof(Ragdoll), "Setup")]
    public static class DeathRagdollPatch
    {
        private static void Postfix(Ragdoll __instance) =>
            SafeCall.Run("Ragdoll.Setup (Bloated corpse)", () => DeathRagdoll.Made(__instance));
    }
}
