using System;

namespace ExtraPotions.Mantle.Embedded.Charter;

/// <summary>
/// A steward's edit travelling to the author: the steward sends the clause and its new TOML value; the author
/// checks the sender, the clause and the value, sets its entry, saves its .cfg and lets the usual push carry
/// the change back to everyone, or answers with a notice that reverts the steward's entry.
/// </summary>
internal sealed class Amendments
{
	private readonly Charter charter;
	private readonly Ledger ledger;
	private readonly Journal journal;
	private readonly string rpcName;

	public Amendments(Charter charter, Ledger ledger, Journal journal)
	{
		this.charter = charter;
		this.ledger = ledger;
		this.journal = journal;
		rpcName = $"Charter_{charter.Guid}_Amend";
	}

	/// <summary>Steward side: sends the entry's current value to the author.</summary>
	public void Send(IClause clause)
	{
		ZRpc? rpc = charter.Receiver.Rpc;
		if (rpc == null || !rpc.IsConnected())
		{
			return;
		}
		string toml = clause.CurrentToml;
		ZPackage pkg = new();
		pkg.Write(Fragmenter.Protocol);
		pkg.Write(charter.Guid);
		pkg.Write(clause.Section);
		pkg.Write(clause.Key);
		pkg.Write(toml);
		rpc.Invoke(rpcName, pkg);
		journal.Info($"amendment sent: {clause.Section}.{clause.Key} = {toml}");
	}

	/// <summary>Author side: the RPC handler.</summary>
	public void OnAmend(ZRpc rpc, ZPackage pkg)
	{
		try
		{
			Receive(rpc, pkg);
		}
		catch (Exception e)
		{
			journal.Error($"handling an amendment failed: {e}");
		}
	}

	private void Receive(ZRpc rpc, ZPackage pkg)
	{
		if (pkg.ReadInt() != Fragmenter.Protocol || pkg.ReadString() != charter.Guid)
		{
			journal.Warning("amendment with a foreign protocol or guid dropped");
			return;
		}
		string section = pkg.ReadString();
		string key = pkg.ReadString();
		string toml = pkg.ReadString();
		ZNetPeer? peer = Side.PeerOf(rpc);
		if (peer == null || !Side.IsServer)
		{
			return;
		}
		string? reason = Accept(peer, section, key, toml);
		if (reason != null)
		{
			Reject(peer, section, key, reason);
		}
	}

	private string? Accept(ZNetPeer peer, string section, string key, string toml)
	{
		if (!Stewardship.IsSteward(peer))
		{
			return "you are not a steward";
		}
		IClause? clause = ledger.Find(section, key);
		if (clause == null)
		{
			return "unknown clause";
		}
		if (clause.Local)
		{
			return "the clause is local";
		}
		try
		{
			clause.Entry.SetSerializedValue(toml);
		}
		catch (Exception e)
		{
			return "bad value: " + e.Message;
		}
		clause.Entry.ConfigFile.Save();
		journal.Info($"amendment by {Side.NameOf(peer)} accepted: {section}.{key} = {toml}");
		return null;
	}

	private void Reject(ZNetPeer peer, string section, string key, string reason)
	{
		journal.Warning($"amendment by {Side.NameOf(peer)} rejected: {section}.{key}: {reason}");
		charter.Publisher.SendNotice(peer, ledger.Find(section, key), $"amendment of {section}.{key} rejected: {reason}");
	}
}
