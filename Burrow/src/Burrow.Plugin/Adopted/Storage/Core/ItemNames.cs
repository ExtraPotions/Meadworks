namespace Burrow.Core
{
    /// <summary>Prefab name, display name and stacking identity of an item.</summary>
    public static class ItemNames
    {
        /// <summary>The drop prefab's name; for items without one (never dropped) the shared name token.</summary>
        private static readonly System.Collections.Generic.Dictionary<UnityEngine.GameObject, string> prefabNames = new System.Collections.Generic.Dictionary<UnityEngine.GameObject, string>();

        public static string PrefabName(ItemDrop.ItemData item)
        {
            if (item == null)
                return "";
            if (item.m_dropPrefab != null)
            {
                // Read every frame per grid slot (favourite marks); the prefab's name never changes, so it is read once.
                if (!prefabNames.TryGetValue(item.m_dropPrefab, out string name))
                {
                    if (prefabNames.Count >= 4096)
                        prefabNames.Clear();
                    prefabNames[item.m_dropPrefab] = name = Utils.GetPrefabName(item.m_dropPrefab);
                }
                return name;
            }
            return item.m_shared != null ? item.m_shared.m_name : "";
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
