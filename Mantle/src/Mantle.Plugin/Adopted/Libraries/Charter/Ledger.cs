using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;

namespace ExtraPotions.Mantle.Embedded.Charter;

/// <summary>
/// One charter's register of clauses and articles: lookup by entry, by section and key, by article name, the
/// binding clause, and the quiet write that changes an entry without saving the .cfg or being mistaken for an edit.
/// </summary>
internal sealed class Ledger
{
	private readonly Dictionary<string, IClause> byName = new(StringComparer.Ordinal);
	private readonly Dictionary<ConfigEntryBase, IClause> byEntry = new();
	private readonly Dictionary<string, IArticle> articles = new(StringComparer.Ordinal);
	private readonly List<IClause> order = new();
	private Func<bool>? bindingOn;

	/// <summary>True while the library itself writes an entry; the change router ignores those writes.</summary>
	public bool Applying { get; private set; }

	public IClause? BindingClause { get; private set; }

	/// <summary>The binding entry's own value, converted; false without a binding entry.</summary>
	public bool BindingOn => bindingOn?.Invoke() ?? false;

	public IReadOnlyList<IClause> Clauses => order;

	/// <summary>Every clause that is not local, the binding clause included.</summary>
	public IEnumerable<IClause> Pushable => order.Where(c => !c.Local);

	public IEnumerable<IArticle> Articles => articles.Values;

	public int ArticleCount => articles.Count;

	public void Add(IClause clause)
	{
		string name = NameOf(clause.Section, clause.Key);
		if (byName.ContainsKey(name))
		{
			throw new ArgumentException($"clause {clause.Section}.{clause.Key} is already registered");
		}
		byName[name] = clause;
		byEntry[clause.Entry] = clause;
		order.Add(clause);
	}

	public void Add(IArticle article)
	{
		if (articles.ContainsKey(article.Name))
		{
			throw new ArgumentException($"article '{article.Name}' is already registered");
		}
		articles[article.Name] = article;
	}

	public void SetBinding(IClause clause, Func<bool> on)
	{
		BindingClause = clause;
		bindingOn = on;
	}

	public IClause? Find(string section, string key) => byName.TryGetValue(NameOf(section, key), out IClause? clause) ? clause : null;

	public IClause? Find(ConfigEntryBase entry) => byEntry.TryGetValue(entry, out IClause? clause) ? clause : null;

	public IArticle? FindArticle(string name) => articles.TryGetValue(name, out IArticle? article) ? article : null;

	public void RestoreOwnAll()
	{
		foreach (IClause clause in Pushable)
		{
			clause.RestoreOwn();
		}
	}

	/// <summary>Runs work with <see cref="Applying"/> raised, so the change router takes the entry changes it makes for the library's own.</summary>
	public void WhileApplying(Action work)
	{
		bool was = Applying;
		Applying = true;
		try
		{
			work();
		}
		finally
		{
			Applying = was;
		}
	}

	/// <summary>Runs a write to an entry without saving its .cfg and with <see cref="Applying"/> raised.</summary>
	public void Quietly(ConfigEntryBase entry, Action write)
	{
		ConfigFile file = entry.ConfigFile;
		bool save = file.SaveOnConfigSet;
		bool was = Applying;
		file.SaveOnConfigSet = false;
		Applying = true;
		try
		{
			write();
		}
		finally
		{
			Applying = was;
			file.SaveOnConfigSet = save;
		}
	}

	private static string NameOf(string section, string key) => section + "\n" + key;
}
