using System;
using BepInEx.Configuration;

namespace ExtraPotions.Burrow.Embedded.Charter;

/// <summary>
/// One BepInEx <see cref="ConfigEntry{T}"/> registered with a charter. A local clause is never pushed and never
/// bound; every other clause takes the author's value while the charter binds.
/// </summary>
public sealed class Clause<T> : IClause
{
	private readonly Ledger ledger;
	private T own;
	private T author;

	internal Clause(ConfigEntry<T> entry, bool local, Ledger ledger)
	{
		Entry = entry;
		Local = local;
		this.ledger = ledger;
		own = entry.Value;
		author = default!;
		AuthorToml = "";
	}

	public ConfigEntry<T> Entry { get; }

	public bool Local { get; }

	/// <summary>The last value the author pushed; default before the first push. On the author: the entry's value.</summary>
	public T AuthorValue => Side.IsServer ? Entry.Value : author;

	/// <summary>The value in this side's own .cfg, remembered while the charter binds.</summary>
	public T OwnValue => own;

	ConfigEntryBase IClause.Entry => Entry;

	string IClause.Section => Entry.Definition.Section;

	string IClause.Key => Entry.Definition.Key;

	public string AuthorToml { get; private set; }

	string IClause.OwnToml => TomlTypeConverter.ConvertToString(own, typeof(T));

	string IClause.CurrentToml => Entry.GetSerializedValue();

	void IClause.RememberOwn() => own = Entry.Value;

	bool IClause.TakeAuthor(string toml)
	{
		try
		{
			author = TomlTypeConverter.ConvertToValue<T>(toml);
			AuthorToml = toml;
			return true;
		}
		catch (Exception)
		{
			return false;
		}
	}

	void IClause.ApplyAuthor() => ledger.Quietly(Entry, () => Entry.Value = author);

	void IClause.RestoreOwn() => ledger.Quietly(Entry, () => Entry.Value = own);
}

/// <summary>The untyped view of a clause the library works with.</summary>
internal interface IClause
{
	ConfigEntryBase Entry { get; }
	string Section { get; }
	string Key { get; }
	bool Local { get; }
	string AuthorToml { get; }
	string OwnToml { get; }
	string CurrentToml { get; }

	/// <summary>Takes the entry's current value as this side's own value.</summary>
	void RememberOwn();

	/// <summary>Remembers the author's value without touching the entry; false when the TOML is unreadable.</summary>
	bool TakeAuthor(string toml);

	/// <summary>Writes the author's value to the entry without saving the .cfg.</summary>
	void ApplyAuthor();

	/// <summary>Writes the own value back to the entry without saving the .cfg.</summary>
	void RestoreOwn();
}
