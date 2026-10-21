using System.Collections.Generic;
using UnityEngine;

namespace Mantle.Core
{
    /// <summary>Prefab name, display name and stacking identity of an item.</summary>
    public static class ItemNames
    {
        /// <summary>
        /// Prefab names read once per drop prefab: reading a GameObject's name makes a new string every time, and the
        /// player's frame asks for every carried item's (the key ring's news, worn packs and tackleboxes).
        /// </summary>
        private static readonly Dictionary<GameObject, string> prefabNames = new Dictionary<GameObject, string>();

        /// <summary>The drop prefab's name; for items without one (never dropped) the shared name token.</summary>
        public static string PrefabName(ItemDrop.ItemData item)
        {
            if (item == null)
                return "";
            GameObject prefab = item.m_dropPrefab;
            if (prefab == null)
                return item.m_shared != null ? item.m_shared.m_name : "";
            if (!prefabNames.TryGetValue(prefab, out string name))
            {
                name = Utils.GetPrefabName(prefab);
                prefabNames[prefab] = name;
            }
            return name;
        }

        /// <summary>The localized shared name.</summary>
        public static string DisplayName(ItemDrop.ItemData item)
        {
            return item != null && item.m_shared != null ? Language.Localize(item.m_shared.m_name) : "";
        }

        /// <summary>Same shared name: the identity the game uses when stacking.</summary>
        public static bool SameItem(ItemDrop.ItemData a, ItemDrop.ItemData b)
        {
            return a != null && b != null && a.m_shared != null && b.m_shared != null && a.m_shared.m_name == b.m_shared.m_name;
        }
    }
}
