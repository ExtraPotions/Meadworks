// Adapted from assets/references/source/ValheimMods-openkeep-3.0.0/ValheimModLibs/YamlConfig/YamlNodeReaders.cs at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Copyright (C) 2026 Gerald Glasgow. SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Saga namespace, configuration, CS1 and ownership boundaries.
using System;
using System.Collections.Generic;

namespace YamlConfig;

/// <summary>
/// Typed readers. Each returns true and the value when the node is a scalar of the right form; returns false
/// silently for a <see cref="YamlNodeKind.Missing"/> node; and reports an error at the node's path and returns
/// false for a null key, a wrong shape or text that does not parse. Parsing itself is <see cref="YamlScalarParser"/>.
/// </summary>
public sealed partial class YamlNode
{
	/// <summary>Reads an integer.</summary>
	public bool TryInt(out int result) => TryScalar(out result);

	/// <summary>Reads a number, invariant culture (a dot as the decimal separator).</summary>
	public bool TryFloat(out float result) => TryScalar(out result);

	/// <summary>Reads a boolean: true/false, yes/no, on/off or 1/0, case-insensitive.</summary>
	public bool TryBool(out bool result) => TryScalar(out result);

	/// <summary>Reads the scalar text as written.</summary>
	public bool TryString(out string result) => TryScalar(out result);

	/// <summary>Reads an enum member by name, case-insensitive.</summary>
	public bool TryEnum<T>(out T result) where T : struct, Enum => TryScalar(out result);

	/// <summary>
	/// Reads a list of scalars of one type (int, float, bool, string or an enum). Items that do not parse are
	/// reported at their own path and skipped; the result is false when any item failed.
	/// </summary>
	public bool TryList<T>(out List<T> items)
	{
		items = new List<T>();
		if (!ExpectShape(YamlNodeKind.List, "a list of " + YamlScalarParser.Plural<T>()))
		{
			return false;
		}
		bool allParsed = true;
		foreach (YamlNode item in ListItems)
		{
			if (item.TryScalar(out T parsed))
			{
				items.Add(parsed);
			}
			else
			{
				allParsed = false;
			}
		}
		return allParsed;
	}

	/// <summary>Reads a list of strings; a single scalar is accepted as a one-item list.</summary>
	public bool TryStringList(out List<string> items)
	{
		if (Kind == YamlNodeKind.Scalar)
		{
			items = new List<string> { Text! };
			return true;
		}
		return TryList(out items);
	}

	private bool TryScalar<T>(out T result)
	{
		result = default!;
		if (!ExpectShape(YamlNodeKind.Scalar, YamlScalarParser.Describe(typeof(T))))
		{
			return false;
		}
		if (YamlScalarParser.TryParse(Text!, typeof(T), out object? parsed))
		{
			result = (T)parsed!;
			return true;
		}
		Error($"'{Text}' is not {YamlScalarParser.Describe(typeof(T))}");
		return false;
	}
}
