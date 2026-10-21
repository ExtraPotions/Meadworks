using System.Collections.Generic;

namespace ExtraPotions.Mantle.Embedded.YamlConfig;

/// <summary>
/// The wire shape of a set's files in its Charter article: one list alternating full path and content.
/// </summary>
internal static class YamlFileList
{
	public static List<string> Flatten(IReadOnlyDictionary<string, string> files)
	{
		List<string> list = new(files.Count * 2);
		foreach (KeyValuePair<string, string> file in files)
		{
			list.Add(file.Key);
			list.Add(file.Value);
		}
		return list;
	}

	public static Dictionary<string, string> Unflatten(List<string>? list)
	{
		Dictionary<string, string> files = new();
		for (int i = 0; list is not null && i + 1 < list.Count; i += 2)
		{
			files[list[i]] = list[i + 1];
		}
		return files;
	}
}
