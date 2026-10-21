// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
using System;
using ExtraPotions.Inscribe.Adopted.Core;
using HarmonyLib;

namespace ExtraPotions.Inscribe.Adopted.Loot
{
    /// <summary>
    /// The game fills a world container with its default items in <c>Container.AddDefaultItems</c>, called once from the
    /// container's <c>Awake</c> on the ZDO owner, only while the ZDO's "default items added" flag is unset (verified in
    /// the decompile); the flag is set right after. A postfix there rolls our loot into the same container
    /// (<see cref="ChestRoller"/>). A failure is logged and swallowed: a throw here would abort the container's
    /// <c>Awake</c> and leave the chest broken.
    /// </summary>
    [HarmonyPatch(typeof(Container), nameof(Container.AddDefaultItems))]
    internal static class ChestFillPatch
    {
        private static void Postfix(Container __instance)
        {
            try
            {
                if (Runtime.InscribeModules.Active(global::ExtraPotions.Inscribe.Integration.InscribeModule.Drops) && Runtime.Plugin.Settings!.Get(global::ExtraPotions.Inscribe.Config.InscribeSettings.Drops.ChestDrops)) ChestRoller.OnDefaultItems(__instance);
            }
            catch (Exception e)
            {
                Log.Error($"chest drop roll failed: {e}");
            }
        }
    }
}
