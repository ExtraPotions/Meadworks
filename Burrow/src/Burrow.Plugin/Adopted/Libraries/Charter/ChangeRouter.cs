using System;
using System.Collections.Generic;
using BepInEx.Configuration;

namespace ExtraPotions.Burrow.Embedded.Charter;

/// <summary>
/// Listens to every ConfigFile a clause lives in and decides what an entry change means: on the author it is
/// something to push; on a bound player it is reverted, or sent to the author when the player is a steward.
/// </summary>
internal sealed class ChangeRouter
{
	private readonly Charter charter;
	private readonly Ledger ledger;
	private readonly Journal journal;
	private readonly HashSet<ConfigFile> watched = new();

	public ChangeRouter(Charter charter, Ledger ledger, Journal journal)
	{
		this.charter = charter;
		this.ledger = ledger;
		this.journal = journal;
	}

	public void Watch(ConfigFile file)
	{
		if (watched.Add(file))
		{
			file.SettingChanged += OnSettingChanged;
		}
	}

	private void OnSettingChanged(object sender, SettingChangedEventArgs args)
	{
		try
		{
			if (!ledger.Applying)
			{
				Route(args.ChangedSetting);
			}
		}
		catch (Exception e)
		{
			journal.Error($"handling a change of {args.ChangedSetting.Definition} failed: {e}");
		}
	}

	private void Route(ConfigEntryBase entry)
	{
		IClause? clause = ledger.Find(entry);
		if (clause == null)
		{
			return;
		}
		clause.RememberOwn();
		if (Side.IsServer)
		{
			charter.Publisher.MarkDirty(clause);
			return;
		}
		Receiver receiver = charter.Receiver;
		if (!receiver.Bound || clause.Local)
		{
			return;
		}
		if (receiver.Steward)
		{
			charter.Amendments.Send(clause);
			return;
		}
		journal.Info($"{clause.Section}.{clause.Key} is bound by the server, reverting to {clause.AuthorToml}");
		clause.ApplyAuthor();
	}
}
