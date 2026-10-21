// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
using System.Collections.Generic;

namespace ExtraPotions.Inscribe.Adopted.Items
{
    /// <summary>
    /// Enabled recipes by result prefab name, built lazily from <c>ObjectDB.instance</c> and rebuilt when the database
    /// instance or its recipe count changes (other mods add recipes late). Not a hot path: tier lookups are cached.
    /// </summary>
    internal static class RecipeIndex
    {
        private static readonly Dictionary<string, List<Recipe>> ByResult = new Dictionary<string, List<Recipe>>(System.StringComparer.Ordinal);
        private static ObjectDB? _db;
        private static int _count = -1;

        /// <summary>True when the index was rebuilt by this call (callers drop what they derived from the old one).</summary>
        public static bool Refresh()
        {
            ObjectDB? db = ObjectDB.instance;
            int count = db?.m_recipes?.Count ?? -1;
            if (ReferenceEquals(db, _db) && count == _count)
            {
                return false;
            }
            _db = db;
            _count = count;
            Rebuild(db);
            return true;
        }

        public static IReadOnlyList<Recipe> For(string prefabName)
        {
            return ByResult.TryGetValue(prefabName, out List<Recipe> list) ? list : (IReadOnlyList<Recipe>)System.Array.Empty<Recipe>();
        }

        public static bool HasRecipe(string prefabName) => ByResult.ContainsKey(prefabName);

        private static void Rebuild(ObjectDB? db)
        {
            ByResult.Clear();
            if (db?.m_recipes == null)
            {
                return;
            }
            foreach (Recipe recipe in db.m_recipes)
            {
                if (recipe == null || !recipe.m_enabled || recipe.m_item == null)
                {
                    continue;
                }
                string name = recipe.m_item.gameObject.name;
                if (!ByResult.TryGetValue(name, out List<Recipe> list))
                {
                    list = new List<Recipe>();
                    ByResult[name] = list;
                }
                list.Add(recipe);
            }
        }
    }
}
