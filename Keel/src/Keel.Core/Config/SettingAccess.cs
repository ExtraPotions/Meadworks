// Keel - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: the C01/C02/C03 rules of design sections 4 and 5 as one portable table. The plugin takes the live facts
// from Crucible (MeadworksAuthority) and uses the same reasons for read-only tags.
using System;
using ExtraPotions.Keel.Localization;

namespace ExtraPotions.Keel.Config
{
    public enum SessionKind { SinglePlayer, Host, DedicatedServer, RemoteClient }

    /// <summary>What the server this client joined runs (Crucible authority gate).</summary>
    public enum ServerKind { Unknown, Vanilla, CrucibleWithoutKeel, Keel }

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

        public static SessionFacts SinglePlayer { get; } = new SessionFacts(SessionKind.SinglePlayer, ServerKind.Keel, true);

        public SessionKind Kind { get; }
        public ServerKind Server { get; }
        public bool Admin { get; }

        /// <summary>This machine is the authority: single player, host or dedicated server.</summary>
        public bool IsAuthority => Kind != SessionKind.RemoteClient;
    }

    public static class SettingAccess
    {
        /// <summary>
        /// Whether this machine may change a setting of <paramref name="scope"/> now, and why not (design 5): on a Keel
        /// server only the server value counts; admins amend it; a non-admin sees Set by the server, or Not admin for a cheat.
        /// </summary>
        public static BlockReason Decide(SettingScope scope, SessionFacts facts)
        {
            if (facts.IsAuthority || scope == SettingScope.Local) return BlockReason.None;
            switch (facts.Server)
            {
                case ServerKind.Unknown: return BlockReason.AuthorityPending;
                case ServerKind.Vanilla: return BlockReason.VanillaServer;
                case ServerKind.CrucibleWithoutKeel: return BlockReason.ProductDisabled;
                default:
                    if (facts.Admin) return BlockReason.None;
                    return scope == SettingScope.Cheat ? BlockReason.NotAdmin : BlockReason.SetByServer;
            }
        }

        /// <summary>
        /// Whether cheats read their safe value: never on the authority; on a remote client while authority is pending, on a
        /// vanilla server or a Crucible server without Keel. On a Keel server the server's value counts.
        /// </summary>
        public static bool CheatLocked(SessionFacts facts) => !facts.IsAuthority && facts.Server != ServerKind.Keel;

    }
}
