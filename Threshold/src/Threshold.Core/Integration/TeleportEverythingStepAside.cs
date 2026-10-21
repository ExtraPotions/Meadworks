// Threshold - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: feature step-aside for OdinPlus TeleportEverything (T19). Only Threshold's TeleportEverything-style
// extras (Teleport Listed Items, Bring Tames) yield; portal routing stays Threshold's.
using System;
using System.Collections.Generic;
using System.Linq;

namespace ExtraPotions.Threshold.Integration
{
    public static class TeleportEverythingStepAside
    {
        /// <summary>PROVISIONAL. The public Thunderstore page states no BepInEx GUID; this is inferred from the config file
        /// it names (odinplus.TeleportEverything.cfg; BepInEx names a plugin's config after its GUID). Confirm against a
        /// loaded install (BepInEx log or Chainloader.PluginInfos) and correct here.</summary>
        public const string Guid = "odinplus.TeleportEverything";

        public const string Name = "TeleportEverything";

        /// <summary>The main plugin or a module of it (GUID equal to, or starting with, <see cref="Guid"/> and a dot), any case.</summary>
        public static bool Installed(IEnumerable<string> installedGuids)
        {
            if (installedGuids == null) throw new ArgumentNullException(nameof(installedGuids));
            return installedGuids.Any(g => g != null
                && (string.Equals(g, Guid, StringComparison.OrdinalIgnoreCase) || g.StartsWith(Guid + ".", StringComparison.OrdinalIgnoreCase)));
        }

        /// <summary>Listed items and tames act only while Threshold routes and TeleportEverything is absent.</summary>
        public static bool ExtrasActive(bool teleportEverythingInstalled, bool thresholdActive) => thresholdActive && !teleportEverythingInstalled;
    }
}
