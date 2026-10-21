// Portions Copyright (C) 2026 Gerald Glasgow. EliteCreaturesPack 0.7.2 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Changed by expDARE, 2026: Bestiary namespace, owned Crucible settings, rebuilt-only bundles and lifecycle adapters.
// SPDX-License-Identifier: GPL-3.0-or-later
using UnityEngine;

namespace ExtraPotions.Bestiary.Runtime.Native.Kraken
{
    /// <summary>
    /// The kraken's meat. Raw, a crafting material like the serpent's (it cannot be eaten raw). Cooked, a food better
    /// than cooked serpent meat on every count: 80 health, 28 stamina, 4 healing a tick for 30 minutes (serpent: 70, 23,
    /// 3, 25 minutes). Raw meat cooks on every cooking station that cooks serpent meat (the game's iron cooking station),
    /// in the same time. Both are copies of the game's serpent meat items, so the cooked one steams like theirs. Both
    /// weigh <see cref="Weight"/>, raw or cooked, set here rather than taken from the serpent's.
    /// </summary>
    public static class KrakenMeat
    {
        private const string GameRaw = "SerpentMeat";
        private const string GameCooked = "SerpentMeatCooked";
        private const float Weight = 10f;

        /// <summary>The workshop cut is 19 cm long; drawn at this scale it reads as a kraken's arm on a deck and on a spit.</summary>
        private const float Scale = 2.5f;

        public static GameObject Raw(ZNetScene scene, AssetBundle bundle)
        {
            GameObject raw = LootItem.Build(scene, bundle, GameRaw, KrakenLoot.Meat, "ecp_kraken_meat", "ecp_krakenmeat", Scale, 0.45f);
            raw.GetComponent<ItemDrop>().m_itemData.m_shared.m_weight = Weight;
            return raw;
        }

        public static GameObject Cooked(ZNetScene scene, AssetBundle bundle)
        {
            GameObject cooked = LootItem.Build(scene, bundle, GameCooked, KrakenLoot.MeatCooked, "ecp_kraken_meat_cooked",
                "ecp_krakenmeatcooked", Scale, 0.3f);
            ItemDrop.ItemData.SharedData shared = cooked.GetComponent<ItemDrop>().m_itemData.m_shared;
            shared.m_weight = Weight;
            shared.m_food = 80f;
            shared.m_foodStamina = 28f;
            shared.m_foodRegen = 4f;
            shared.m_foodBurnTime = 1800f;
            return cooked;
        }

        /// <summary>
        /// Kraken meat onto every cooking station prefab that cooks serpent meat, taking as long. The prefabs outlive a
        /// world, so a station that already has it is left alone.
        /// </summary>
        public static void Cook(ZNetScene scene, GameObject raw, GameObject cooked)
        {
            foreach (GameObject prefab in scene.m_prefabs)
            {
                CookingStation? station = prefab != null ? prefab.GetComponent<CookingStation>() : null;
                if (station == null || station.m_conversion.Exists(c => c.m_from != null && c.m_from.name == raw.name))
                {
                    continue;
                }
                CookingStation.ItemConversion? serpent = station.m_conversion.Find(c => c.m_from != null && c.m_from.name == GameRaw);
                if (serpent != null)
                {
                    var conversion = new CookingStation.ItemConversion
                    {
                        m_from = raw.GetComponent<ItemDrop>(), m_to = cooked.GetComponent<ItemDrop>(), m_cookTime = serpent.m_cookTime,
                    };
                    station.m_conversion.Add(conversion);
                    Adapters.NativeRuntime.Cooking(station, conversion);
                }
            }
        }
    }
}
