// Inscribe - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: the host's per-tier drop weight multipliers (batch 3), read live on the peer that rolls the loot.
using ExtraPotions.Inscribe.Config;
using ExtraPotions.Inscribe.Rarity;

namespace ExtraPotions.Inscribe.Runtime
{
    /// <summary>
    /// <c>4 - Drops / Uncommon, Rare, Epic Drop Weight</c> on top of the YAML rarity tables. All at 1 keeps the precomputed
    /// table; otherwise the gear rarity pick re-weights its few entries (Adopted/Loot/RarityFind). Any other rarity: 1.
    /// </summary>
    internal static class RarityTuning
    {
        public static bool Active =>
            Multiplier("magic") != 1f || Multiplier("rare") != 1f || Multiplier(RarityTiers.Epic) != 1f;

        public static float Multiplier(string rarityId)
        {
            InscribeConfig? settings = Plugin.Settings;
            if (settings == null) return 1f;
            switch (rarityId)
            {
                case "magic": return settings.Get(InscribeSettings.Drops.UncommonWeight);
                case "rare": return settings.Get(InscribeSettings.Drops.RareWeight);
                case RarityTiers.Epic: return settings.Get(InscribeSettings.Drops.EpicWeight);
                default: return 1f;
            }
        }
    }
}
