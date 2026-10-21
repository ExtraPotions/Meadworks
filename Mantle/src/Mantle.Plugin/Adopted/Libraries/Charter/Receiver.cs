using System;
using System.Collections.Generic;

namespace ExtraPotions.Mantle.Embedded.Charter;

/// <summary>
/// The player side of pushes: reassembles them, applies clause and article values, keeps the bound and steward
/// flags the author sent, and returns the player's own values when the connection ends.
/// </summary>
internal sealed class Receiver
{
	private readonly Charter charter;
	private readonly Ledger ledger;
	private readonly Journal journal;
	private readonly FragmentAssembler assembler = new();
	private readonly HashSet<string> unknown = new();
	private bool protocolWarned;

	public Receiver(Charter charter, Ledger ledger, Journal journal)
	{
		this.charter = charter;
		this.ledger = ledger;
		this.journal = journal;
	}

	public bool Bound { get; private set; }
	public bool Steward { get; private set; }
	public ZRpc? Rpc { get; private set; }
	public int PushCount { get; private set; }
	public DateTime? LastPush { get; private set; }
	public int PushedBytes { get; private set; }

	public void OnPush(ZRpc rpc, ZPackage pkg)
	{
		try
		{
			Receive(rpc, pkg);
		}
		catch (Exception e)
		{
			journal.Error($"applying a push failed: {e}");
		}
	}

	/// <summary>Ends the session when the server is gone; called every frame.</summary>
	public void Poll()
	{
		if (Rpc != null && (ZNet.instance == null || !Rpc.IsConnected()))
		{
			EndSession();
		}
	}

	private void Receive(ZRpc rpc, ZPackage pkg)
	{
		if (!ProtocolMatches(pkg.ReadInt()) || pkg.ReadString() != charter.Guid)
		{
			return;
		}
		int sequence = pkg.ReadInt();
		int index = pkg.ReadInt();
		int count = pkg.ReadInt();
		bool compressed = pkg.ReadBool();
		byte[] data = pkg.ReadByteArray();
		journal.Trace($"fragment {index + 1}/{count} of push #{sequence}, {data.Length} bytes");
		byte[]? body = assembler.Add(sequence, index, count, compressed, data, out int wireBytes);
		if (body != null)
		{
			Apply(rpc, PushBody.FromBytes(body), sequence, wireBytes);
		}
	}

	/// <summary>A wrong protocol number logs one warning per connection and drops the push.</summary>
	private bool ProtocolMatches(int protocol)
	{
		if (protocol == Fragmenter.Protocol)
		{
			return true;
		}
		if (!protocolWarned)
		{
			journal.Warning($"push with protocol {protocol} dropped, this build speaks {Fragmenter.Protocol}");
		}
		protocolWarned = true;
		return false;
	}

	private void Apply(ZRpc rpc, PushBody body, int sequence, int wireBytes)
	{
		Rpc = rpc;
		bool wasBound = Bound;
		Bound = body.Bound;
		Steward = body.Steward;
		foreach (ClauseRecord record in body.Clauses)
		{
			ApplyClause(record);
		}
		if (wasBound && !Bound)
		{
			RestoreExceptBinding();
		}
		List<IArticle> changed = new();
		foreach (ArticleRecord record in body.Articles)
		{
			ApplyArticle(record, changed);
		}
		PushCount++;
		LastPush = DateTime.Now;
		PushedBytes = wireBytes;
		journal.Info($"push #{sequence} received: {body.Clauses.Count} clause(s), {body.Articles.Count} article(s), {wireBytes} bytes, bound {Bound}, steward {Steward}");
		Finish(body.Notice, changed);
	}

	private void Finish(string notice, List<IArticle> changed)
	{
		if (notice.Length > 0)
		{
			journal.Warning(notice);
		}
		foreach (IArticle article in changed)
		{
			article.Raise();
		}
		charter.AfterPush(PushCount == 1);
	}

	private void ApplyClause(ClauseRecord record)
	{
		IClause? clause = ledger.Find(record.Section, record.Key);
		if (clause == null || clause.Local)
		{
			if (unknown.Add(record.Section + "." + record.Key))
			{
				journal.Info($"push names {(clause == null ? "unknown" : "local")} clause {record.Section}.{record.Key}, ignored");
			}
			return;
		}
		if (!clause.TakeAuthor(record.Toml))
		{
			journal.Warning($"value '{record.Toml}' for {record.Section}.{record.Key} is not readable, ignored");
			return;
		}
		if (Bound || clause == ledger.BindingClause)
		{
			clause.ApplyAuthor();
			journal.Trace($"applied {record.Section}.{record.Key} = {record.Toml}");
		}
	}

	private void ApplyArticle(ArticleRecord record, List<IArticle> changed)
	{
		IArticle? article = ledger.FindArticle(record.Name);
		if (article == null)
		{
			if (unknown.Add("article " + record.Name))
			{
				journal.Info($"push names unknown article '{record.Name}', ignored");
			}
			return;
		}
		if (article.Kind != record.Kind)
		{
			journal.Warning($"article '{record.Name}' arrived as {record.Kind}, expected {article.Kind}, ignored");
			return;
		}
		if (!Bound && !article.Standing)
		{
			journal.Trace($"ordinary article '{record.Name}' arrived while unbound, own value kept");
			return;
		}
		if (article.Accept(record.Value))
		{
			changed.Add(article);
		}
		journal.Trace($"article '{record.Name}' {(changed.Contains(article) ? "changed" : "unchanged")}");
	}

	private void RestoreExceptBinding()
	{
		foreach (IClause clause in ledger.Pushable)
		{
			if (clause != ledger.BindingClause)
			{
				clause.RestoreOwn();
			}
		}
	}

	private void EndSession()
	{
		Rpc = null;
		Bound = false;
		Steward = false;
		PushCount = 0;
		protocolWarned = false;
		assembler.Reset();
		unknown.Clear();
		ledger.RestoreOwnAll();
		journal.Info("connection ended, own values restored");
		charter.AfterSessionEnd();
	}
}
