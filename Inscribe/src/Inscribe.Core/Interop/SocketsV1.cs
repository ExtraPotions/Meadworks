// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System;
namespace ExtraPotions.Inscribe.Interop
{
    /// <summary>
    /// <c>meadworks.items.sockets</c> v1 (Inscribe provides; Mantle consumes for backpack sockets). Opaque native item input.
    /// Item1 returns the item's socket count (0 when it has none). Item2 returns one row per socket:
    /// (gem id or "" when empty, localized gem name or "", gem color as #RRGGBB or "", localized stat line or "").
    /// </summary>
    public static class SocketsV1
    {
        public const string Capability = "meadworks.items.sockets";
        public static readonly Version Version = new Version(1, 0);
        public static Type ContractType => typeof(Tuple<Func<object, int>, Func<object, Tuple<string, string, string, string>[]>>);
    }

    /// <summary>
    /// <c>meadworks.crafting.salvage_rune</c> v1 (Inscribe provides; a salvaging product such as Kindle consumes beside its
    /// <c>meadworks.crafting.salvage_item</c>). Opaque native item input. Item1 returns the rune prefab a salvaged Magic or Rare
    /// item may give back, or null; Item2 returns the chance 0 to 1 (0 when the server turned it off). The consumer rolls.
    /// </summary>
    public static class SalvageRuneV1
    {
        public const string Capability = "meadworks.crafting.salvage_rune";
        public static readonly Version Version = new Version(1, 0);
        public static Type ContractType => typeof(Tuple<Func<object, string>, Func<object, float>>);
    }
}

namespace ExtraPotions.Inscribe.Interop
{
    /// <summary>
    /// <c>meadworks.crafting.salvage_gems</c> v1 (Inscribe provides; a salvaging product such as Kindle consumes beside
    /// <see cref="SalvageRuneV1"/>). Opaque native item input. Item1: the prefab names of the gems set in the item's
    /// sockets (empty when none or Gems and sockets is off); Item2: the chance 0 to 1 that each comes back, by the item's
    /// rarity (host settings); Item3: the localized message for this many gems that did not come back. The consumer rolls
    /// each gem once, when it salvages.
    /// </summary>
    public static class SalvageGemsV1
    {
        public const string Capability = "meadworks.crafting.salvage_gems";
        public static readonly Version Version = new Version(1, 0);
        public static Type ContractType => typeof(Tuple<Func<object, string[]>, Func<object, float>, Func<int, string>>);
    }
}
