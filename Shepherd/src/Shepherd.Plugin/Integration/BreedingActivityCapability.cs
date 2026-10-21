// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
using System;
using ExtraPotions.Meadworks.Crucible;
using ExtraPotions.Shepherd.Runtime.Breeding;

namespace ExtraPotions.Shepherd.Runtime.Integration
{
    /// <summary>Live birth/growth ownership for optional consumers; no messages or additional breeding producer.</summary>
    public static class BreedingActivityCapability
    {
        public const string Id = "meadworks.husbandry.breeding";
        public static readonly System.Version ContractVersion = new System.Version(1, 0);
        public static readonly Func<bool> Provider = Active;
        public static void Register(ProductRegistration registration)
        {
            if (registration == null) throw new ArgumentNullException(nameof(registration));
            registration.AddCapability(Id, ContractVersion, Provider);
        }
        private static bool Active()
        {
            try { return NativeBreeding.Active; }
            catch (Exception) { return false; }
        }
    }
}
