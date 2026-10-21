// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
using System;
using CruciblePlugin = ExtraPotions.Meadworks.Crucible.Plugin;

namespace ExtraPotions.Shepherd.Runtime.Integration
{
    /// <summary>
    /// Wildblood's optional birth-star rule. No assembly reference: an absent or failing provider
    /// means the newborn keeps the level the game already gave it.
    /// </summary>
    internal static class WildbloodOffspringStars
    {
        internal const string Id = "meadworks.wildblood.offspring_stars";
        internal static readonly System.Version Version = new System.Version(1, 0);

        internal static bool Allow()
        {
            try
            {
                if (!CruciblePlugin.Services.Capabilities.TryResolve<Func<bool>>(Id, Version, out var allow) || allow == null)
                    return false;
                return allow();
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
