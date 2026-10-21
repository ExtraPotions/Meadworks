// Delve - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: stable site identity through Crucible C12 (design 7): a dungeon generator or a boss place keeps one
// token across world loads, so per-site state (visits, full reset clocks, remembered spots) survives.
using ExtraPotions.Meadworks.Crucible;

namespace ExtraPotions.Delve.Runtime
{
    public static class DelveSites
    {
        /// <summary>True when Delve's key is the one Crucible's C12 rule gives the product namespace.</summary>
        public static bool KeyMatchesCrucible => InstanceIdentity.KeyFor(DelveCore.TokenNamespace) == DelveCore.InstanceKey;

        /// <summary>The site's token, assigned on first use by the owner (the authority claims ownership before writing).</summary>
        public static InstanceIdentityResult Ensure(ZDO site) => MeadworksInstanceIdentity.Ensure(site, DelveCore.InstanceKey);

        /// <summary>The site's token when it has one; null otherwise.</summary>
        public static InstanceToken? Read(ZDO site) => site == null ? (InstanceToken?)null : MeadworksInstanceIdentity.Read(site, DelveCore.InstanceKey);
    }
}
