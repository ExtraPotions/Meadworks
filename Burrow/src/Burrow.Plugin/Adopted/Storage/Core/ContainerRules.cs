using System;
using System.Collections.Generic;

namespace Burrow.Core
{
    /// <summary>
    /// Which container prefabs the mod may use, by prefab name. Reach's YAML fills it from the <c>enabled</c>
    /// flags of its <c>containers:</c> map; every prefab not listed is enabled. Stow reads it too.
    /// </summary>
    public static class ContainerRules
    {
        private static Dictionary<string, bool> enabled = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

        /// <summary>Replaces the table. Null or empty enables every prefab.</summary>
        public static void SetEnabled(IReadOnlyDictionary<string, bool> byPrefab)
        {
            Dictionary<string, bool> table = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            if (byPrefab != null)
            {
                foreach (KeyValuePair<string, bool> entry in byPrefab)
                {
                    if (!string.IsNullOrWhiteSpace(entry.Key))
                        table[entry.Key.Trim()] = entry.Value;
                }
            }
            enabled = table;
        }

        public static bool IsEnabled(string prefabName)
        {
            return prefabName == null || !enabled.TryGetValue(prefabName, out bool value) || value;
        }
    }
}
