// Delve - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (c) 2026 liekos47, MIT (RestockRegen 0.19.0 at aa179d9120b4b141fdb306825136ba05be0908b0,
// src/AdminCommands.cs: the ZNet.RPC_Kick prefix that takes prefixed words before anyone is kicked, the adminlist check
// and the replies to the admin's console and screen). The MIT notice is in THIRD_PARTY_NOTICES/RestockRegen-MIT.txt.
// Changed by expDARE, 2026: verbs are Delve's (parsed by Delve.Core's DelveCommands); regenerate-now goes to the Delve
// engine with the Regenerate cheat verdict (Mischief decided for the calling peer); a host's or single player's own
// kick (ZNet.Kick on the server) is handled too; General / Kick Command Fallback off leaves every kick to the game. This
// is the fallback path; the Crucible command relay waits for CS-D1.
using System;
using System.Collections.Generic;
using System.Linq;
using ExtraPotions.Delve.Commands;
using ExtraPotions.Delve.Config;
using ExtraPotions.Meadworks.Crucible;
using HarmonyLib;

namespace ExtraPotions.Delve.Runtime
{
    public static class DelveKickCommands
    {
        private static bool Enabled => Plugin.Ready && Plugin.Settings != null && Plugin.Settings.Get(DelveSettings.General.KickCommandFallback)
            && DelveModules.ProductActive(out _);

        /// <summary>Runs a parsed command for a caller; the lines to answer with.</summary>
        public static IReadOnlyList<string> Run(DelveCommand command, bool isAdmin, bool cheatLockedForCaller, string caller,
            UnityEngine.Vector3 position = default, long callerUid = 0L, Action<string>? print = null)
        {
            var engine = Plugin.Engine;
            var settings = Plugin.Settings;
            if (!isAdmin) return new[] { DelveCommands.Reply(RegenerateVerdict.NotAdmin, Array.Empty<Engine.DelveKind>()) };
            Plugin.Log?.Info("Delve admin command from " + caller + ": " + command.Verb + (command.Kinds.Count > 0 ? " " + string.Join(",", command.Kinds) : ""));
            switch (command.Verb)
            {
                case CommandVerb.Status:
                    return engine?.StatusLines().ToList() ?? new List<string> { "Delve status: not started." };
                case CommandVerb.Regenerate:
                    if (engine == null || settings == null || !engine.Running) return new[] { "Delve: the regeneration engine is not running on this server." };
                    var verdict = settings.RegenerateFor(true, cheatLockedForCaller);
                    if (verdict == RegenerateVerdict.CheatLocked) MeadworksBlockedOptions.TryUse(Plugin.Guid, DelveSettings.Cheats.RegenerateWithoutWaiting.Id, ConfigScope.Cheat);
                    var mode = DelveCommands.Mode(verdict);
                    bool skipDelay = mode == RegenerateMode.SkipDelay;
                    var kinds = engine.Request(command.Kinds.Where(engine.Active), skipDelay);
                    engine.ConsiderClaimClear(command.Kinds, kinds, skipDelay);
                    if (kinds.Count == 0) return new[] { "Delve: none of those kinds is on (switched off or stepped aside)." };
                    return new[] { DelveCommands.Reply(verdict, kinds) };
                case CommandVerb.Surface:
                    return DelveSurface.Begin(position, command.Scope, callerUid, caller, print);
                default:
                    return DelveCommands.Help();
            }
        }

        /// <summary>A remote player's kick: the word reaches the server, which checks adminlist.txt by platform ID.</summary>
        [HarmonyPatch(typeof(ZNet), "RPC_Kick")]
        private static class RemoteKick
        {
            private static bool Prefix(ZNet __instance, ZRpc rpc, string user)
            {
                var command = DelveCommands.Parse(user);
                if (command == null || !Enabled) return true; // a real kick
                try
                {
                    var host = rpc.GetSocket().GetHostName();
                    bool admin = __instance.ListContainsId(__instance.m_adminList, host);
                    bool locked = !MeadworksMischief.DecideForPeer(rpc, MischiefCategory.World).Allowed;
                    ZNetPeer peer = __instance.GetPeer(rpc);
                    if (command.Verb == CommandVerb.Surface && peer == null) { __instance.RemotePrint(rpc, "Delve: the surface reset needs your position; join the world first."); return false; }
                    foreach (var line in Run(command, admin, locked, peer?.m_playerName ?? "?", peer?.GetRefPos() ?? default, peer?.m_uid ?? 0L))
                    {
                        __instance.RemotePrint(rpc, line);
                        if (peer != null) DelveNotices.Tell(peer.m_uid, line, ServerNoticePlacement.TopLeft);
                    }
                }
                catch (Exception error)
                {
                    Plugin.Log?.Error("Delve admin command failed: " + error.GetType().Name + ": " + error.Message);
                    __instance.RemotePrint(rpc, "Delve: the command failed, see the server log.");
                }
                return false; // never reaches the real kick
            }
        }

        /// <summary>The single-player or host player's own kick: on the server ZNet.Kick kicks directly, without the RPC.</summary>
        [HarmonyPatch(typeof(ZNet), nameof(ZNet.Kick))]
        private static class LocalKick
        {
            private static bool Prefix(ZNet __instance, string user)
            {
                if (!__instance.IsServer()) return true;
                var command = DelveCommands.Parse(user);
                if (command == null || !Enabled) return true;
                try
                {
                    // The authority player of single player or a host may run Delve's admin actions (design 3); the cheat
                    // gate is the local Mischief decision.
                    var position = Player.m_localPlayer != null ? Player.m_localPlayer.transform.position : __instance.GetReferencePosition();
                    foreach (var line in Run(command, true, false, "the host", position, ZNet.GetUID(), line => global::Console.instance?.Print(line)))
                    {
                        global::Console.instance?.Print(line);
                        DelveNotices.Tell(ZNet.GetUID(), line, ServerNoticePlacement.TopLeft);
                    }
                }
                catch (Exception error)
                {
                    Plugin.Log?.Error("Delve admin command failed: " + error.GetType().Name + ": " + error.Message);
                }
                return false;
            }
        }
    }
}
