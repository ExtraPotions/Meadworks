// Kindle - ExtraPotions Meadworks crafting conveniences.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
using System;
using ExtraPotions.Meadworks.Crucible;

namespace ExtraPotions.Kindle.Runtime
{
    /// <summary>
    /// Kindle's side of the Burrow contract (design "Burrow contracts", Burrow KB1): capability
    /// <c>meadworks.crafting.salvage_tab</c>, version 1.0, contract <c>Func&lt;bool&gt;</c>, true while Kindle's Salvage tab
    /// is the active crafting tab so Burrow leaves the requirement rows to Kindle. The tab arrives with K07; until then
    /// the provider answers false. Called on the Unity main thread only.
    /// </summary>
    public static class SalvageTabCapability
    {
        public const string Id = "meadworks.crafting.salvage_tab";

        public static readonly System.Version ContractVersion = new System.Version(1, 0);

        /// <summary>Set by the Salvage tab (K07) while it is the active crafting tab.</summary>
        public static bool Active { get; internal set; }

        /// <summary>The provider registered with Crucible (a BCL delegate, so consumers resolve it without referencing Kindle).</summary>
        public static readonly Func<bool> Provider = () => Active;

        public static void Register(ProductRegistration registration)
        {
            if (registration == null) throw new ArgumentNullException(nameof(registration));
            registration.AddCapability<Func<bool>>(Id, ContractVersion, Provider);
        }
    }
}
