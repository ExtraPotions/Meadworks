using System;
using System.Collections.Generic;
using HarmonyLib;
using Mantle.Accessories;
using Mantle.Backpacks;
using Mantle.Core;
using Mantle.Tackle;
using UnityEngine;

namespace Mantle.Crafting
{
    /// <summary>
    /// One recipe per crafted item (the backpacks and the tackleboxes), added to every ObjectDB the game builds or copies
    /// once the items exist, and brought up to date whenever an item file or a switch changes (a server's values arriving
    /// included): the station (found through the game's own recipes, so it is the same station object as theirs; an
    /// unknown name falls back to the item's default), its level, the cost (<see cref="CostText"/>; a cost with nothing
    /// usable falls back to the default) and whether it can be crafted at all.
    /// </summary>
    public static class CraftRecipes
    {
        private static readonly Dictionary<CraftedKind, Recipe> recipes = new Dictionary<CraftedKind, Recipe>();
        private static readonly List<Action> releases = new List<Action>();

        [HarmonyPatch(typeof(ObjectDB), nameof(ObjectDB.Awake))]
        public static class Built
        {
            [HarmonyPostfix]
            public static void Postfix(ObjectDB __instance) => Install(__instance);
        }

        [HarmonyPatch(typeof(ObjectDB), nameof(ObjectDB.CopyOtherDB))]
        public static class Copied
        {
            [HarmonyPostfix]
            public static void Postfix(ObjectDB __instance) => Install(__instance);
        }

        /// <summary>Every crafted kind: backpacks, tackleboxes, then the rings and belts.</summary>
        public static IEnumerable<CraftedKind> All
        {
            get
            {
                foreach (BackpackKind kind in BackpackCatalog.All)
                    yield return kind;
                foreach (TackleboxKind kind in TackleboxCatalog.All)
                    yield return kind;
                foreach (AccessoryKind kind in AccessoryKinds.All)
                    yield return kind;
            }
        }

        public static void Watch()
        {
            Subscribe(BackpackSettings.Enabled);
            Subscribe(TackleboxSettings.Enabled);
            Subscribe(InventorySettings.RingSlots);
            Subscribe(InventorySettings.BeltSlot);
            Subscribe(InventorySettings.Enabled);
            Subscribe(InventorySettings.BackpackSlot);
        }

        /// <summary>Takes the handlers off again (module shutdown), so a reload leaves none behind.</summary>
        public static void Unwatch()
        {
            foreach (Action release in releases)
                release();
            releases.Clear();
        }

        private static void Subscribe<T>(BepInEx.Configuration.ConfigEntry<T> entry)
        {
            EventHandler handler = (sender, args) => RefreshAll();
            entry.SettingChanged += handler;
            releases.Add(() => entry.SettingChanged -= handler);
        }

        public static void Install(ObjectDB db)
        {
            if (db == null)
                return;
            foreach (CraftedKind kind in All)
            {
                if (kind.Item == null)
                    continue;
                Recipe recipe = RecipeOf(kind);
                if (!db.m_recipes.Contains(recipe))
                    db.m_recipes.Add(recipe);
            }
            Refresh(db);
        }

        public static void RefreshAll() => Refresh(ObjectDB.instance);

        private static void Refresh(ObjectDB db)
        {
            // A database whose registry is not filled yet (a reload, the main menu) knows no item at all: keep the costs and refresh when Install runs.
            if (db == null || !CostText.Ready(db))
                return;
            foreach (KeyValuePair<CraftedKind, Recipe> pair in recipes)
                Refresh(db, pair.Key, pair.Value);
        }

        private static Recipe RecipeOf(CraftedKind kind)
        {
            if (recipes.TryGetValue(kind, out Recipe recipe))
                return recipe;
            recipe = ScriptableObject.CreateInstance<Recipe>();
            recipe.name = "Recipe_" + kind.Id;
            recipe.m_item = kind.Item.GetComponent<ItemDrop>();
            recipe.m_amount = 1;
            recipe.m_listSortWeight = 100 + recipes.Count;
            recipes[kind] = recipe;
            return recipe;
        }

        /// <summary>No station found means no recipe: without one the game would let it be made by hand anywhere.</summary>
        private static void Refresh(ObjectDB db, CraftedKind kind, Recipe recipe)
        {
            CraftingStation station = Station(db, kind.Recipe.Station) ?? Station(db, kind.DefaultRecipe.Station);
            recipe.m_enabled = kind.Craftable && station != null;
            recipe.m_craftingStation = station;
            recipe.m_minStationLevel = kind.Recipe.Level;
            Piece.Requirement[] cost = CostText.Parse(db, kind.Recipe.Cost, kind.Name);
            recipe.m_resources = cost.Length > 0 ? cost : CostText.Parse(db, kind.DefaultRecipe.Cost, kind.Name);
            if (kind is BackpackKind)
                BackpackLevels.PriceUpgrades(recipe.m_resources);
            if (station == null)
                Plugin.Log.LogWarning($"{kind.Name}: no crafting station named \"{kind.Recipe.Station}\"; it cannot be crafted");
        }

        private static CraftingStation Station(ObjectDB db, string name)
        {
            foreach (Recipe other in db.m_recipes)
            {
                if (other != null && other.m_craftingStation != null && other.m_craftingStation.name == name)
                    return other.m_craftingStation;
            }
            GameObject prefab = ZNetScene.instance != null ? ZNetScene.instance.GetPrefab(name) : null;
            return prefab != null ? prefab.GetComponent<CraftingStation>() : null;
        }
    }
}
