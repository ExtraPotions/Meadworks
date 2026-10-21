// Portions Copyright (C) 2026 Gerald Glasgow. EliteCreaturesPack 0.7.2 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Changed by expDARE, 2026: Bestiary namespace, owned Crucible settings, rebuilt-only bundles and lifecycle adapters.
// SPDX-License-Identifier: GPL-3.0-or-later
using System.Collections.Generic;
using ExtraPotions.Bestiary.Runtime.Native.Core;
using HarmonyLib;
using UnityEngine;

namespace ExtraPotions.Bestiary.Runtime.Native.Arsenal
{
    /// <summary>
    /// The bone weapons' and bone arrows' recipes, in every ObjectDB the game builds or copies once the items exist: the
    /// weapons at a level 2 workbench, costing <see cref="ArsenalWeapon.Recipe"/> (item:amount:amount per upgrade; the
    /// spine by its prefab name, ECP_Spine), and 20 arrows for 8 bone fragments at a level 2 workbench. An item
    /// name the game does not know is logged and left out. Not made at all when no workbench is found: without a
    /// station the game would let them be made by hand anywhere.
    /// </summary>
    public static class ArsenalRecipes
    {
        private const string Workbench = "piece_workbench";
        private const int WeaponLevel = 2;        // as the bronze-age flint and bone kit
        private const int ArrowLevel = 2;        // as flint arrows; wood arrows need 1
        private const int ArrowsPerCraft = 20;   // as the game's wood and flint arrows
        private const string ArrowCost = "BoneFragments:8";

        private static readonly Dictionary<GameObject, Recipe> recipes = new Dictionary<GameObject, Recipe>();

        [HarmonyPatch(typeof(ObjectDB), nameof(ObjectDB.Awake))]
        public static class Built
        {
            private static void Postfix(ObjectDB __instance) => SafeCall.Run("bone weapon recipes", () => Install(__instance));
        }

        [HarmonyPatch(typeof(ObjectDB), nameof(ObjectDB.CopyOtherDB))]
        public static class Copied
        {
            private static void Postfix(ObjectDB __instance) => SafeCall.Run("bone weapon recipes", () => Install(__instance));
        }

        public static void Install(ObjectDB? db)
        {
            // No ZNetScene: the main menu's database, where nothing is crafted and neither stations nor items exist yet.
            if (db == null || ZNetScene.instance == null)
            {
                return;
            }
            foreach (var pair in ArsenalItems.Weapons)
            {
                Add(db, pair.Value, 1, WeaponLevel, pair.Key.Recipe);
            }
            if (ArsenalArrow.Item != null)
            {
                Add(db, ArsenalArrow.Item, ArrowsPerCraft, ArrowLevel, ArrowCost);
            }
        }

        private static void Add(ObjectDB db, GameObject item, int amount, int level, string cost)
        {
            if (!recipes.TryGetValue(item, out Recipe recipe))
            {
                recipe = ScriptableObject.CreateInstance<Recipe>();
                (recipe.name, recipe.m_item, recipe.m_amount) = ("Recipe_" + item.name, item.GetComponent<ItemDrop>(), amount);
                recipes[item] = recipe;
            }
            GameObject? bench = ZNetScene.instance != null ? ZNetScene.instance.GetPrefab(Workbench) : null;
            recipe.m_craftingStation = bench != null ? bench.GetComponent<CraftingStation>() : null;
            recipe.m_minStationLevel = level;
            recipe.m_resources = Cost(db, item.name, cost);
            recipe.m_enabled = Adapters.NativeConfiguration.Active && (recipe.m_craftingStation != null && recipe.m_resources.Length > 0);
            if (!db.m_recipes.Contains(recipe))
            {
                db.m_recipes.Add(recipe);
                Adapters.NativeRuntime.Recipe(db, recipe);
            }
        }

        /// <summary>"ECP_Spine:1, BoneFragments:10:5" into requirements; unknown items and bad entries are logged and skipped.</summary>
        private static Piece.Requirement[] Cost(ObjectDB db, string made, string text)
        {
            var cost = new List<Piece.Requirement>();
            foreach (string entry in text.Split(','))
            {
                string[] parts = entry.Trim().Split(':');
                ItemDrop? item = parts.Length >= 2 ? ArsenalItems.Find(db, parts[0].Trim()) : null;
                if (item == null || !int.TryParse(parts[1], out int amount))
                {
                    if (entry.Trim().Length > 0)
                    {
                        Log.Warn($"{made} recipe: \"{entry.Trim()}\" is not item:amount[:per upgrade] with an item the game knows; left out.");
                    }
                    continue;
                }
                int perLevel = parts.Length >= 3 && int.TryParse(parts[2], out int per) ? per : 0;
                cost.Add(new Piece.Requirement { m_resItem = item, m_amount = amount, m_amountPerLevel = perLevel, m_recover = true });
            }
            return cost.ToArray();
        }
    }
}
