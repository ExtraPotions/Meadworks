using System;
using System.Collections.Generic;
using System.Linq;

namespace ExtraPotions.Burrow.Embedded.Charter;

/// <summary>
/// The join check of the lead copy: the server sends its family registry to a new peer, the player answers with
/// its own, both compare. The server refuses a mismatching peer with a refusal code and disconnects it a second
/// later; the player refuses itself when the server lacks a mod it requires; a peer that never answers is
/// re-greeted every five seconds and refused only once twenty seconds have passed without a reply, when any of
/// the server's mods is mandatory.
/// </summary>
internal static class FamilyRpc
{
	private const string FamilyName = "Charter_Family";
	private const string RefuseName = "Charter_Refuse";
	private const float RetryInterval = 5f;
	private const float MaxWait = 20f;
	private const float Grace = 1f;

	private sealed class Waiting
	{
		public float NextRetry;
		public float Deadline;
	}

	private static readonly Dictionary<ZNetPeer, Waiting> awaiting = new();
	private static readonly Dictionary<ZNetPeer, float> leaving = new();
	private static readonly List<ZNetPeer> due = new();
	private static readonly List<ZNetPeer> gone = new();
	private static float now;

	public static Journal? Journal { get; set; }

	/// <summary>The server's registry as received at join, on the player; null when not connected.</summary>
	public static List<FamilyEntry>? ServerRegistry { get; private set; }

	public static void Attach(ZNetPeer peer, bool server)
	{
		if (server)
		{
			peer.m_rpc.Register<ZPackage>(FamilyName, OnClientRegistry);
			return;
		}
		ServerRegistry = null;
		peer.m_rpc.Register<ZPackage>(FamilyName, OnServerRegistry);
		peer.m_rpc.Register<ZPackage>(RefuseName, OnRefused);
	}

	/// <summary>Server: sends the registry to a peer the game just accepted and starts waiting for the answer.</summary>
	public static void Greet(ZNetPeer peer)
	{
		SendFamily(peer);
		awaiting[peer] = new Waiting { NextRetry = now + RetryInterval, Deadline = now + MaxWait };
	}

	private static void SendFamily(ZNetPeer peer)
	{
		ZPackage pkg = new();
		FamilyEntry.Write(pkg, Family.Entries());
		peer.m_rpc.Invoke(FamilyName, pkg);
	}

	public static void Tick(float time)
	{
		now = time;
		TickAwaiting();
		TickLeaving();
	}

	/// <summary>
	/// Runs every frame on every machine (in the lead copy), and both lists are nearly always empty: nothing is made
	/// unless a peer is due, and the due peers go into a list kept for the purpose (a peer may leave the dictionary while
	/// the loop runs).
	/// </summary>
	private static void TickAwaiting()
	{
		if (awaiting.Count == 0)
		{
			return;
		}
		due.Clear();
		foreach (KeyValuePair<ZNetPeer, Waiting> pair in awaiting)
		{
			if (pair.Value.NextRetry <= now)
			{
				due.Add(pair.Key);
			}
		}
		foreach (ZNetPeer peer in due)
		{
			TickPeer(peer, awaiting[peer]);
		}
	}

	private static void TickPeer(ZNetPeer peer, Waiting waiting)
	{
		if (!Side.IsPresent(peer))
		{
			awaiting.Remove(peer);
			return;
		}
		if (now >= waiting.Deadline)
		{
			awaiting.Remove(peer);
			List<FamilyEntry> mine = Family.Entries();
			if (mine.Any(e => e.Mandatory))
			{
				Refuse(peer, FamilyCheck.Compare(mine, new List<FamilyEntry>()));
			}
			return;
		}
		SendFamily(peer);
		waiting.NextRetry = now + RetryInterval;
	}

	private static void TickLeaving()
	{
		if (leaving.Count == 0)
		{
			return;
		}
		gone.Clear();
		foreach (KeyValuePair<ZNetPeer, float> pair in leaving)
		{
			if (pair.Value <= now)
			{
				gone.Add(pair.Key);
			}
		}
		foreach (ZNetPeer peer in gone)
		{
			leaving.Remove(peer);
			if (Side.IsPresent(peer))
			{
				ZNet.instance.Disconnect(peer);
			}
		}
	}

	private static void OnClientRegistry(ZRpc rpc, ZPackage pkg)
	{
		ZNetPeer? peer = Side.PeerOf(rpc);
		List<FamilyEntry>? theirs = FamilyEntry.Read(pkg);
		if (peer == null || theirs == null)
		{
			return;
		}
		awaiting.Remove(peer);
		List<string> lines = FamilyCheck.Compare(Family.Entries(), theirs);
		if (lines.Count > 0)
		{
			Refuse(peer, lines);
			return;
		}
		Journal?.Trace($"family check passed for {Side.NameOf(peer)}: {theirs.Count} mod(s)");
	}

	private static void OnServerRegistry(ZRpc rpc, ZPackage pkg)
	{
		List<FamilyEntry>? theirs = FamilyEntry.Read(pkg);
		if (theirs == null)
		{
			return;
		}
		ServerRegistry = theirs;
		List<FamilyEntry> mine = Family.Entries();
		ZPackage answer = new();
		FamilyEntry.Write(answer, mine);
		rpc.Invoke(FamilyName, answer);
		if (FamilyCheck.ServerLacksMandatory(theirs, mine))
		{
			Leave(rpc, FamilyCheck.Compare(theirs, mine));
		}
	}

	private static void OnRefused(ZRpc rpc, ZPackage pkg)
	{
		string id = pkg.ReadString();
		string text = pkg.ReadString();
		foreach (string line in text.Split('\n'))
		{
			Journal?.Error(line);
		}
		Journal?.Error($"refused by the server, refusal code {id}");
		RefusalScreen.Pending = text;
	}

	private static void Refuse(ZNetPeer peer, List<string> lines)
	{
		string id = FamilyCheck.NewRefusalCode();
		foreach (string line in lines)
		{
			Journal?.Error(line);
		}
		Journal?.Error($"refusing {Side.NameOf(peer)} ({Side.HostOf(peer)}), refusal code {id}");
		ZPackage pkg = new();
		pkg.Write(id);
		pkg.Write(FamilyCheck.Message(lines, id));
		peer.m_rpc.Invoke(RefuseName, pkg);
		leaving[peer] = now + Grace;
	}

	private static void Leave(ZRpc rpc, List<string> lines)
	{
		string id = FamilyCheck.NewRefusalCode();
		foreach (string line in lines)
		{
			Journal?.Error(line);
		}
		Journal?.Error($"leaving the server, refusal code {id}");
		RefusalScreen.Pending = FamilyCheck.Message(lines, id);
		rpc.GetSocket().Close();
	}
}
