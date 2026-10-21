// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using ExtraPotions.Wildblood.Adopted.Aspects;
using HarmonyLib;

namespace ExtraPotions.Wildblood.Adopted.Patches
{
    /// <summary>
    /// The boss's owner, as the game makes a dying boss's ragdoll: a Colossal boss's corpse is grown to its size (see
    /// <see cref="ColossalCorpse"/>). Runs inside the game's death, so a failure is reported and swallowed.
    /// </summary>
    [HarmonyPatch(typeof(Humanoid), "OnRagdollCreated")]
    public static class ColossalCorpseMadePatch
    {
        private static void Postfix(Humanoid __instance, Ragdoll ragdoll) =>
            SafeCall.Run("Humanoid.OnRagdollCreated (Colossal)", static (dying, body) => ColossalCorpse.Grow(dying, body), __instance,
                ragdoll);
    }

    /// <summary>
    /// Every machine, as any ragdoll wakes: one recorded as a Colossal boss's corpse grows to the size on its record.
    /// </summary>
    [HarmonyPatch(typeof(Ragdoll), "Awake")]
    public static class ColossalCorpseWakePatch
    {
        private static void Postfix(Ragdoll __instance) =>
            SafeCall.Run("Ragdoll.Awake (Colossal)", static body => ColossalCorpse.Wake(body), __instance);
    }
}
