// Adapted from assets/references/source/ValheimMods-openkeep-3.0.0/GrindstoneSkills/GrindstoneSkills/Woodcutting/Perks/ReplantWait.cs at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Copyright (C) 2026 Gerald Glasgow. SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Saga namespace, configuration, CS1 and ownership boundaries.
using HarmonyLib;
using UnityEngine;

namespace ExtraPotions.Saga.Runtime.Adopted.Grindstone
{
    /// <summary>
    /// A replanted sapling waits under the felled log instead of dying. The log falls beside the sapling, and logs are on
    /// the Default layer, which the game's grow-space and roof checks read: while the log lies there the sapling reads
    /// "no space" or "no sun" (Plant.UpdateHealth). On the sapling's owner, once its grow time has passed, Plant.SUpdate
    /// calls Plant.Grow at every check; Grow destroys a sapling that is not healthy (m_destroyIfCantGrow, set on every
    /// tree sapling), so a log left for an hour would kill it.
    /// <para>A prefix skips Grow for a sapling marked by <see cref="Replanting"/> while it is blocked and a log (TreeLog)
    /// lies within its grow radius: it stays and tries again at the next check, and grows as soon as the logs are cleared.
    /// Anything else in the way kills it as the game would, and saplings players plant keep the game's rule. Nothing
    /// changes while Woodcutting is off.</para>
    /// </summary>
    public static class ReplantWait
    {
        private static readonly Collider[] Hits = new Collider[32];

        [HarmonyPatch(typeof(Plant), nameof(Plant.Grow))]
        private static class Growing
        {
            // Every plant whose grow time has passed comes here every ten seconds: the cheap checks run first.
            [HarmonyPrefix]
            private static bool Prefix(Plant __instance, ref GameObject __result)
            {
                if (!WoodSkill.Active || !Blocked(__instance.GetStatus()) || !HookGuard.Run("replanting", static plant => Waits(plant), __instance, false))
                    return true;
                __result = null;
                return false;
            }
        }

        private static bool Waits(Plant plant) => Replanting.IsReplanted(plant) && UnderLog(plant);

        private static bool Blocked(Plant.Status status) => status == Plant.Status.NoSpace || status == Plant.Status.NoSun;

        private static bool UnderLog(Plant plant)
        {
            int count = Physics.OverlapSphereNonAlloc(plant.transform.position, plant.m_growRadius, Hits, ReplantSpace.SpaceMask);
            for (int i = 0; i < count; i++)
            {
                if (Hits[i] != null && Hits[i].GetComponentInParent<TreeLog>() != null)
                    return true;
            }
            return false;
        }
    }
}
