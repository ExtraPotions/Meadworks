// Adapted from assets/references/source/ValheimMods-openkeep-3.0.0/ValheimModLibs/YamlConfig/YamlNode.cs at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Copyright (C) 2026 Gerald Glasgow. SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Saga namespace, configuration, CS1 and ownership boundaries.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace YamlConfig;

/// <summary>The shape of a <see cref="YamlNode"/>.</summary>
public enum YamlNodeKind
{
	/// <summary>A text value.</summary>
	Scalar,
	/// <summary>A sequence of values.</summary>
	List,
	/// <summary>A mapping from keys to values.</summary>
	Map,
	/// <summary>A key that is present but has no value.</summary>
	Null,
	/// <summary>A key that is not present at all.</summary>
	Missing,
}

/// <summary>
/// One value of a YAML tree as a <see cref="YamlModel"/> sees it: a scalar, a list or a map, wrapping the object
/// graph of YamlDotNet's untyped deserializer. Reads report problems to the model with the node's path, so a
/// message reads like <c>rules[3].limits.Count: expected a number, found a list</c>. <see cref="Get"/> never
/// returns null: a key that is not there yields a <see cref="YamlNodeKind.Missing"/> node that reads nothing and
/// reports nothing, so calls chain safely.
/// </summary>
public sealed partial class YamlNode
{
	private readonly YamlModel model;
	private readonly object? value;
	private List<KeyValuePair<string, YamlNode>>? mapEntries;
	private Dictionary<string, YamlNode>? mapLookup;
	private List<YamlNode>? listItems;
	private HashSet<string>? askedKeys;
	private bool visited;
	private bool shapeReported;

	internal YamlNode(YamlModel model, object? value, string path, YamlNodeKind kind)
	{
		this.model = model;
		this.value = value;
		Path = path;
		Kind = kind;
	}

	internal YamlNode(YamlModel model, object? value, string path) : this(model, value, path, KindOf(value))
	{
	}

	/// <summary>Dotted path from the root, for messages: <c>rules[3].limits.Count</c>. Empty for the root.</summary>
	public string Path { get; }

	/// <summary>The shape of this node.</summary>
	public YamlNodeKind Kind { get; }

	/// <summary>The scalar text, or null for anything that is not a scalar.</summary>
	public string? Text => Kind == YamlNodeKind.Scalar ? Convert.ToString(value, CultureInfo.InvariantCulture) : null;

	/// <summary>Number of children: entries of a map, items of a list, otherwise 0.</summary>
	public int Count => Kind switch
	{
		YamlNodeKind.Map => MapEntries.Count,
		YamlNodeKind.List => ListItems.Count,
		_ => 0,
	};

	/// <summary>
	/// The child of a map by key, case-insensitive. Returns a <see cref="YamlNodeKind.Missing"/> node when the key
	/// is absent or this node is not a map (which is reported once). Marks the key as expected for
	/// <see cref="WarnUnknownKeys"/>.
	/// </summary>
	public YamlNode Get(string key)
	{
		string childPath = string.IsNullOrEmpty(Path) ? key : Path + "." + key;
		if (!ExpectShape(YamlNodeKind.Map, "a map"))
		{
			return new YamlNode(model, null, childPath, YamlNodeKind.Missing);
		}
		visited = true;
		AskedKeys.Add(key);
		return MapLookup.TryGetValue(key, out YamlNode child) ? child : new YamlNode(model, null, childPath, YamlNodeKind.Missing);
	}

	/// <summary>The key/value pairs of a map in file order; empty for anything else. Marks every key as expected.</summary>
	public IReadOnlyList<KeyValuePair<string, YamlNode>> Entries
	{
		get
		{
			if (!ExpectShape(YamlNodeKind.Map, "a map"))
			{
				return Array.Empty<KeyValuePair<string, YamlNode>>();
			}
			visited = true;
			AskedKeys.UnionWith(MapEntries.Select(e => e.Key));
			return MapEntries;
		}
	}

	/// <summary>The items of a list in order; empty for anything else.</summary>
	public IReadOnlyList<YamlNode> Items => ExpectShape(YamlNodeKind.List, "a list") ? ListItems : Array.Empty<YamlNode>();

	/// <summary>True when this is a scalar equal to the keyword, case-insensitive (for words like <c>inherit</c>).</summary>
	public bool Is(string keyword) => Kind == YamlNodeKind.Scalar && string.Equals(Text!.Trim(), keyword, StringComparison.OrdinalIgnoreCase);

	/// <summary>Records an error at this node's path. The file set is rejected when a model has errors.</summary>
	public void Error(string message) => model.AddError(Prefixed(message));

	/// <summary>Records a warning at this node's path. Warnings are logged, the file set is still applied.</summary>
	public void Warn(string message) => model.AddWarning(Prefixed(message));

	/// <summary>
	/// Warns about keys nobody asked for, in this map and recursively in every map below it that a model visited
	/// through <see cref="Get"/> or <see cref="Entries"/>. Maps that were never visited are left alone.
	/// </summary>
	public void WarnUnknownKeys()
	{
		if (Kind == YamlNodeKind.Map && visited)
		{
			List<string> unknown = MapEntries.Select(e => e.Key).Where(k => !AskedKeys.Contains(k)).ToList();
			if (unknown.Count > 0)
			{
				Warn("unknown keys " + string.Join(", ", unknown));
			}
			foreach (KeyValuePair<string, YamlNode> entry in MapEntries)
			{
				entry.Value.WarnUnknownKeys();
			}
		}
		else if (Kind == YamlNodeKind.List && listItems is not null)
		{
			listItems.ForEach(item => item.WarnUnknownKeys());
		}
	}

	private string Prefixed(string message) => string.IsNullOrEmpty(Path) ? message : Path + ": " + message;

	private HashSet<string> AskedKeys => askedKeys ??= new HashSet<string>(StringComparer.OrdinalIgnoreCase);

	private List<KeyValuePair<string, YamlNode>> MapEntries
	{
		get
		{
			if (mapEntries is null)
			{
				BuildMap();
			}
			return mapEntries!;
		}
	}

	private Dictionary<string, YamlNode> MapLookup
	{
		get
		{
			if (mapLookup is null)
			{
				BuildMap();
			}
			return mapLookup!;
		}
	}

	private List<YamlNode> ListItems
	{
		get
		{
			if (listItems is null)
			{
				IList<object> items = (IList<object>)value!;
				listItems = items.Select((item, i) => new YamlNode(model, item, $"{Path}[{i}]")).ToList();
			}
			return listItems;
		}
	}

	private void BuildMap()
	{
		mapEntries = new List<KeyValuePair<string, YamlNode>>();
		mapLookup = new Dictionary<string, YamlNode>(StringComparer.OrdinalIgnoreCase);
		foreach (KeyValuePair<object, object> pair in (IDictionary<object, object>)value!)
		{
			string key = Convert.ToString(pair.Key, CultureInfo.InvariantCulture) ?? "";
			string childPath = string.IsNullOrEmpty(Path) ? key : Path + "." + key;
			YamlNode child = new(model, pair.Value, childPath);
			if (mapLookup.ContainsKey(key))
			{
				child.Warn("duplicate key (differs only in case), ignored");
				continue;
			}
			mapLookup.Add(key, child);
			mapEntries.Add(new KeyValuePair<string, YamlNode>(key, child));
		}
	}

	/// <summary>
	/// True when the node has the wanted shape. Missing nodes fail silently; every other mismatch is reported once
	/// per node as an error naming what was expected and what was found.
	/// </summary>
	private bool ExpectShape(YamlNodeKind wanted, string description)
	{
		if (Kind == wanted)
		{
			return true;
		}
		if (Kind != YamlNodeKind.Missing && !shapeReported)
		{
			shapeReported = true;
			Error(Kind == YamlNodeKind.Null ? $"expected {description} but the key has no value" : $"expected {description}, found {Describe(Kind)}");
		}
		return false;
	}

	private static string Describe(YamlNodeKind kind) => kind switch
	{
		YamlNodeKind.Scalar => "a single value",
		YamlNodeKind.List => "a list",
		YamlNodeKind.Map => "a map",
		_ => "nothing",
	};

	private static YamlNodeKind KindOf(object? value) => value switch
	{
		null => YamlNodeKind.Null,
		IDictionary<object, object> => YamlNodeKind.Map,
		IList<object> => YamlNodeKind.List,
		_ => YamlNodeKind.Scalar,
	};
}
