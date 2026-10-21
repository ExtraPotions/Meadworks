using System;

namespace ExtraPotions.Burrow.Embedded.Charter;

/// <summary>
/// A typed value the author pushes to every player that is not a ConfigEntry: a string, a list of strings, an
/// int, a float or a bool. The name is unique per charter. An ordinary article travels only while the charter
/// binds, like a non-local clause; a standing article travels always (state, not settings).
/// </summary>
public sealed class Article<T> : IArticle
{
	private readonly Charter charter;
	private readonly ArticleKind kind;
	private T value;

	public Article(Charter charter, string name, T initial, bool standing = false)
	{
		kind = ArticleValues.KindOf(typeof(T));
		this.charter = charter;
		Name = name;
		Standing = standing;
		value = initial;
		charter.Ledger.Add(this);
	}

	public string Name { get; }

	/// <summary>True: pushed whether or not the charter binds.</summary>
	public bool Standing { get; }

	public T Value => value;

	/// <summary>Raised on every side after the value changed, including on the author.</summary>
	public event Action? Changed;

	/// <summary>Sets the value on any side that is the author of its own values and pushes it from the server. On a bound player a no-op with a warning.</summary>
	public void Assign(T value)
	{
		if (!charter.IsAuthor)
		{
			charter.Journal.Warning($"article '{Name}' cannot be assigned while the server's charter binds");
			return;
		}
		if (ArticleValues.Same(this.value, value))
		{
			return;
		}
		this.value = value;
		Raise();
		charter.Publisher.MarkDirty(this);
	}

	ArticleKind IArticle.Kind => kind;

	object IArticle.Boxed => value!;

	bool IArticle.Accept(object incoming)
	{
		if (ArticleValues.Same(value, incoming))
		{
			return false;
		}
		value = (T)incoming;
		return true;
	}

	void IArticle.Raise() => Raise();

	private void Raise()
	{
		try
		{
			Changed?.Invoke();
		}
		catch (Exception e)
		{
			charter.Journal.Error($"a Changed handler of article '{Name}' threw: {e}");
		}
	}
}

/// <summary>The untyped view of an article the library works with.</summary>
internal interface IArticle
{
	string Name { get; }
	bool Standing { get; }
	ArticleKind Kind { get; }
	object Boxed { get; }

	/// <summary>Stores a received value; true when it differs from the current one.</summary>
	bool Accept(object incoming);

	void Raise();
}
