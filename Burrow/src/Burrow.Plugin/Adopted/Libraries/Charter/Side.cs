using System.Collections.Generic;
using System.Linq;

namespace ExtraPotions.Burrow.Embedded.Charter;

/// <summary>Which side of the connection this process is, read from the game each time it is asked.</summary>
internal static class Side
{
	/// <summary>True on a dedicated server, for the hosting player, in single player and outside any game.</summary>
	public static bool IsServer => ZNet.instance == null || ZNet.instance.IsServer();

	/// <summary>The connected peers that passed the game's own join checks.</summary>
	public static List<ZNetPeer> ReadyPeers()
	{
		if (ZNet.instance == null)
		{
			return new List<ZNetPeer>();
		}
		return ZNet.instance.GetPeers().Where(p => p.IsReady() && p.m_rpc != null && p.m_rpc.IsConnected()).ToList();
	}

	public static ZNetPeer? PeerOf(ZRpc rpc)
	{
		return ZNet.instance == null ? null : ZNet.instance.GetPeers().FirstOrDefault(p => p.m_rpc == rpc);
	}

	/// <summary>True while the peer is still in the game's peer list and its socket is open.</summary>
	public static bool IsPresent(ZNetPeer peer)
	{
		return ZNet.instance != null && ZNet.instance.GetPeers().Contains(peer) && peer.m_rpc != null && peer.m_rpc.IsConnected();
	}

	public static string NameOf(ZNetPeer peer) => string.IsNullOrEmpty(peer.m_playerName) ? HostOf(peer) : peer.m_playerName;

	public static string HostOf(ZNetPeer peer) => peer.m_socket?.GetHostName() ?? "?";
}
