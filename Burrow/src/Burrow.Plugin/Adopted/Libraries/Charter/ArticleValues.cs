using System;
using System.Collections.Generic;
using System.Linq;

namespace ExtraPotions.Burrow.Embedded.Charter;

/// <summary>The type tag of an article value on the wire.</summary>
internal enum ArticleKind : byte
{
	Text = 1,
	TextList = 2,
	Integer = 3,
	Number = 4,
	Toggle = 5,
}

/// <summary>Reads, writes, sizes and compares the supported article value types.</summary>
internal static class ArticleValues
{
	public static ArticleKind KindOf(Type type)
	{
		if (type == typeof(string)) return ArticleKind.Text;
		if (type == typeof(List<string>)) return ArticleKind.TextList;
		if (type == typeof(int)) return ArticleKind.Integer;
		if (type == typeof(float)) return ArticleKind.Number;
		if (type == typeof(bool)) return ArticleKind.Toggle;
		throw new ArgumentException($"Charter articles hold string, List<string>, int, float or bool, not {type.Name}");
	}

	public static void Write(ZPackage pkg, ArticleKind kind, object? value)
	{
		switch (kind)
		{
			case ArticleKind.Text: pkg.Write(value as string ?? ""); break;
			case ArticleKind.TextList: WriteList(pkg, value as List<string>); break;
			case ArticleKind.Integer: pkg.Write(value is int i ? i : 0); break;
			case ArticleKind.Number: pkg.Write(value is float f ? f : 0f); break;
			case ArticleKind.Toggle: pkg.Write(value is bool b && b); break;
			default: throw new ArgumentException($"unknown article kind {kind}");
		}
	}

	public static object Read(ZPackage pkg, ArticleKind kind)
	{
		return kind switch
		{
			ArticleKind.Text => pkg.ReadString(),
			ArticleKind.TextList => ReadList(pkg),
			ArticleKind.Integer => pkg.ReadInt(),
			ArticleKind.Number => pkg.ReadSingle(),
			ArticleKind.Toggle => pkg.ReadBool(),
			_ => throw new ArgumentException($"unknown article kind {kind}"),
		};
	}

	/// <summary>Approximate size of the value on the wire, for the large-article warning.</summary>
	public static int Size(ArticleKind kind, object? value)
	{
		return kind switch
		{
			ArticleKind.Text => (value as string)?.Length ?? 0,
			ArticleKind.TextList => (value as List<string>)?.Sum(s => s?.Length ?? 0) ?? 0,
			_ => 4,
		};
	}

	public static bool Same(object? a, object? b)
	{
		if (a is List<string> first)
		{
			List<string> second = b as List<string> ?? new List<string>();
			return first.SequenceEqual(second, StringComparer.Ordinal);
		}
		if (b is List<string> other)
		{
			return other.Count == 0 && a is null;
		}
		return Equals(a, b);
	}

	private static void WriteList(ZPackage pkg, List<string>? list)
	{
		pkg.Write(list?.Count ?? 0);
		foreach (string item in list ?? new List<string>())
		{
			pkg.Write(item ?? "");
		}
	}

	private static List<string> ReadList(ZPackage pkg)
	{
		int count = pkg.ReadInt();
		List<string> list = new(Math.Max(0, count));
		for (int i = 0; i < count; i++)
		{
			list.Add(pkg.ReadString());
		}
		return list;
	}
}
