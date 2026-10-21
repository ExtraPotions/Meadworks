// Portions Copyright (C) 2026 Gerald Glasgow. EliteCreaturesPack 0.7.2 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Changed by expDARE, 2026: Bestiary namespace, owned Crucible settings, rebuilt-only bundles and lifecycle adapters.
// SPDX-License-Identifier: GPL-3.0-or-later
using System.Collections.Generic;
using System.Linq;
using ExtraPotions.Bestiary.Catalog;
using ExtraPotions.Bestiary.Runtime.Native.Core;
using HarmonyLib;
using UnityEngine;

namespace ExtraPotions.Bestiary.Runtime.Native.Kraken
{
    /// <summary>
    /// The Kraken shield's recipe, in every ObjectDB the game builds or copies once the items exist: 10 fine wood, 5
    /// silver and a kraken beak, at the forge, at the station level and with the upgrade item the game's serpent scale
    /// shield needs (read from that shield's own recipe, so a game change carries over). Each upgrade takes 10 more fine
    /// wood and 3 more silver, never another beak. No forge found while Bestiary is active means the recipe is off and the
    /// log says so (without a station the game would let it be made by hand anywhere); while Bestiary is off the recipe is
    /// off quietly, and RefreshOwned fills it again when Bestiary turns on.
    /// </summary>
    public static class ShieldRecipe
    {
        private const string GameShield = "ShieldSerpentscale";
        private const string Forge = "forge";
        private const int ForgeLevel = 3;

        private static Recipe? recipe;

        [HarmonyPatch(typeof(ObjectDB), nameof(ObjectDB.Awake))]
        public static class Built
        {
            private static void Postfix(ObjectDB __instance) => SafeCall.Run("kraken shield recipe", () => Install(__instance));
        }

        [HarmonyPatch(typeof(ObjectDB), nameof(ObjectDB.CopyOtherDB))]
        public static class Copied
        {
            private static void Postfix(ObjectDB __instance) => SafeCall.Run("kraken shield recipe", () => Install(__instance));
        }

        public static void Install(ObjectDB? db)
        {
            // No ZNetScene: the main menu's database, where nothing is crafted and neither stations nor items exist yet.
            if (db == null || ZNetScene.instance == null || KrakenLoot.ShieldItem == null || KrakenLoot.BeakItem == null)
            {
                return;
            }
            recipe ??= Create(KrakenLoot.ShieldItem);
            Fill(db, recipe, KrakenLoot.BeakItem);
            if (!db.m_recipes.Contains(recipe))
            {
                db.m_recipes.Add(recipe);
                Adapters.NativeRuntime.Recipe(db, recipe);
            }
        }

        private static Recipe Create(GameObject shield)
        {
            Recipe made = ScriptableObject.CreateInstance<Recipe>();
            made.name = "Recipe_" + KrakenLoot.Shield;
            made.m_item = shield.GetComponent<ItemDrop>();
            made.m_amount = 1;
            return made;
        }

        /// <summary>
        /// Station and cost from this database, where the game's own items are. The beak is the prefab itself: this may
        /// run before the database lists the mod's items.
        /// </summary>
        private static void Fill(ObjectDB db, Recipe made, GameObject beak)
        {
            Recipe? serpent = db.m_recipes.Find(r => r != null && r.m_item != null && r.m_item.name == GameShield);
            made.m_craftingStation = serpent != null && serpent.m_craftingStation != null ? serpent.m_craftingStation : Station(Forge);
            made.m_minStationLevel = serpent != null ? serpent.m_minStationLevel : ForgeLevel;
            RecipeStationOutcome outcome = RecipeStationRule.Decide(Adapters.NativeConfiguration.Active, made.m_craftingStation != null);
            made.m_enabled = outcome == RecipeStationOutcome.Enabled;
            var cost = new List<Piece.Requirement>();
            Add(cost, db.GetItemPrefab("FineWood"), 10, 10);
            Add(cost, db.GetItemPrefab("Silver"), 5, 3);
            Add(cost, beak, 1, 0);
            if (serpent != null)
            {
                cost.AddRange(serpent.m_resources.Where(requirement => requirement.m_upgraderResource));
            }
            made.m_resources = cost.ToArray();
            if (outcome == RecipeStationOutcome.NoStation)
            {
                Log.Warn($"Kraken shield: no {Forge} found; it cannot be made.");
            }
        }

        private static void Add(List<Piece.Requirement> cost, GameObject? item, int amount, int perLevel)
        {
            ItemDrop? drop = item != null ? item.GetComponent<ItemDrop>() : null;
            if (drop != null)
            {
                cost.Add(new Piece.Requirement { m_resItem = drop, m_amount = amount, m_amountPerLevel = perLevel, m_recover = true });
            }
        }

        private static CraftingStation? Station(string name)
        {
            GameObject? prefab = ZNetScene.instance != null ? ZNetScene.instance.GetPrefab(name) : null;
            return prefab != null ? prefab.GetComponent<CraftingStation>() : null;
        }
    }
}
