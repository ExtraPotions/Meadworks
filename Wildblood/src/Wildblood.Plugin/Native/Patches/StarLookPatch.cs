// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using System;
using ExtraPotions.Wildblood.Adopted.Visuals;
using HarmonyLib;
using ExtraPotions.Wildblood.AdoptedLibs.PatchGuard;

namespace ExtraPotions.Wildblood.Adopted.Patches
{
    /// <summary>
    /// The dying creature's owner, as the game reads the tint to paint on its ragdoll: a starred creature's star look
    /// (see <see cref="StarLook"/>) instead of the level-1 one. Runs inside the game's death, so a failure is reported
    /// and swallowed, and the corpse keeps the game's tint.
    /// </summary>
    [HarmonyPatch(typeof(LevelEffects), nameof(LevelEffects.GetColorChanges))]
    public static class StarLookColorPatch
    {
        private static void Postfix(LevelEffects __instance, ref float hue, ref float saturation, ref float value)
        {
            LevelEffects.LevelSetup? setup = null;
            try
            {
                setup = StarLook.SetupFor(__instance);
            }
            catch (Exception e)
            {
                Guard.Report(e, "LevelEffects.GetColorChanges (star look)");
            }
            if (setup != null)
            {
                hue = setup.m_hue;
                saturation = setup.m_saturation;
                value = setup.m_value;
            }
        }
    }

    /// <summary>
    /// The owner, as a creature that is not a humanoid makes its ragdoll: the corpse keeps the star look (see
    /// <see cref="StarCorpse"/>). Humanoid overrides this without calling it, so it has its own patch below.
    /// </summary>
    [HarmonyPatch(typeof(Character), "OnRagdollCreated")]
    public static class StarCorpseMadePatch
    {
        private static void Postfix(Character __instance, Ragdoll ragdoll) =>
            SafeCall.Run("Character.OnRagdollCreated (star look)", static (dying, body) => StarCorpse.Made(dying, body), __instance,
                ragdoll);
    }

    /// <summary>The owner, as a humanoid creature makes its ragdoll: as <see cref="StarCorpseMadePatch"/>.</summary>
    [HarmonyPatch(typeof(Humanoid), "OnRagdollCreated")]
    public static class StarCorpseMadeHumanoidPatch
    {
        private static void Postfix(Humanoid __instance, Ragdoll ragdoll) =>
            SafeCall.Run("Humanoid.OnRagdollCreated (star look)", static (dying, body) => StarCorpse.Made(dying, body), __instance,
                ragdoll);
    }

    /// <summary>Every machine, as any ragdoll wakes: one recorded as a starred creature's corpse takes its look.</summary>
    [HarmonyPatch(typeof(Ragdoll), "Awake")]
    public static class StarCorpseWakePatch
    {
        private static void Postfix(Ragdoll __instance) =>
            SafeCall.Run("Ragdoll.Awake (star look)", static body => StarCorpse.Wake(body), __instance);
    }
}
