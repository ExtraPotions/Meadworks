// Cairn - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: the C01/C02/C03 rules of design sections 3 and 5 for a client-optional product, and the user's Q1
// hold, as one portable table. The plugin takes the live facts from Crucible (MeadworksAuthority).
using System;
using ExtraPotions.Cairn.Localization;

namespace ExtraPotions.Cairn.Config
{
    public enum SessionKind { SinglePlayer, Host, DedicatedServer, RemoteClient }

    /// <summary>What the server this client joined runs (Crucible authority gate and peer-class handshake).</summary>
    public enum ServerKind { Unknown, Vanilla, CrucibleWithoutCairn, Cairn }

    /// <summary>Why an option is read-only or held (C03 reasons).</summary>
    public enum BlockReason { None, SetByServer, VanillaServer, ProductDisabled, NotAdmin, AuthorityPending }

    public readonly struct SessionFacts
    {
        public SessionFacts(SessionKind kind, ServerKind server, bool admin)
        {
            Kind = kind;
            Server = server;
            Admin = admin;
        }

        public static SessionFacts SinglePlayer { get; } = new SessionFacts(SessionKind.SinglePlayer, ServerKind.Cairn, true);

        public SessionKind Kind { get; }
        public ServerKind Server { get; }
        public bool Admin { get; }

        /// <summary>This machine is the authority: single player, host or dedicated server.</summary>
        public bool IsAuthority => Kind != SessionKind.RemoteClient;
    }

    public static class SettingAccess
    {
        /// <summary>
        /// Whether this machine may change a setting of <paramref name="scope"/> now, and why not. Cairn is client-optional
        /// (C02): on a server without Cairn the player keeps their own server values; on a Cairn server only the server's
        /// value counts and admins amend it. Cheats are locked unless Cairn runs on the server (Cairn binds none today).
        /// </summary>
        public static BlockReason Decide(SettingScope scope, SessionFacts facts)
        {
            if (facts.IsAuthority || scope == SettingScope.Local) return BlockReason.None;
            var cheat = scope == SettingScope.Cheat;
            switch (facts.Server)
            {
                case ServerKind.Unknown: return BlockReason.AuthorityPending;
                case ServerKind.Vanilla: return cheat ? BlockReason.VanillaServer : BlockReason.None;
                case ServerKind.CrucibleWithoutCairn: return cheat ? BlockReason.ProductDisabled : BlockReason.None;
                default:
                    if (facts.Admin) return BlockReason.None;
                    return cheat ? BlockReason.NotAdmin : BlockReason.SetByServer;
            }
        }
    }

    /// <summary>
    /// User decision Q1 (2026-10-07): on a server that does not run Cairn, and while authority is pending, gate-like
    /// visibility (Mistlands, particle and cave mist) stays on and the radius is held at Pathfinder's defaults.
    /// </summary>
    public static class GatePolicy
    {
        public const float HeldLandRadius = 200f;
        public const float HeldSeaRadius = 300f;
        public const float HeldMaximumRadius = 2000f;

        /// <summary>Gate-like parts are held: a remote client whose server does not (yet) vouch for Cairn.</summary>
        public static bool Holds(SessionFacts facts) => !facts.IsAuthority && facts.Server != ServerKind.Cairn;

        /// <summary>The reason a held part shows (C03); None when nothing is held.</summary>
        public static BlockReason HeldReason(SessionFacts facts)
        {
            if (!Holds(facts)) return BlockReason.None;
            switch (facts.Server)
            {
                case ServerKind.Vanilla: return BlockReason.VanillaServer;
                case ServerKind.CrucibleWithoutCairn: return BlockReason.ProductDisabled;
                default: return BlockReason.AuthorityPending;
            }
        }
    }
}
