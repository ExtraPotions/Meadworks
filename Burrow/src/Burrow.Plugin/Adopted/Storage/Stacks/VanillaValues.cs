using System;
using System.Collections.Generic;

namespace Burrow.Stacks
{
    /// <summary>
    /// The stack and weight every item prefab had before Burrow touched it, remembered the first time the prefab
    /// is seen. Re-applying is idempotent because every apply starts from these; a multiplier set back to 1 and an
    /// emptied YAML restore them exactly.
    /// </summary>
    public static class VanillaValues
    {
        private static readonly Dictionary<string, ItemValue> byPrefab = new Dictionary<string, ItemValue>(StringComparer.OrdinalIgnoreCase);

        public static int Count => byPrefab.Count;

        /// <summary>Remembers the current values as vanilla when the prefab is new, and returns the vanilla values.</summary>
        public static ItemValue Remember(string prefabName, ItemDrop.ItemData.SharedData shared)
        {
            if (!byPrefab.TryGetValue(prefabName, out ItemValue value))
            {
                value = new ItemValue(shared.m_maxStackSize, shared.m_weight);
                byPrefab[prefabName] = value;
            }
            return value;
        }

        public static bool TryGet(string prefabName, out ItemValue value) => byPrefab.TryGetValue(prefabName, out value);
    }
}
