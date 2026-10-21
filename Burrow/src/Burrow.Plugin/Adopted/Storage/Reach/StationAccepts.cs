using System;
using Burrow.Core;
using UnityEngine;

namespace Burrow.Reach
{
    /// <summary>
    /// What each station takes from storage: the station's own allow checks (so modded conversions work),
    /// narrowed by the station prefab's allow / deny lists of the <c>stations:</c> map.
    /// </summary>
    internal static class StationAccepts
    {
        public static Func<ItemDrop.ItemData, bool> SmelterOre(Smelter smelter)
        {
            return Filtered(smelter, item => item != null && smelter.IsItemAllowed(ItemNames.PrefabName(item)));
        }

        public static Func<ItemDrop.ItemData, bool> CookingFood(CookingStation station)
        {
            return Filtered(station, item => item != null && station.IsItemAllowed(ItemNames.PrefabName(item)));
        }

        public static Func<ItemDrop.ItemData, bool> FermenterBase(Fermenter fermenter)
        {
            return Filtered(fermenter, item => item != null && fermenter.IsItemAllowed(ItemNames.PrefabName(item).GetStableHashCode()));
        }

        /// <summary>The fuel of a smelter, cooking station or fireplace: the same shared name.</summary>
        public static Func<ItemDrop.ItemData, bool> Fuel(Component station, ItemDrop fuel)
        {
            string name = fuel != null ? fuel.m_itemData.m_shared.m_name : null;
            return Filtered(station, item => name != null && item != null && item.m_shared.m_name == name);
        }

        private static Func<ItemDrop.ItemData, bool> Filtered(Component station, Func<ItemDrop.ItemData, bool> accepts)
        {
            StationRule rule = ReachRules.StationRuleFor(station);
            return item => accepts(item) && rule.Accepts(item);
        }
    }
}
