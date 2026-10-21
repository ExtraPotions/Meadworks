using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;

namespace ExtraPotions.Burrow.Embedded.Charter;

/// <summary>
/// The Harmony patches, installed once per copy: peer RPC registration on a new connection, the first push and
/// the family greeting when the game accepts a peer, and for the lead copy the refusal screen and the console
/// command. Also the per-frame tick that drives every charter of this copy.
/// </summary>
internal static class GameHooks
{
	private static readonly List<Charter> charters = new();
	private static bool installed;

	public static bool Lead { get; private set; }

	public static Journal? LeadJournal => charters.Count > 0 ? charters[0].Journal : null;

	public static void Track(Charter charter) => charters.Add(charter);

	public static void Install(Harmony harmony)
	{
		if (installed)
		{
			return;
		}
		installed = true;
		Lead = charters.Count > 0 && Family.ClaimLead(charters[0].Guid);
		harmony.Patch(AccessTools.Method(typeof(ZNet), "OnNewConnection"), postfix: new HarmonyMethod(typeof(GameHooks), nameof(AfterNewConnection)));
		harmony.Patch(AccessTools.Method(typeof(ZNet), "RPC_PeerInfo"), postfix: new HarmonyMethod(typeof(GameHooks), nameof(AfterPeerInfo)));
		if (Lead)
		{
			FamilyRpc.Journal = LeadJournal;
			harmony.Patch(AccessTools.Method(typeof(FejdStartup), "ShowConnectError"), postfix: new HarmonyMethod(typeof(GameHooks), nameof(AfterShowConnectError)));
			harmony.Patch(AccessTools.Method(typeof(Terminal), "InitTerminal"), postfix: new HarmonyMethod(typeof(GameHooks), nameof(AfterInitTerminal)));
		}
		Ticker.Ensure();
	}

	public static void Tick(float now)
	{
		foreach (Charter charter in charters)
		{
			try
			{
				charter.Tick(now);
			}
			catch (Exception e)
			{
				charter.Journal.Error($"tick failed: {e}");
			}
		}
		if (Lead)
		{
			FamilyRpc.Tick(now);
		}
	}

	private static void AfterNewConnection(ZNet __instance, ZNetPeer peer)
	{
		try
		{
			bool server = __instance.IsServer();
			foreach (Charter charter in charters)
			{
				charter.Attach(peer, server);
			}
			if (Lead)
			{
				FamilyRpc.Attach(peer, server);
			}
		}
		catch (Exception e)
		{
			LeadJournal?.Error($"registering peer RPCs failed: {e}");
		}
	}

	private static void AfterPeerInfo(ZNet __instance, ZRpc rpc)
	{
		try
		{
			ZNetPeer? peer = __instance.IsServer() ? __instance.GetPeers().FirstOrDefault(p => p.m_rpc == rpc) : null;
			if (peer == null || !peer.IsReady() || !rpc.IsConnected())
			{
				return;
			}
			if (Lead)
			{
				FamilyRpc.Greet(peer);
			}
			foreach (Charter charter in charters)
			{
				charter.Publisher.SendFirst(peer);
			}
		}
		catch (Exception e)
		{
			LeadJournal?.Error($"greeting a new peer failed: {e}");
		}
	}

	private static void AfterShowConnectError(FejdStartup __instance) => RefusalScreen.Show(__instance);

	private static void AfterInitTerminal() => ConsoleCommands.Register();
}
