// Kindle - ExtraPotions Meadworks crafting conveniences.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
using System;
using ExtraPotions.Kindle.Runtime.Salvage;
using ExtraPotions.Meadworks.Crucible;

namespace ExtraPotions.Kindle.Runtime
{
    /// <summary>
    /// Kindle's side of Burrow's "Trash Uses Salvage" contract (Burrow KB2), version 1.0:
    /// <c>meadworks.crafting.salvage_can</c> (<c>Func&lt;object, bool&gt;</c>, true when an <c>ItemDrop.ItemData</c> in the local
    /// player's inventory can be salvaged now) and <c>meadworks.crafting.salvage_item</c> (<c>Func&lt;object, object, bool&gt;</c>,
    /// salvages the whole stack for (player, item); true when it did). Both go through <see cref="SalvageActions"/>, so the
    /// Salvage settings, blockers, fit check and rollback are the Salvage tab's own. Called on the Unity main thread only.
    /// </summary>
    public static class SalvageStackCapability
    {
        public const string CanId = "meadworks.crafting.salvage_can";
        public const string ItemId = "meadworks.crafting.salvage_item";

        public static readonly System.Version ContractVersion = new System.Version(1, 0);

        public static readonly Func<object, bool> Can = item => item is ItemDrop.ItemData data && SalvageActions.CanSalvage(data);

        public static readonly Func<object, object, bool> Salvage =
            (player, item) => player is Player actor && item is ItemDrop.ItemData data && SalvageActions.Salvage(actor, data);

        public static void Register(ProductRegistration registration)
        {
            if (registration == null) throw new ArgumentNullException(nameof(registration));
            registration.AddCapability<Func<object, bool>>(CanId, ContractVersion, Can);
            registration.AddCapability<Func<object, object, bool>>(ItemId, ContractVersion, Salvage);
        }
    }
}
