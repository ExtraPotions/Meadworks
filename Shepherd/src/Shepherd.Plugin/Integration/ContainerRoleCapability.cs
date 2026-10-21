// Shepherd - ExtraPotions Meadworks animal husbandry.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
using System;
using ExtraPotions.Meadworks.Crucible;
using ExtraPotions.Shepherd.Troughs;

namespace ExtraPotions.Shepherd.Runtime.Integration
{
    /// <summary>
    /// Shepherd's side of the Burrow contract (design "Burrow contracts", Burrow BS1): capability
    /// <c>meadworks.containers.role</c>, version 1.0, contract <c>Func&lt;object, string&gt;</c>. Given a game
    /// <c>Container</c> it answers <c>feed</c> for a Shepherd trough (by prefab name) and null for anything else, so
    /// Burrow excludes troughs from materials, Top Up and routing. Trough objects (alias-loaded
    /// grindstone_feeder ZDOs included) carry the Shepherd prefab name. Called on the Unity main thread only; never throws.
    /// </summary>
    public static class ContainerRoleCapability
    {
        public const string Id = "meadworks.containers.role";

        public static readonly System.Version ContractVersion = new System.Version(1, 0);

        /// <summary>The provider registered with Crucible (a BCL delegate, so consumers resolve it without referencing Shepherd).</summary>
        public static readonly Func<object, string> Provider = RoleOf;

        public static void Register(ProductRegistration registration)
        {
            if (registration == null) throw new ArgumentNullException(nameof(registration));
            registration.AddCapability<Func<object, string>>(Id, ContractVersion, Provider);
        }

        private static string RoleOf(object container)
        {
            try
            {
                return container is Container chest && chest != null
                    ? TroughPrefabs.RoleOf(global::Utils.GetPrefabName(chest.gameObject))!
                    : null!;
            }
            catch (Exception)
            {
                return null!;
            }
        }
    }
}
