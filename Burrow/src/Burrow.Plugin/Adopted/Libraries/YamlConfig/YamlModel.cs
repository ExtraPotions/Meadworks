using System;
using System.Collections.Generic;
using System.IO;
using YamlDotNet.Serialization;

namespace ExtraPotions.Burrow.Embedded.YamlConfig;

/// <summary>
/// Base class for a mod's view of its YAML files. Derive from it, read what you need from the root map in
/// <see cref="Read"/>, and check cross-references in <see cref="Verify"/>. One model instance accumulates all
/// files of a set (<see cref="LoadAll"/>). Problems collect in <see cref="Errors"/> (the files are rejected and the
/// previous configuration stays) and <see cref="Warnings"/> (logged, the files are applied).
/// </summary>
public abstract class YamlModel
{
	private static readonly IDeserializer Parser = new DeserializerBuilder().Build();

	private string? currentFile;

	/// <summary>Problems that reject the files, each prefixed with the file and the YAML path.</summary>
	public List<string> Errors { get; } = new();

	/// <summary>Problems worth logging that do not reject the files.</summary>
	public List<string> Warnings { get; } = new();

	/// <summary>Named groups read by <see cref="ReadGroups"/>, for models whose entries may name a group.</summary>
	protected YamlGroupTable Groups { get; } = new();

	/// <summary>Parses one text, reads it and verifies. Invalid YAML becomes one error.</summary>
	public void Load(string yamlText)
	{
		ReadText(yamlText);
		Verify();
	}

	/// <summary>Parses and reads every file (path and text) in order into this one model, then verifies.</summary>
	public void LoadAll(IEnumerable<KeyValuePair<string, string>> files)
	{
		foreach (KeyValuePair<string, string> file in files)
		{
			currentFile = Path.GetFileName(file.Key);
			ReadText(file.Value);
		}
		currentFile = null;
		Verify();
	}

	/// <summary>The groups a name belongs to, innermost first, following groups that contain groups.</summary>
	public IReadOnlyList<string> ResolveGroups(string name) => Groups.Resolve(name);

	/// <summary>Reads what the model wants from the root map of one file. Unread keys are warned about afterwards.</summary>
	protected abstract void Read(YamlNode root);

	/// <summary>Called once after all files were read, for checks that span files.</summary>
	protected virtual void Verify()
	{
	}

	/// <summary>
	/// Reads the common <c>groups:</c> map (group name to a list of names, or one name; names may be other groups)
	/// into <see cref="Groups"/>. Call it from <see cref="Read"/>.
	/// </summary>
	protected void ReadGroups(YamlNode root, string key = "groups")
	{
		foreach (KeyValuePair<string, YamlNode> entry in root.Get(key).Entries)
		{
			if (entry.Value.TryStringList(out List<string> members))
			{
				Groups.Add(entry.Key, members);
			}
		}
	}

	internal void AddError(string message) => Errors.Add(WithFile(message));

	internal void AddWarning(string message) => Warnings.Add(WithFile(message));

	private string WithFile(string message) => currentFile is null ? message : currentFile + ": " + message;

	private void ReadText(string yamlText)
	{
		if (!TryParse(yamlText, out object? graph) || graph is null)
		{
			return;
		}
		if (graph is not IDictionary<object, object>)
		{
			AddError("the file must be a map of keys at the top level, not " + (graph is IList<object> ? "a list" : "a single value"));
			return;
		}
		YamlNode root = new(this, graph, "");
		Read(root);
		root.WarnUnknownKeys();
	}

	/// <summary>Parses the text (a BOM is stripped); invalid YAML becomes one error and returns false.</summary>
	private bool TryParse(string yamlText, out object? graph)
	{
		try
		{
			graph = Parser.Deserialize<object?>(yamlText.TrimStart('\uFEFF'));
			return true;
		}
		catch (Exception e)
		{
			AddError("invalid YAML: " + e.Message + (e.InnerException is null ? "" : " (" + e.InnerException.Message + ")"));
			graph = null;
			return false;
		}
	}
}
