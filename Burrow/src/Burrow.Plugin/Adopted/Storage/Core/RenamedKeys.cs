using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx.Configuration;

namespace Burrow.Core
{
    /// <summary>
    /// Carries the value of a renamed config key over from the player's cfg. BepInEx keeps every line of the file
    /// that no bound entry claims in the ConfigFile's orphaned entries; a module asks for the old line before it
    /// binds the new key, so a cfg written by an earlier version keeps its bindings. The old line is consumed, so
    /// the next save of the cfg drops it. A cfg that already has the new key is left alone.
    /// </summary>
    public static class RenamedKeys
    {
        /// <summary>The old entry's value, parsed as <typeparamref name="T"/>, or <paramref name="fallback"/> when
        /// the cfg has the new key, has no old line, or the old line does not parse.</summary>
        public static T Carry<T>(ConfigFile config, string section, string oldKey, string newKey, T fallback)
        {
            Dictionary<ConfigDefinition, string> orphans = Orphans(config);
            if (orphans == null || orphans.ContainsKey(new ConfigDefinition(section, newKey)))
                return fallback;
            ConfigDefinition old = new ConfigDefinition(section, oldKey);
            if (!orphans.TryGetValue(old, out string text))
                return fallback;
            try
            {
                T value = TomlTypeConverter.ConvertToValue<T>(text);
                orphans.Remove(old);
                Plugin.Log.LogInfo($"[{section}] {newKey} takes the value of the renamed key {oldKey}: {text}");
                return value;
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"[{section}] {oldKey} = {text} could not be carried over to {newKey}: {e.Message}");
                return fallback;
            }
        }

        /// <summary>BepInEx's unbound lines of the file, reached by reflection because the property is not public.</summary>
        private static Dictionary<ConfigDefinition, string> Orphans(ConfigFile config)
        {
            if (config == null)
                return null;
            PropertyInfo property = typeof(ConfigFile).GetProperty("OrphanedEntries",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            return property != null ? property.GetValue(config) as Dictionary<ConfigDefinition, string> : null;
        }
    }
}
