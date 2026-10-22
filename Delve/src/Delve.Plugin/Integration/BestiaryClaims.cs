// Delve - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: the authority calls Bestiary's claim reset after a full regenerate has finished.
using System;
using ExtraPotions.Delve.Commands;
using ExtraPotions.Delve.Config;
using CruciblePlugin = ExtraPotions.Meadworks.Crucible.Plugin;

namespace ExtraPotions.Delve.Runtime
{
    internal static class BestiaryClaims
    {
        /// <summary>The armed regenerate has finished. Still the authority only; a client never writes the keys.</summary>
        public static void ClearFinished()
        {
            if (Plugin.Settings != null && Plugin.Settings.Get(DelveSettings.General.DryRunAll))
            {
                Plugin.Log?.Info("Delve left Bestiary region claims in place (Dry Run All).");
                return;
            }
            if (ZNet.instance == null || !ZNet.instance.IsServer()) return;
            try
            {
                if (!CruciblePlugin.Services.Capabilities.TryResolve<Func<int>>(BestiaryClaimReset.CapabilityId, BestiaryClaimReset.Version, out var reset) || reset == null)
                {
                    Plugin.Log?.Info("Delve region claims were not cleared: meadworks.bestiary.world is not published.");
                    return;
                }
                int cleared = BestiaryClaimReset.Apply(reset, true);
                Plugin.Log?.Info("Delve region claims cleared: " + cleared + ".");
            }
            catch (Exception error)
            {
                Plugin.Log?.Warning("Delve could not clear Bestiary region claims: " + error.GetType().Name + ": " + error.Message);
            }
        }
    }
}
