// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Original optional integration with Meadworks' own Edge; no third-party code is embedded.
using System;
using ExtraPotions.Meadworks.Contracts;
using ExtraPotions.Meadworks.Crucible;
namespace ExtraPotions.Hex.Runtime;

/// <summary>
/// Hex's "no durability loss" for a product that spends durability itself (Edge's secondary attacks): Hex provides
/// <c>meadworks.items.wear_exempt</c> and the spender asks before draining, instead of Hex patching that product's private
/// method (user decision 2026-10-12 22:41). Without a spender asking, nothing changes.
/// </summary>
internal static class EdgeWear
{
    internal static void Register(ProductRegistration registration) =>
        registration.AddCapability<Func<object, object, bool>>(WearExemptContract.Id, WearExemptContract.ContractVersion, Exempt);

    /// <summary>
    /// Only the wear debit is skipped. The spender still owns its attack, payment admission, stamina/eitr/health spending,
    /// cooldowns, effects, resources and already released projectiles.
    /// </summary>
    internal static bool Exempt(object character, object item) =>
        Plugin.Active && character is Humanoid owner && item is ItemDrop.ItemData weapon
        && Native.HexMovement.On(owner, "disable_durability_loss") && owner.GetInventory().ContainsItem(weapon);
}
