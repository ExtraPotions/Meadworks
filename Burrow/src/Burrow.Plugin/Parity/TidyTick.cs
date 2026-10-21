// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
using HarmonyLib;

namespace Burrow.Stow
{
    /// <summary>
    /// Auto Tidy's clock (parity choice #54): the game's once-a-second <c>Container.CheckForChanges</c> on every peer hands
    /// the container and its revision before the check to <see cref="TidySchedule.Tick"/>, as OpenKeep 3.0.0 does from its
    /// own container tick. The schedule itself returns at once while Auto Tidy or the Stow module is off.
    /// </summary>
    [HarmonyPatch(typeof(Container), nameof(Container.CheckForChanges))]
    public static class TidyTick
    {
        [HarmonyPrefix]
        public static void Prefix(Container __instance, out uint __state) => __state = __instance.m_lastRevision;

        [HarmonyPostfix]
        public static void Postfix(Container __instance, uint __state) => TidySchedule.Tick(__instance, __state);
    }
}
