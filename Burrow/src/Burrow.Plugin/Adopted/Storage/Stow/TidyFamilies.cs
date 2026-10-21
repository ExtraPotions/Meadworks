using System;
using System.Collections.Generic;
using UnityEngine;

namespace Burrow.Stow
{
    /// <summary>
    /// Material families Auto Tidy reads from the game itself, so a chest of bars is one theme from the first day: what
    /// every fuelled smelting station (the game's <c>Smelter</c> with a fuel item, such as the smelter and the blast
    /// furnace on coal) takes in is one family (ores and scrap), what it gives out another (bars), keyed by the fuel.
    /// A material crafted only from one family's members joins it (bronze from copper and tin, then bronze nails).
    /// Mods' stations and items count the same way. Built once per object database.
    /// </summary>
    internal static class TidyFamilies
    {
        private static Dictionary<string, string> families;
        private static ObjectDB builtFor;

        /// <summary>The family label of a prefab, or null when it has none.</summary>
        public static string Of(string prefab)
        {
            if (ObjectDB.instance == null || ZNetScene.instance == null)
                return null;
            if (families == null || !ReferenceEquals(builtFor, ObjectDB.instance))
                Build();
            return families.TryGetValue(prefab, out string family) ? family : null;
        }

        private static void Build()
        {
            families = new Dictionary<string, string>(StringComparer.Ordinal);
            builtFor = ObjectDB.instance;
            foreach (GameObject prefab in ZNetScene.instance.m_prefabs)
            {
                Smelter smelter = prefab != null ? prefab.GetComponent<Smelter>() : null;
                if (smelter != null && smelter.m_fuelItem != null)
                    AddStation(smelter);
            }
            JoinCrafted();
            JoinCrafted();
        }

        private static void AddStation(Smelter smelter)
        {
            string fuel = Utils.GetPrefabName(smelter.m_fuelItem.gameObject);
            foreach (Smelter.ItemConversion conversion in smelter.m_conversion)
            {
                if (conversion == null || conversion.m_from == null || conversion.m_to == null)
                    continue;
                Add(conversion.m_from, "smelts on " + fuel);
                Add(conversion.m_to, "smelted on " + fuel);
            }
        }

        private static void Add(ItemDrop item, string family)
        {
            string name = Utils.GetPrefabName(item.gameObject);
            if (!families.ContainsKey(name))
                families[name] = family;
        }

        /// <summary>A material whose recipe takes only members of one family joins that family.</summary>
        private static void JoinCrafted()
        {
            foreach (Recipe recipe in ObjectDB.instance.m_recipes)
            {
                ItemDrop item = recipe != null && recipe.m_enabled ? recipe.m_item : null;
                if (item == null || item.m_itemData.m_shared.m_itemType != ItemDrop.ItemData.ItemType.Material)
                    continue;
                string name = Utils.GetPrefabName(item.gameObject);
                string family = families.ContainsKey(name) ? null : SharedFamily(recipe.m_resources);
                if (family != null)
                    families[name] = family;
            }
        }

        private static string SharedFamily(Piece.Requirement[] resources)
        {
            string shared = null;
            foreach (Piece.Requirement resource in resources ?? new Piece.Requirement[0])
            {
                if (resource == null || resource.m_resItem == null)
                    continue;
                if (!families.TryGetValue(Utils.GetPrefabName(resource.m_resItem.gameObject), out string family))
                    return null;
                if (shared != null && shared != family)
                    return null;
                shared = family;
            }
            return shared;
        }
    }
}
