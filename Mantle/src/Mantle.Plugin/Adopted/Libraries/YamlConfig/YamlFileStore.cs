using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using BepInEx.Logging;

namespace ExtraPotions.Mantle.Embedded.YamlConfig;

/// <summary>
/// The disk side of a <see cref="YamlFileHub"/>: finds the files of a set in the search folders, reads them,
/// writes the default content when nothing exists and writes edited files back atomically.
/// </summary>
internal sealed class YamlFileStore
{
	private readonly string modName;
	private readonly ManualLogSource log;
	private readonly List<string> searchFolders;

	public YamlFileStore(string modName, ManualLogSource log, IEnumerable<string> searchFolders)
	{
		this.modName = modName;
		this.log = log;
		this.searchFolders = searchFolders.ToList();
	}

	/// <summary>Where files are looked for, in order; defaults are written to the first.</summary>
	public IReadOnlyList<string> SearchFolders => searchFolders;

	/// <summary>Full paths of the set's files in every search folder: the main file first, then by name.</summary>
	public List<string> Discover(YamlFileSet set)
	{
		Regex nameFilter = new("^" + Regex.Escape(set.FilePattern).Replace("\\*", ".*") + "$", RegexOptions.IgnoreCase);
		List<string> found = new();
		foreach (string folder in searchFolders.Where(Directory.Exists))
		{
			found.AddRange(Directory.GetFiles(folder, set.FilePattern).Where(p => nameFilter.IsMatch(Path.GetFileName(p))));
		}
		return found
			.OrderBy(p => string.Equals(Path.GetFileName(p), set.MainFileName, StringComparison.OrdinalIgnoreCase) ? 0 : 1)
			.ThenBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
			.ToList();
	}

	/// <summary>Reads the given files; a file that cannot be read is logged and left out.</summary>
	public Dictionary<string, string> Read(IEnumerable<string> paths)
	{
		Dictionary<string, string> files = new();
		foreach (string path in paths)
		{
			try
			{
				files[path] = File.ReadAllText(path);
			}
			catch (Exception e)
			{
				log.LogError($"{modName}: reading {path} failed: {e.Message}");
			}
		}
		return files;
	}

	/// <summary>Reads the set's files, writing the default content first when no file exists.</summary>
	public Dictionary<string, string> ReadOrCreate(YamlFileSet set)
	{
		Dictionary<string, string> files = Read(Discover(set));
		return files.Count > 0 ? files : WriteDefault(set);
	}

	/// <summary>Last write time of every file of the set, by full path.</summary>
	public Dictionary<string, DateTime> WriteTimes(YamlFileSet set)
	{
		Dictionary<string, DateTime> times = new(StringComparer.OrdinalIgnoreCase);
		foreach (string path in Discover(set))
		{
			times[path] = File.GetLastWriteTimeUtc(path);
		}
		return times;
	}

	/// <summary>Writes every file whose content differs from what is on disk, each through a temporary file.</summary>
	public void WriteAll(IReadOnlyDictionary<string, string> files)
	{
		foreach (KeyValuePair<string, string> file in files)
		{
			WriteAtomic(file.Key, file.Value);
		}
	}

	private Dictionary<string, string> WriteDefault(YamlFileSet set)
	{
		byte[]? content = set.DefaultContent?.Invoke();
		if (content is null || searchFolders.Count == 0)
		{
			return new Dictionary<string, string>();
		}
		string path = Path.Combine(searchFolders[0], set.MainFileName);
		try
		{
			Directory.CreateDirectory(searchFolders[0]);
			File.WriteAllBytes(path, content);
			log.LogInfo($"{modName}: wrote default {path}");
		}
		catch (Exception e)
		{
			log.LogError($"{modName}: writing default {path} failed: {e.Message}");
			return new Dictionary<string, string>();
		}
		return Read(new[] { path });
	}

	private void WriteAtomic(string path, string content)
	{
		try
		{
			if (File.Exists(path) && File.ReadAllText(path) == content)
			{
				return;
			}
			ReplaceThroughTempFile(path, content);
			log.LogInfo($"{modName}: wrote {path}");
		}
		catch (Exception e)
		{
			log.LogError($"{modName}: writing {path} failed: {e.Message}");
		}
	}

	private static void ReplaceThroughTempFile(string path, string content)
	{
		string temp = path + ".tmp";
		File.WriteAllText(temp, content, new UTF8Encoding(false));
		if (File.Exists(path))
		{
			File.Replace(temp, path, null);
		}
		else
		{
			File.Move(temp, path);
		}
	}
}
