// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System;
using CruciblePlugin = ExtraPotions.Meadworks.Crucible.Plugin;

namespace ExtraPotions.Shepherd.Runtime.Integration
{
    internal static class WildbloodBreedingReadiness
    {
        internal const string Id = "meadworks.wildblood.breeding_ready";
        internal static readonly System.Version Version = new System.Version(1, 0);

        internal static bool Ready()
        {
            try
            {
                if (!CruciblePlugin.Services.Capabilities.TryResolve<Func<bool>>(Id, Version, out var ready)) return true;
                return ready != null && ready();
            }
            catch (Exception) { return false; }
        }
    }
}
