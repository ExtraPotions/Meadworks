// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System;
using ExtraPotions.Wildblood.Config;
using ExtraPotions.Wildblood.Runtime;
using CruciblePlugin = ExtraPotions.Meadworks.Crucible.Plugin;

namespace ExtraPotions.Wildblood.Adopted.Adapters
{
    /// <summary>Live ownership of the minimum trait carry. No breeding producer or plugin-name inference.</summary>
    internal static class ShepherdBreedingOwnership
    {
        internal const string Capability = "meadworks.husbandry.breeding";
        private static readonly System.Version Version = new System.Version(1, 0);

        internal static bool FallbackActive
        {
            get
            {
                try
                {
                    if (!WildbloodModules.ProductActive(out _) ||
                        ExtraPotions.Wildblood.Runtime.Plugin.Settings?.Get(WildbloodSettings.General.Enabled) != true)
                        return false;
                    return FallbackOwnsBirth;
                }
                catch (Exception)
                {
                    // A provider that cannot report ownership is not evidence that its producers stopped.
                    return false;
                }
            }
        }

        // Ownership must be queried before remote settings have synchronized.
        internal static bool FallbackOwnsBirth
        {
            get
            {
                try
                {
                    return !CruciblePlugin.Services.Capabilities.TryResolve<Func<bool>>(Capability, Version, out var active)
                        || active == null || !active();
                }
                catch (Exception) { return false; }
            }
        }
    }
}
