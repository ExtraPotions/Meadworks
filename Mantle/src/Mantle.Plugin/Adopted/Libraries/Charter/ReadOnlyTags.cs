using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx.Configuration;
using HarmonyLib;

namespace ExtraPotions.Mantle.Embedded.Charter;

/// <summary>The object placed in a clause's description tags while the player may not amend it.</summary>
internal sealed class ReadOnlyTag
{
	public bool ReadOnly = true;
}

/// <summary>
/// Adds one <see cref="ReadOnlyTag"/> to every non-local clause while the charter binds and the player is no
/// steward, and removes it again. Configuration managers read tag objects by field name, so no manager is
/// referenced. Entries sharing the empty description are left alone; the tag would leak to every such entry.
/// </summary>
internal sealed class ReadOnlyTags
{
	private static readonly FieldInfo? TagsField = AccessTools.Field(typeof(ConfigDescription), "<Tags>k__BackingField");

	private readonly ReadOnlyTag tag = new();
	private bool applied;

	/// <summary>
	/// Called every frame by each charter: the ledger's clauses are only read (a LINQ sequence, made anew on each read)
	/// when the tags change.
	/// </summary>
	public void Update(bool readOnly, Ledger ledger)
	{
		if (readOnly == applied || TagsField == null)
		{
			return;
		}
		applied = readOnly;
		foreach (IClause clause in ledger.Pushable)
		{
			ConfigDescription description = clause.Entry.Description;
			if (description == null || ReferenceEquals(description, ConfigDescription.Empty))
			{
				continue;
			}
			object[] tags = description.Tags ?? new object[0];
			TagsField.SetValue(description, readOnly ? tags.Where(t => t != tag).Append(tag).ToArray() : tags.Where(t => t != tag).ToArray());
		}
	}
}
