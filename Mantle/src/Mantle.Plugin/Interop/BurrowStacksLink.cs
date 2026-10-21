// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
using ExtraPotions.Meadworks.Contracts;
using ExtraPotions.Meadworks.Crucible;
using CruciblePlugin = ExtraPotions.Meadworks.Crucible.Plugin;

namespace Mantle.Interop
{
    /// <summary>
    /// Whether a product's Stacks feature (Burrow's) runs on this peer: the owner of <c>meadworks.owns.stacks</c> is
    /// there, whatever its switch says, as before this only asked whether Burrow had its <c>4. Stacks / Enabled</c> entry.
    /// That feature writes every item's stack size and reads the key ring from Mantle's <c>meadworks.inventory.keyring</c>
    /// (its own key rule, kept with its Stacks module off too), so Mantle's <see cref="Ring.KeyStacks"/> steps aside, as
    /// it does for OpenKeep. Read through Crucible, never by plugin lookup; a provider registering late is still seen.
    /// </summary>
    public static class BurrowStacksLink
    {
        private static readonly FeatureLink owner = new FeatureLink(FeatureOwnerContract.Id(FeatureOwnerContract.Stacks));

        public static bool Present => owner.TryOwned(CruciblePlugin.IsReady ? CruciblePlugin.Services.Capabilities : null, out _);
    }
}
