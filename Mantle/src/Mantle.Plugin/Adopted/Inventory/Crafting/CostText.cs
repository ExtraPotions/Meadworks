using System.Collections.Generic;
using Mantle.Backpacks;
using Mantle.Tackle;
using UnityEngine;

namespace Mantle.Crafting
{
    /// <summary>
    /// A crafted item's cost text ("Mantle_DeerhideSatchel:1, TrollHide:20, Bronze:2") as the game's requirements. An
    /// item of Mantle's own (a backpack or a tacklebox) is taken from its catalog, so a recipe that takes the one before
    /// it holds even before ObjectDB has them; anything else comes from ObjectDB. A pair that is not prefab:amount of a
    /// known item is logged and skipped.
    /// </summary>
    public static class CostText
    {
        /// <summary>False while the database's item registry is still empty, when every name would look unknown.</summary>
        public static bool Ready(ObjectDB db) => db != null && db.GetItemPrefab("Wood") != null;

        public static Piece.Requirement[] Parse(ObjectDB db, string text, string owner)
        {
            List<Piece.Requirement> cost = new List<Piece.Requirement>();
            foreach (string pair in (text ?? "").Split(','))
            {
                if (pair.Trim().Length == 0)
                    continue;
                string[] parts = pair.Split(':');
                ItemDrop item = parts.Length == 2 ? Item(db, parts[0].Trim()) : null;
                if (item == null || !int.TryParse(parts[1].Trim(), out int amount) || amount <= 0)
                {
                    Plugin.Log.LogWarning($"{owner} cost: \"{pair.Trim()}\" is not prefab:amount of a known item; skipped");
                    continue;
                }
                cost.Add(new Piece.Requirement { m_resItem = item, m_amount = amount, m_amountPerLevel = 0, m_recover = true });
            }
            return cost.ToArray();
        }

        private static ItemDrop Item(ObjectDB db, string name)
        {
            CraftedKind own = (CraftedKind)BackpackCatalog.ById(name) ?? TackleboxCatalog.ById(name);
            GameObject prefab = own != null ? own.Item : db.GetItemPrefab(name);
            return prefab != null ? prefab.GetComponent<ItemDrop>() : null;
        }
    }
}
