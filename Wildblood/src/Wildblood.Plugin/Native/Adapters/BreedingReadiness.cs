// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System;
using ExtraPotions.Meadworks.Crucible;
using ExtraPotions.Wildblood.Config;
using ExtraPotions.Wildblood.Runtime;
using CruciblePlugin = ExtraPotions.Meadworks.Crucible.Plugin;
using WildbloodPlugin = ExtraPotions.Wildblood.Runtime.Plugin;

namespace ExtraPotions.Wildblood.Adopted.Adapters
{
    /// <summary>False holds the pregnancy before a newborn or egg exists; true does not grant star inheritance.</summary>
    public static class BreedingReadiness
    {
        public const string Id = "meadworks.wildblood.breeding_ready";
        public static readonly System.Version Version = new System.Version(1, 0);

        public static void Register(ProductRegistration registration)
        {
            if (registration == null) throw new ArgumentNullException(nameof(registration));
            registration.AddCapability<Func<bool>>(Id, Version, Ready);
        }

        public static bool Ready()
        {
            try
            {
                if (WildbloodPlugin.NativeRulesReady) return true;
                if (!CruciblePlugin.Session.AuthorityReady) return false;
                if (!WildbloodModules.ProductActive(out var reason)
                    && (reason == ConfigBlockReason.ProductDisabled || reason == ConfigBlockReason.VanillaServer)) return true;
                if (!CruciblePlugin.Session.IsAuthority) return false;
                // Remote saved settings may still precede the server's config package.
                return !WildbloodModules.ProductActive(out _)
                    || WildbloodPlugin.Settings?.Get(WildbloodSettings.General.Enabled) == false;
            }
            catch (Exception) { return false; }
        }
    }
}
