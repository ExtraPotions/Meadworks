using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx.Configuration;

namespace ExtraPotions.Mantle.Embedded.ConfigReload;

/// <summary>
/// Compares the text of a .cfg file with the values a ConfigFile holds. BepInEx writes <c>[Section]</c> headers,
/// <c>#</c> comments and <c>Key = Value</c> lines, and reads them back the same way (trimmed, split at the first
/// <c>=</c>); a value it read but nobody bound sits in its OrphanedEntries. When every line of the file carries
/// the loaded text of its entry (or orphan) and no bound entry is missing, a reload would change nothing.
/// </summary>
internal static class LoadedValues
{
	private static readonly PropertyInfo? orphans = typeof(ConfigFile).GetProperty("OrphanedEntries", BindingFlags.NonPublic | BindingFlags.Instance);

	/// <summary>True when the file text holds exactly the values the ConfigFile has loaded.</summary>
	public static bool Matches(ConfigFile config, string content) => Matches(config, content, null);

	/// <summary>
	/// As <see cref="Matches(ConfigFile, string)"/>, leaving out the entries <paramref name="held"/> names: values the server
	/// holds for this player. They sit in memory without being saved, so the file text is stale for them by design and says
	/// nothing about an edit.
	/// </summary>
	public static bool Matches(ConfigFile config, string content, Func<ConfigDefinition, bool>? held)
	{
		Dictionary<ConfigDefinition, string> file = Parse(content);
		Dictionary<ConfigDefinition, string>? loaded = Loaded(config);
		if (loaded is null)
		{
			return false;
		}
		if (held is not null)
		{
			foreach (ConfigDefinition definition in new List<ConfigDefinition>(loaded.Keys))
			{
				if (held(definition))
				{
					loaded.Remove(definition);
					file.Remove(definition);
				}
			}
		}
		if (file.Count != loaded.Count)
		{
			return false;
		}
		foreach (KeyValuePair<ConfigDefinition, string> line in file)
		{
			if (!loaded.TryGetValue(line.Key, out string? value) || !string.Equals(value, line.Value, StringComparison.Ordinal))
			{
				return false;
			}
		}
		return true;
	}

	/// <summary>Every bound entry's serialized text plus the orphans; null when the orphans cannot be read.</summary>
	private static Dictionary<ConfigDefinition, string>? Loaded(ConfigFile config)
	{
		if (orphans?.GetValue(config) is not Dictionary<ConfigDefinition, string> homeless)
		{
			return null;
		}
		Dictionary<ConfigDefinition, string> result = new(homeless);
		foreach (KeyValuePair<ConfigDefinition, ConfigEntryBase> entry in config)
		{
			result[entry.Key] = entry.Value.GetSerializedValue();
		}
		return result;
	}

	private static Dictionary<ConfigDefinition, string> Parse(string content)
	{
		Dictionary<ConfigDefinition, string> result = new();
		string section = "";
		foreach (string rawLine in content.Split('\n'))
		{
			string line = rawLine.Trim();
			if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal))
			{
				continue;
			}
			if (line.StartsWith("[", StringComparison.Ordinal) && line.EndsWith("]", StringComparison.Ordinal))
			{
				section = line.Substring(1, line.Length - 2);
				continue;
			}
			int split = line.IndexOf('=');
			if (split > 0)
			{
				result[new ConfigDefinition(section, line.Substring(0, split).Trim())] = line.Substring(split + 1).Trim();
			}
		}
		return result;
	}
}
