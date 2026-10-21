// Portions Copyright (C) 2026 Gerald Glasgow. EliteCreaturesPack 0.7.2 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Changed by expDARE, 2026: Bestiary namespace, owned Crucible settings, rebuilt-only bundles and lifecycle adapters.
// SPDX-License-Identifier: GPL-3.0-or-later
using ExtraPotions.Bestiary.Runtime.Adopted.BundlePrefabs;
using ExtraPotions.Bestiary.Runtime.Native.Core;
using HarmonyLib;
using UnityEngine;

namespace ExtraPotions.Bestiary.Runtime.Native.Kraken
{
    /// <summary>
    /// What a kraken leaves of its own: its beak, the meat of its arms (raw, and cooked: <see cref="KrakenMeat"/>) and the
    /// shield a smith makes from the beak (<see cref="KrakenShield"/>). Four item prefabs, each a copy of the game's
    /// nearest item wearing a model and icon from the embedded bundle <c>ecp_kraken_loot</c> (AssetWorkshop assets
    /// <c>ecp_kraken_beak</c>, <c>ecp_kraken_beak_shield</c>, <c>ecp_kraken_meat</c>, <c>ecp_kraken_meat_cooked</c>;
    /// <see cref="LootItem"/>). Built once, before the kraken (whose drops name them), then registered with ZNetScene and
    /// ObjectDB on every wake, the same on the server and every client, with the cooking and the shield's recipe.
    /// </summary>
    public static class KrakenLoot
    {
        public const string Bundle = "ecp_kraken_loot";
        public const string Beak = "ECP_KrakenBeak";
        public const string Meat = "ECP_KrakenMeat";
        public const string MeatCooked = "ECP_KrakenMeatCooked";
        public const string Shield = "ECP_ShieldKraken";

        private const string BeakAsset = "ecp_kraken_beak";
        private const string GameBeak = "SerpentScale";

        /// <summary>The dropped beak is drawn this much larger than the one set in the shield, so it stands out on a deck.</summary>
        private const float BeakDropScale = 1.3f;

        public static GameObject? BeakItem { get; private set; }
        public static GameObject? MeatItem { get; private set; }
        public static GameObject? CookedItem { get; private set; }
        public static GameObject? ShieldItem { get; private set; }

        /// <summary>Builds the four items (once) and registers them in this scene; throws, registering nothing, if one cannot be built.</summary>
        public static void Build(ZNetScene scene, Harmony harmony)
        {
            if (ShieldItem == null)
            {
                Create(scene);
            }
            foreach (GameObject item in new[] { BeakItem!, MeatItem!, CookedItem!, ShieldItem! })
            {
                NetPrefabs.Register(scene, item);
                ItemPrefabs.Register(harmony, item);
            }
            KrakenMeat.Cook(scene, MeatItem!, CookedItem!);
            ShieldRecipe.Install(ObjectDB.instance);
            Log.Info($"Kraken loot ready: {Beak}, {Meat}, {MeatCooked}, {Shield}.");
        }

        /// <summary>All four or none: the properties are set only once every item is built.</summary>
        private static void Create(ZNetScene scene)
        {
            AssetBundle bundle = EmbeddedBundle.Load(typeof(KrakenLoot).Assembly, Bundle);
            GameObject beak = BuildBeak(scene, bundle);
            GameObject meat = KrakenMeat.Raw(scene, bundle);
            GameObject cooked = KrakenMeat.Cooked(scene, bundle);
            GameObject shield = KrakenShield.Build(scene, bundle);
            (BeakItem, MeatItem, CookedItem, ShieldItem) = (beak, meat, cooked, shield);
        }

        /// <summary>A crafting material, like the serpent's scales it is copied from; one drops from each kraken.</summary>
        private static GameObject BuildBeak(ZNetScene scene, AssetBundle bundle)
        {
            GameObject beak = LootItem.Build(scene, bundle, GameBeak, Beak, BeakAsset, "ecp_krakenbeak", BeakDropScale, 0.35f);
            ItemDrop.ItemData.SharedData shared = beak.GetComponent<ItemDrop>().m_itemData.m_shared;
            shared.m_itemType = ItemDrop.ItemData.ItemType.Material;
            shared.m_maxStackSize = 10;
            shared.m_weight = 3f;
            shared.m_teleportable = true;
            return beak;
        }
    }
}
