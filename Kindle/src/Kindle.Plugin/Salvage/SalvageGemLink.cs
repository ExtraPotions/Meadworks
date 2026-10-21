// Kindle - ExtraPotions Meadworks crafting conveniences.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: socketed gems come back from a salvage by the provider's chance (batch 3 gem recovery).
using System;
using System.Collections.Generic;
using System.Linq;
using ExtraPotions.Meadworks.Crucible;
using UnityEngine;
using CruciblePlugin = ExtraPotions.Meadworks.Crucible.Plugin;

namespace ExtraPotions.Kindle.Runtime.Salvage
{
    /// <summary>
    /// <c>meadworks.crafting.salvage_gems</c> v1 (an item-affix product such as Inscribe provides it): Item1 the prefab names
    /// of the gems set in the item's sockets, Item2 the chance 0-1 that each comes back (the host's Gem Recovery setting for
    /// the item's tier), Item3 the provider's localized message for gems that did not. Each gem joins the returns with that
    /// chance and is rolled once with the rune, when the stack is salvaged. Without a provider nothing changes: gems are lost.
    /// </summary>
    internal static class SalvageGemLink
    {
        public const string Id = "meadworks.crafting.salvage_gems";
        private static readonly System.Version Version = new System.Version(1, 0);

        static readonly CapabilityLink<Tuple<Func<object, string[]>, Func<object, float>, Func<int, string>>> link =
            new CapabilityLink<Tuple<Func<object, string[]>, Func<object, float>, Func<int, string>>>(Id, Version);

        static bool TryGet(out Tuple<Func<object, string[]>, Func<object, float>, Func<int, string>>? provider)
        {
            provider = null;
            return CruciblePlugin.IsReady && link.TryGet(CruciblePlugin.Services.Capabilities, out provider)
                && provider != null && provider.Item1 != null && provider.Item2 != null && provider.Item3 != null;
        }

        /// <summary>Adds each socketed gem, one apiece with its chance, to the returns.</summary>
        public static void AddGems(ItemDrop.ItemData item, List<SalvageReturn> returns)
        {
            if (item == null || ObjectDB.instance == null || !TryGet(out var provider))
                return;
            string[] gems;
            float chance;
            try { gems = provider!.Item1(item) ?? Array.Empty<string>(); chance = provider.Item2(item); }
            catch (Exception) { return; }
            if (!(chance > 0f))
                return;
            foreach (string prefab in gems)
            {
                GameObject gem = string.IsNullOrEmpty(prefab) ? null! : ObjectDB.instance.GetItemPrefab(prefab);
                ItemDrop? drop = gem != null ? gem.GetComponent<ItemDrop>() : null;
                if (drop != null)
                    returns.Add(new SalvageReturn(drop, 1, Mathf.Min(chance, 1f)));
            }
        }

        /// <summary>How many gems the item held that did not come back in <paramref name="rolled"/>.</summary>
        public static int Lost(ItemDrop.ItemData item, List<SalvageReturn> rolled)
        {
            if (item == null || !TryGet(out var provider))
                return 0;
            string[] gems;
            try { gems = provider!.Item1(item) ?? Array.Empty<string>(); }
            catch (Exception) { return 0; }
            int kept = rolled.Count(r => r.Item != null && gems.Contains(r.Item.name));
            return Math.Max(0, gems.Length - kept);
        }

        /// <summary>The provider's message for gems that did not come back, in the center of the screen.</summary>
        public static void ReportLost(int lost)
        {
            if (lost <= 0 || !TryGet(out var provider))
                return;
            try { Messages.Center(provider!.Item3(lost)); }
            catch (Exception) { }
        }
    }
}
