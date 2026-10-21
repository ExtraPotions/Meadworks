// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using ExtraPotions.Meadworks.Contracts;
using ExtraPotions.Meadworks.Crucible;
using CruciblePlugin = ExtraPotions.Meadworks.Crucible.Plugin;
namespace ExtraPotions.Inscribe.Runtime
{
    /// <summary>
    /// A product owns the elite creature facts (<c>meadworks.owns.elite_creatures</c>, Wildblood): ECR's keys are then not
    /// read. Presence only, as before: the owner's running answer is ignored.
    /// </summary>
    internal static class EliteCreaturesOwner
    {
        private static readonly FeatureLink link = new FeatureLink(FeatureOwnerContract.Id(FeatureOwnerContract.EliteCreatures));
        internal static bool Present => CruciblePlugin.IsReady && link.TryOwned(CruciblePlugin.Services.Capabilities, out _);
    }
}
