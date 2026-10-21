// Inscribe - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: gem recovery (batch 3): a replaced or salvaged gem comes back by the host's chance for the item's tier.
using System;
using System.Linq;
using ExtraPotions.Inscribe.Adopted.Affixes;
using ExtraPotions.Inscribe.Adopted.Rules;
using ExtraPotions.Inscribe.Adopted.Sockets;
using ExtraPotions.Inscribe.Config;
using ExtraPotions.Inscribe.Interop;
using GemRecovery = ExtraPotions.Inscribe.Rarity.GemRecovery;
using ExtraPotions.Meadworks.Crucible;
using UnityEngine;

namespace ExtraPotions.Inscribe.Runtime
{
    /// <summary>
    /// The five <c>2 - Runes / Gem Recovery</c> settings (host, synced) applied where the gem is replaced or salvaged:
    /// the rune's owning client, as every Inscribe rune roll, or the salvaging product (<see cref="SalvageGemsV1"/>).
    /// </summary>
    internal static class GemRecoveryRuntime
    {
        public static float Percent(string? rarityId)
        {
            InscribeConfig? settings = Plugin.Settings;
            float[] percents = settings == null ? GemRecovery.Defaults
                : InscribeSettings.Runes.GemRecovery.Select(s => settings.Get(s)).ToArray();
            return GemRecovery.PercentFor(rarityId, percents);
        }

        /// <summary>"The old gem comes back N% of the time." for the replace dialog and the Rune Table.</summary>
        public static string ChanceLine(string? rarityId) =>
            InscribeText.Format("gem_replace_chance", Adopted.Core.Numbers.Format(Percent(rarityId)));

        /// <summary>Puts a returned gem into the player's inventory, or at their feet when it is full.</summary>
        public static void Give(Player player, string gemId)
        {
            StoneDef? stone = ActiveRules.Current.Economy.Stone(gemId);
            ItemDrop.ItemData? item = stone != null ? Adopted.Loot.LootSpawner.StoneItem(stone, false) : null;
            if (player == null || item == null)
            {
                Adopted.Core.Log.Warn($"gem recovery: no item for '{gemId}'; not returned");
                return;
            }
            if (!player.GetInventory().AddItem(item))
                ItemDrop.DropItem(item, 1, player.transform.position + player.transform.forward + Vector3.up, Quaternion.identity);
        }

        /// <summary><see cref="SalvageGemsV1"/>: the gems a salvage may return, their chance, and the lost-gems message.</summary>
        public static void Publish(ProductRegistration product) =>
            product.AddCapability(SalvageGemsV1.Capability, SalvageGemsV1.Version,
                Tuple.Create<Func<object, string[]>, Func<object, float>, Func<int, string>>(Gems, Chance, lost => InscribeText.Format("gem_salvage_lost", lost)));

        private static string[] Gems(object value)
        {
            try
            {
                if (!SocketSwitch.On || !(value is ItemDrop.ItemData item)) return Array.Empty<string>();
                EconomyRules economy = ActiveRules.Current.Economy;
                return ItemState.Read(item).Gems.Select(g => economy.Stone(g.GemId)?.Prefab).Where(p => !string.IsNullOrEmpty(p)).Select(p => p!).ToArray();
            }
            catch (Exception) { return Array.Empty<string>(); }
        }

        private static float Chance(object value)
        {
            try { return value is ItemDrop.ItemData item ? Percent(ItemState.Read(item).RarityId) / 100f : 0f; }
            catch (Exception) { return 0f; }
        }
    }
}
