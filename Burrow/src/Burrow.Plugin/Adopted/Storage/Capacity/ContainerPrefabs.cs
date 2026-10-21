using System;
using System.Collections.Generic;
using UnityEngine;

namespace Burrow.Capacity
{
    /// <summary>
    /// The container prefabs of the scene: every ZNetScene prefab with a Container component on it or on a child
    /// (ships and carts keep their storage on a child), by prefab name.
    /// </summary>
    public static class ContainerPrefabs
    {
        public static Dictionary<string, Container> All()
        {
            Dictionary<string, Container> prefabs = new Dictionary<string, Container>(StringComparer.OrdinalIgnoreCase);
            ZNetScene scene = ZNetScene.instance;
            if (scene == null || scene.m_prefabs == null)
                return prefabs;
            foreach (GameObject prefab in scene.m_prefabs)
            {
                Container container = prefab != null ? prefab.GetComponentInChildren<Container>(true) : null;
                if (container != null && !prefabs.ContainsKey(prefab.name))
                    prefabs[prefab.name] = container;
            }
            return prefabs;
        }
    }
}
