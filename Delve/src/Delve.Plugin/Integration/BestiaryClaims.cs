// Delve - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: the server calls Bestiary's claim reset when an admin regenerates every kind.
using System;
using System.Collections.Generic;
using ExtraPotions.Delve.Commands;
using ExtraPotions.Delve.Engine;
using ExtraPotions.Meadworks.Crucible;
using CruciblePlugin = ExtraPotions.Meadworks.Crucible.Plugin;

namespace ExtraPotions.Delve.Runtime
{
    internal static class BestiaryClaims
    {
        public static void ClearIf(DelveCommand command, bool dryRunAll) 
        {
            if (BestiaryClaimReset.Clears(command)) Clear(dryRunAll);
        }

        public static void ClearIf(IReadOnlyList<DelveKind> kinds, bool dryRunAll)
        {
            if (BestiaryClaimReset.CoversEveryKind(kinds)) Clear(dryRunAll);
        }

        private static void Clear(bool dryRunAll)
        {
            if (dryRunAll)
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
