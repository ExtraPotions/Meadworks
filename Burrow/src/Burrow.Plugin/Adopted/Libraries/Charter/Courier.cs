using System.Collections.Generic;
using System.Linq;

namespace ExtraPotions.Burrow.Embedded.Charter;

/// <summary>
/// Carries pushes from the author to each peer: one lane per peer with its own sequence and a queue of
/// fragments that go out one per frame, so a large push does not stall the peer's other traffic.
/// </summary>
internal sealed class Courier
{
	/// <summary>No fragment is sent while the peer's socket still holds this much unsent data.</summary>
	private const int HoldAboveQueued = 512 * 1024;

	private sealed class Lane
	{
		public int Sequence;
		public bool Steward;
		public readonly Queue<ZPackage> Fragments = new();
	}

	private readonly Dictionary<ZNetPeer, Lane> lanes = new();
	private readonly List<ZNetPeer> peers = new();
	private readonly string guid;
	private readonly string rpcName;
	private readonly Journal journal;

	public Courier(string guid, Journal journal)
	{
		this.guid = guid;
		this.journal = journal;
		rpcName = $"Charter_{guid}_Push";
	}

	/// <summary>The steward flag last sent to the peer, or null before its first push.</summary>
	public bool? LastSteward(ZNetPeer peer) => lanes.TryGetValue(peer, out Lane? lane) ? lane.Steward : null;

	/// <summary>Queues one push for a peer; the fragments leave from <see cref="Tick"/>.</summary>
	public void Send(ZNetPeer peer, PushBody body, string reason)
	{
		byte[] bytes = body.ToBytes();
		if (bytes.Length > Fragmenter.LargestBody)
		{
			journal.Error($"push refused: {bytes.Length} bytes exceed {Fragmenter.LargestBody} ({guid})");
			return;
		}
		if (!lanes.TryGetValue(peer, out Lane? lane))
		{
			lanes[peer] = lane = new Lane();
		}
		lane.Steward = body.Steward;
		int sequence = ++lane.Sequence;
		List<ZPackage> fragments = Fragmenter.Split(guid, sequence, bytes, out int wireBytes, out bool compressed);
		foreach (ZPackage fragment in fragments)
		{
			lane.Fragments.Enqueue(fragment);
		}
		journal.Info($"push #{sequence} to {Side.NameOf(peer)} ({reason}): {body.Clauses.Count} clause(s), {body.Articles.Count} article(s), " +
			$"{wireBytes} bytes{(compressed ? " compressed" : "")}, {fragments.Count} fragment(s)");
	}

	/// <summary>
	/// Sends one fragment per peer and forgets peers that left. Every frame on the server, and a lane stays while its peer
	/// is connected, so the peers are copied into a list kept for the purpose (a peer that left is removed in the loop).
	/// </summary>
	public void Tick()
	{
		if (lanes.Count == 0)
		{
			return;
		}
		peers.Clear();
		foreach (ZNetPeer peer in lanes.Keys)
		{
			peers.Add(peer);
		}
		foreach (ZNetPeer peer in peers)
		{
			SendNext(peer);
		}
	}

	private void SendNext(ZNetPeer peer)
	{
		if (!Side.IsPresent(peer))
		{
			lanes.Remove(peer);
			return;
		}
		Lane lane = lanes[peer];
		if (lane.Fragments.Count == 0 || peer.m_socket.GetSendQueueSize() > HoldAboveQueued)
		{
			return;
		}
		peer.m_rpc.Invoke(rpcName, lane.Fragments.Dequeue());
		journal.Trace($"fragment sent to {Side.NameOf(peer)}, {lane.Fragments.Count} left in the queue");
	}
}
