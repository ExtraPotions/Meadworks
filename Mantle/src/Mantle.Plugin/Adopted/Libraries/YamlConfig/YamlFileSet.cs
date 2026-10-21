using System;
using System.Collections.Generic;
using ExtraPotions.Mantle.Embedded.Charter;

namespace ExtraPotions.Mantle.Embedded.YamlConfig;

/// <summary>
/// One family of YAML files (<c>Name*.yml</c>): how to create the model that reads them and how to apply it.
/// Register it with a <see cref="YamlFileHub"/>, which fills <see cref="Files"/> and <see cref="Current"/>.
/// </summary>
public sealed class YamlFileSet
{
	/// <param name="filePattern">File name pattern with one <c>*</c>, e.g. <c>MyMod.Rules*.yml</c>.</param>
	/// <param name="syncKey">Name of the Charter article carrying the files; unique within the mod.</param>
	/// <param name="createModel">Creates a fresh model for every parse.</param>
	/// <param name="apply">Hands a successfully parsed model to the mod.</param>
	public YamlFileSet(string filePattern, string syncKey, Func<YamlModel> createModel, Action<YamlModel> apply)
	{
		FilePattern = filePattern;
		MainFileName = filePattern.Replace("*", "");
		SyncKey = syncKey;
		CreateModel = createModel;
		Apply = apply;
		EditorLabel = () => "Edit " + MainFileName;
	}

	/// <summary>The file name pattern, e.g. <c>MyMod.Rules*.yml</c>.</summary>
	public string FilePattern { get; }

	/// <summary>The pattern without the <c>*</c>: the file that is listed first and that defaults are written to.</summary>
	public string MainFileName { get; }

	/// <summary>Name of the Charter article that carries the files to players.</summary>
	public string SyncKey { get; }

	/// <summary>Creates a fresh model for every parse.</summary>
	public Func<YamlModel> CreateModel { get; }

	/// <summary>Hands a successfully parsed model to the mod.</summary>
	public Action<YamlModel> Apply { get; }

	/// <summary>Whether the set is in use; when false, missing files are not warned about and models are not applied.</summary>
	public Func<bool> Enabled { get; set; } = () => true;

	/// <summary>Content written as the main file when no file exists, e.g. an embedded resource.</summary>
	public Func<byte[]?>? DefaultContent { get; set; }

	/// <summary>Caption of the button that opens this set in the editor.</summary>
	public Func<string> EditorLabel { get; set; }

	/// <summary>The current file contents by full path (paths as the server sent them on clients), main file first.</summary>
	public IReadOnlyDictionary<string, string> Files { get; internal set; } = new Dictionary<string, string>();

	/// <summary>The last model that parsed without errors, or null before the first successful parse.</summary>
	public YamlModel? Current { get; internal set; }

	internal Article<List<string>>? Channel { get; set; }

	internal Dictionary<string, DateTime> Snapshot { get; } = new(StringComparer.OrdinalIgnoreCase);

	internal int ReloadPending;
}
