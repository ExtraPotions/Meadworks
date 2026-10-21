// Portions Copyright (C) 2026 Gerald Glasgow. EliteCreaturesPack 0.7.2 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Changed by expDARE, 2026: Bestiary namespace, owned Crucible settings, rebuilt-only bundles and lifecycle adapters.
// SPDX-License-Identifier: GPL-3.0-or-later
using System.Collections.Generic;
using ExtraPotions.Bestiary.Runtime.Native.Arsenal;
using ExtraPotions.Bestiary.Runtime.Native.Core;
using HarmonyLib;
using UnityEngine;

namespace ExtraPotions.Bestiary.Runtime.Native.Crossbow
{
    /// <summary>
    /// The Bone Crossbow's recipe and its Blunted Bone Bolts' (`Bolt Recipe`, `Bolts Per Craft` at a time), in every
    /// ObjectDB the game builds or copies once the items exist: at the workbench, at the settings' level, costing what
    /// the settings list (item:amount:amount per upgrade; the skeleton arsenal's spine by its prefab name, ECP_Spine). An
    /// item name the game does not know is logged and left out.
    /// Off (and not made at all) while `Craftable` is off, or when no workbench is found: without a station the game
    /// would let them be made by hand anywhere. Rebuilt when the settings change.
    /// </summary>
    public static class XbowRecipe
    {
        private const string Workbench = "piece_workbench";

        private static readonly Dictionary<GameObject, Recipe> recipes = new Dictionary<GameObject, Recipe>();

        [HarmonyPatch(typeof(ObjectDB), nameof(ObjectDB.Awake))]
        public static class Built
        {
            private static void Postfix(ObjectDB __instance) => SafeCall.Run("bone crossbow recipe", () => Install(__instance));
        }

        [HarmonyPatch(typeof(ObjectDB), nameof(ObjectDB.CopyOtherDB))]
        public static class Copied
        {
            private static void Postfix(ObjectDB __instance) => SafeCall.Run("bone crossbow recipe", () => Install(__instance));
        }

        /// <summary>The Bone Crossbow's recipe and its Blunted Bone Bolts', once each exists.</summary>
        public static void Install(ObjectDB? db)
        {
            // No ZNetScene: the main menu's database, where nothing is crafted and neither stations nor items exist yet.
            if (db == null || ZNetScene.instance == null)
            {
                return;
            }
            if (XbowItem.Prefab != null)
            {
                Put(db, XbowItem.Prefab, 1, XbowItemSettings.Recipe);
            }
            if (XbowBolts.Item != null)
            {
                Put(db, XbowBolts.Item, XbowItemSettings.BoltsPerCraft, XbowItemSettings.BoltRecipe);
            }
        }

        private static void Put(ObjectDB db, GameObject item, int amount, string cost)
        {
            if (!recipes.TryGetValue(item, out Recipe made))
            {
                made = recipes[item] = ScriptableObject.CreateInstance<Recipe>();
                made.name = "Recipe_" + item.name;
                made.m_item = item.GetComponent<ItemDrop>();
            }
            made.m_amount = amount;
            Fill(db, made, cost);
            if (!db.m_recipes.Contains(made))
            {
                db.m_recipes.Add(made);
                Adapters.NativeRuntime.Recipe(db, made);
            }
        }

        private static void Fill(ObjectDB db, Recipe made, string cost)
        {
            GameObject? bench = ZNetScene.instance != null ? ZNetScene.instance.GetPrefab(Workbench) : null;
            made.m_craftingStation = bench != null ? bench.GetComponent<CraftingStation>() : null;
            made.m_minStationLevel = XbowItemSettings.WorkbenchLevel;
            made.m_resources = Cost(db, cost);
            made.m_enabled = Adapters.NativeConfiguration.Active && (XbowItemSettings.Craftable && made.m_craftingStation != null && made.m_resources.Length > 0);
            if (XbowItemSettings.Craftable && made.m_craftingStation == null)
            {
                Log.Warn($"Bone crossbow: no {Workbench} found; {made.m_item.name} cannot be made.");
            }
        }

        /// <summary>"Wood:10:5, BoneFragments:12:6" into requirements; unknown items and bad entries are logged and skipped.</summary>
        private static Piece.Requirement[] Cost(ObjectDB db, string text)
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
                        Log.Warn($"Bone crossbow recipe: \"{entry.Trim()}\" is not item:amount[:per upgrade] with an item the game knows; left out.");
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
