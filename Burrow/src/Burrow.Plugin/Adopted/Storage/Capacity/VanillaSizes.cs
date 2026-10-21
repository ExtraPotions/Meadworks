using System;
using System.Collections.Generic;

namespace Burrow.Capacity
{
    /// <summary>The size every container prefab had before Burrow touched it, remembered the first time it is seen.</summary>
    public static class VanillaSizes
    {
        private static readonly Dictionary<string, ContainerSize> byPrefab = new Dictionary<string, ContainerSize>(StringComparer.OrdinalIgnoreCase);

        /// <summary>Remembers the prefab component's current size as vanilla when the prefab is new, and returns the vanilla size.</summary>
        public static ContainerSize Remember(string prefabName, Container prefab)
        {
            if (!byPrefab.TryGetValue(prefabName, out ContainerSize size))
            {
                size = new ContainerSize(prefab.m_width, prefab.m_height);
                byPrefab[prefabName] = size;
            }
            return size;
        }

        public static bool TryGet(string prefabName, out ContainerSize size) => byPrefab.TryGetValue(prefabName, out size);
    }
}
