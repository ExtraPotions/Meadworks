// Kindle - ExtraPotions Meadworks crafting conveniences.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Behaviour follows OpenKeep 4.0.0 (Salvage: with EliteCrafting, Magic and Rare items are salvaged and may give a rune
// back, chance shown); Meadworks-original code that reaches the provider only through a Crucible capability.
using System;
using System.Collections.Generic;
using ExtraPotions.Meadworks.Contracts;
using ExtraPotions.Meadworks.Crucible;
using UnityEngine;
using CruciblePlugin = ExtraPotions.Meadworks.Crucible.Plugin;

namespace ExtraPotions.Kindle.Runtime.Salvage
{
    /// <summary>
    /// Salvage of inscribed gear through <c>meadworks.crafting.salvage_rune</c> v1 (an item-affix product provides it:
    /// Item1 the rune prefab a salvaged item may give back or null, Item2 its chance 0-1; both take the native item as an
    /// opaque object). While a provider is present, item data in the EliteCrafting format (keys starting with
    /// <see cref="HandledPrefix"/>, the format the provider keeps) never counts for Skip Items With Mod Data, and the
    /// rune joins the returns with its chance. The chance is rolled once, when the stack is salvaged. Without a provider
    /// nothing changes.
    /// </summary>
    internal static class SalvageRuneLink
    {
        /// <summary>Crucible.Contracts' id for Inscribe's SocketsV1.SalvageRuneV1; Inscribe is not referenced, BCL delegates only.</summary>
        public const string Id = SalvageRuneContract.Id;

        /// <summary>The EliteCrafting item data key prefix (third-party format, also the Mod Data Prefixes default).</summary>
        public const string HandledPrefix = "ecf_";

        static readonly CapabilityLink<Tuple<Func<object, string>, Func<object, float>>> link =
            new CapabilityLink<Tuple<Func<object, string>, Func<object, float>>>(Id, SalvageRuneContract.ContractVersion);

        static bool TryGet(out Tuple<Func<object, string>, Func<object, float>>? provider)
        {
            provider = null;
            return CruciblePlugin.IsReady && link.TryGet(CruciblePlugin.Services.Capabilities, out provider)
                && provider != null && provider.Item1 != null && provider.Item2 != null;
        }

        public static bool Present => TryGet(out _);

        /// <summary>An item data key the present provider handles.</summary>
        public static bool Handles(string key) => key.StartsWith(HandledPrefix, StringComparison.Ordinal) && Present;

        /// <summary>Adds the rune the item may give back, one per stack with its chance, to the returns.</summary>
        public static void AddRune(ItemDrop.ItemData item, List<SalvageReturn> returns)
        {
            if (item == null || ObjectDB.instance == null || !TryGet(out var provider))
                return;
            string? prefab;
            float chance;
            try { prefab = provider!.Item1(item); chance = provider.Item2(item); }
            catch (Exception) { return; }
            if (string.IsNullOrEmpty(prefab) || !(chance > 0f))
                return;
            GameObject rune = ObjectDB.instance.GetItemPrefab(prefab);
            ItemDrop? drop = rune != null ? rune.GetComponent<ItemDrop>() : null;
            if (drop != null)
                returns.Add(new SalvageReturn(drop, 1, Mathf.Min(chance, 1f)));
        }

        /// <summary>The returns as they come out: each chance rolled once, a won one kept as certain, a lost one left out.</summary>
        public static List<SalvageReturn> Roll(List<SalvageReturn> returns)
        {
            var rolled = new List<SalvageReturn>(returns.Count);
            foreach (SalvageReturn entry in returns)
            {
                if (entry.IsCertain)
                    rolled.Add(entry);
                else if (UnityEngine.Random.value < entry.Chance)
                    rolled.Add(new SalvageReturn(entry.Item, entry.Amount));
            }
            return rolled;
        }
    }
}
