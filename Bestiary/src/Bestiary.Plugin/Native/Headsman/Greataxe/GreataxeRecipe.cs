// Portions Copyright (C) 2026 Gerald Glasgow. EliteCreaturesPack 0.7.2 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Changed by expDARE, 2026: Bestiary namespace, owned Crucible settings, rebuilt-only bundles and lifecycle adapters.
// SPDX-License-Identifier: GPL-3.0-or-later
using System.Collections.Generic;
using ExtraPotions.Bestiary.Runtime.Native.Arsenal;
using ExtraPotions.Bestiary.Runtime.Native.Core;
using HarmonyLib;
using UnityEngine;

namespace ExtraPotions.Bestiary.Runtime.Native.Headsman
{
    /// <summary>
    /// The Executioner's Greataxe's recipe, in every ObjectDB the game builds or copies once the item exists: at the
    /// workbench at the settings' level, from the Executioner's axehead, spines (the skeleton arsenal's ECP_Spine, found
    /// even before the database lists it; bone fragments in their place when the arsenal is not built) and bone
    /// fragments, as many as the settings say.
    /// Off while `Recipe` is off, or when no workbench is found. Rebuilt when the settings change.
    /// </summary>
    public static class GreataxeRecipe
    {
        private const string Workbench = "piece_workbench", Bones = "BoneFragments";

        private static Recipe? recipe;

        [HarmonyPatch(typeof(ObjectDB), nameof(ObjectDB.Awake))]
        public static class Built
        {
            private static void Postfix(ObjectDB __instance) => SafeCall.Run("greataxe recipe", () => Install(__instance));
        }

        [HarmonyPatch(typeof(ObjectDB), nameof(ObjectDB.CopyOtherDB))]
        public static class Copied
        {
            private static void Postfix(ObjectDB __instance) => SafeCall.Run("greataxe recipe", () => Install(__instance));
        }

        public static void Install(ObjectDB? db)
        {
            // No ZNetScene: the main menu's database, where nothing is crafted and neither stations nor items exist yet.
            if (db == null || ZNetScene.instance == null || GreataxeItems.Axe == null || GreataxeItems.Axehead == null)
            {
                return;
            }
            if (recipe == null)
            {
                recipe = ScriptableObject.CreateInstance<Recipe>();
                recipe.name = "Recipe_" + GreataxeItems.AxeName;
                recipe.m_item = GreataxeItems.Axe.GetComponent<ItemDrop>();
                recipe.m_amount = 1;
            }
            Fill(db, recipe);
            if (!db.m_recipes.Contains(recipe))
            {
                db.m_recipes.Add(recipe);
                Adapters.NativeRuntime.Recipe(db, recipe);
            }
        }

        private static void Fill(ObjectDB db, Recipe made)
        {
            GameObject? bench = ZNetScene.instance != null ? ZNetScene.instance.GetPrefab(Workbench) : null;
            made.m_craftingStation = bench != null ? bench.GetComponent<CraftingStation>() : null;
            made.m_minStationLevel = GreataxeSettings.StationLevel;
            made.m_resources = Cost(db);
            made.m_enabled = Adapters.NativeConfiguration.Active && (GreataxeSettings.On && made.m_craftingStation != null);
        }

        private static Piece.Requirement[] Cost(ObjectDB db)
        {
            var cost = new List<Piece.Requirement>();
            Add(cost, GreataxeItems.Axehead!.GetComponent<ItemDrop>(), 1);
            ItemDrop? bones = db.GetItemPrefab(Bones)?.GetComponent<ItemDrop>();
            ItemDrop? spine = ArsenalItems.Find(db, ArsenalItems.SpineName);
            Add(cost, spine ?? bones, GreataxeSettings.Spines);
            Add(cost, bones, GreataxeSettings.Bones);
            return cost.ToArray();
        }

        /// <summary>The item at `amount`, added to what the same item already costs.</summary>
        private static void Add(List<Piece.Requirement> cost, ItemDrop? item, int amount)
        {
            if (item == null || amount <= 0)
            {
                return;
            }
            Piece.Requirement? same = cost.Find(r => r.m_resItem == item);
            if (same != null)
            {
                same.m_amount += amount;
                return;
            }
            cost.Add(new Piece.Requirement { m_resItem = item, m_amount = amount, m_amountPerLevel = 0, m_recover = true });
        }
    }
}
