// Threshold - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: the C01/C02/C03 rules of design 4.3 and 5 as one portable table (same model as Edge.Core). The
// plugin takes the live facts from Crucible (MeadworksAuthority) and uses the same reasons for read-only tags.
using System;
using ExtraPotions.Threshold.Localization;

namespace ExtraPotions.Threshold.Config
{
    public enum SessionKind { SinglePlayer, Host, DedicatedServer, RemoteClient }

    /// <summary>What the server this client joined runs (Crucible authority gate).</summary>
    public enum ServerKind { Unknown, Vanilla, CrucibleWithoutThreshold, Threshold }

    /// <summary>Why an option is read-only or a cheat reads its safe value (C03 reasons).</summary>
    public enum BlockReason { None, SetByServer, VanillaServer, ProductDisabled, NotAdmin, AuthorityPending }

    public readonly struct SessionFacts
    {
        public SessionFacts(SessionKind kind, ServerKind server, bool admin)
        {
            Kind = kind;
            Server = server;
            Admin = admin;
        }

        public static SessionFacts SinglePlayer { get; } = new SessionFacts(SessionKind.SinglePlayer, ServerKind.Threshold, true);

        public SessionKind Kind { get; }
        public ServerKind Server { get; }
        public bool Admin { get; }

        /// <summary>This machine is the authority: single player, host or dedicated server.</summary>
        public bool IsAuthority => Kind != SessionKind.RemoteClient;
    }

    public static class SettingAccess
    {
        /// <summary>
        /// Whether this machine may change a setting of <paramref name="scope"/> now, and why not: on a Threshold server only
        /// the server value counts; admins amend it; a non-admin sees Set by the server, or Not admin for a cheat.
        /// </summary>
        public static BlockReason Decide(SettingScope scope, SessionFacts facts)
        {
            if (facts.IsAuthority || scope == SettingScope.Local) return BlockReason.None;
            switch (facts.Server)
            {
                case ServerKind.Unknown: return BlockReason.AuthorityPending;
                case ServerKind.Vanilla: return BlockReason.VanillaServer;
                case ServerKind.CrucibleWithoutThreshold: return BlockReason.ProductDisabled;
                default:
                    if (facts.Admin) return BlockReason.None;
                    return scope == SettingScope.Cheat ? BlockReason.NotAdmin : BlockReason.SetByServer;
            }
        }

        /// <summary>Cheats read their safe value on a remote client unless the server runs Threshold; never on the authority.</summary>
        public static bool CheatLocked(SessionFacts facts) => !facts.IsAuthority && facts.Server != ServerKind.Threshold;
    }
}
