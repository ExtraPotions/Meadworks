
using System.Collections.Generic;

namespace ExtraPotions.Mantle.Embedded.Charter;

/// <summary>One clause value on the wire: section, key and the TOML string.</summary>
internal readonly struct ClauseRecord
{
	public ClauseRecord(string section, string key, string toml)
	{
		Section = section;
		Key = key;
		Toml = toml;
	}

	public string Section { get; }
	public string Key { get; }
	public string Toml { get; }
}

/// <summary>One article value on the wire: name, type tag and the value.</summary>
internal readonly struct ArticleRecord
{
	public ArticleRecord(string name, ArticleKind kind, object value)
	{
		Name = name;
		Kind = kind;
		Value = value;
	}

	public string Name { get; }
	public ArticleKind Kind { get; }
	public object Value { get; }
}

/// <summary>
/// The reassembled body of a push: the bound and steward flags, the clause records, the article records and a
/// notice for the receiver's log (empty except when an amendment was rejected).
/// </summary>
internal sealed class PushBody
{
	public bool Bound;
	public bool Steward;
	public string Notice = "";
	public readonly List<ClauseRecord> Clauses = new();
	public readonly List<ArticleRecord> Articles = new();

	public byte[] ToBytes()
	{
		ZPackage pkg = new();
		pkg.Write(Bound);
		pkg.Write(Steward);
		pkg.Write(Clauses.Count);
		foreach (ClauseRecord clause in Clauses)
		{
			pkg.Write(clause.Section);
			pkg.Write(clause.Key);
			pkg.Write(clause.Toml);
		}
		pkg.Write(Articles.Count);
		foreach (ArticleRecord article in Articles)
		{
			pkg.Write(article.Name);
			pkg.Write((byte)article.Kind);
			ArticleValues.Write(pkg, article.Kind, article.Value);
		}
		pkg.Write(Notice);
		return pkg.GetArray();
	}

	public static PushBody FromBytes(byte[] bytes)
	{
		ZPackage pkg = new(bytes);
		PushBody body = new() { Bound = pkg.ReadBool(), Steward = pkg.ReadBool() };
		int clauses = pkg.ReadInt();
		for (int i = 0; i < clauses; i++)
		{
			body.Clauses.Add(new ClauseRecord(pkg.ReadString(), pkg.ReadString(), pkg.ReadString()));
		}
		int articles = pkg.ReadInt();
		for (int i = 0; i < articles; i++)
		{
			string name = pkg.ReadString();
			ArticleKind kind = (ArticleKind)pkg.ReadByte();
			body.Articles.Add(new ArticleRecord(name, kind, ArticleValues.Read(pkg, kind)));
		}
		body.Notice = pkg.ReadString();
		return body;
	}
}
