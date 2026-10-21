// Inscribe - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: the C01/C02/C03 rules of design section 5 as one portable table, same shape as Wildblood.Core.
// The plugin takes the live facts from Crucible (MeadworksAuthority) and uses the same reasons for read-only tags and
// console verb refusals.
using System;
using ExtraPotions.Inscribe.Localization;

namespace ExtraPotions.Inscribe.Config
{
    public enum SessionKind { SinglePlayer, Host, DedicatedServer, RemoteClient }

    /// <summary>What the server this client joined runs (Crucible authority gate).</summary>
    public enum ServerKind { Unknown, Vanilla, CrucibleWithoutInscribe, Inscribe }

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

        public static SessionFacts SinglePlayer { get; } = new SessionFacts(SessionKind.SinglePlayer, ServerKind.Inscribe, true);

        public SessionKind Kind { get; }
        public ServerKind Server { get; }
        public bool Admin { get; }

        /// <summary>This machine is the authority: single player, host or dedicated server.</summary>
        public bool IsAuthority => Kind != SessionKind.RemoteClient;
    }

    public static class SettingAccess
    {
        /// <summary>
        /// Whether this machine may change a setting of <paramref name="scope"/> now, and why not. A remote admin amends
        /// ServerAuthoritative settings and cheats (C01 amendments); a remote non-admin sees "Set by the server" for
        /// ServerAuthoritative settings and "Only an admin" for cheats (design 5).
        /// </summary>
        public static BlockReason Decide(SettingScope scope, SessionFacts facts)
        {
            if (facts.IsAuthority || scope == SettingScope.Local) return BlockReason.None;
            switch (facts.Server)
            {
                case ServerKind.Unknown: return BlockReason.AuthorityPending;
                case ServerKind.Vanilla: return BlockReason.VanillaServer;
                case ServerKind.CrucibleWithoutInscribe: return BlockReason.ProductDisabled;
                default:
                    if (facts.Admin) return BlockReason.None;
                    return scope == SettingScope.Cheat ? BlockReason.NotAdmin : BlockReason.SetByServer;
            }
        }

        /// <summary>
        /// Whether cheats read their safe value: never on the authority; on a remote client while authority is pending,
        /// on a vanilla server or a Crucible server without Inscribe. On an Inscribe server the server's value counts.
        /// </summary>
        public static bool CheatLocked(SessionFacts facts) => !facts.IsAuthority && facts.Server != ServerKind.Inscribe;

    }

    /// <summary>A blocked cheat evaluates to its safe value; the cheat's tuning numbers act only through it (design 4.6, 5).</summary>
    public static class InscribeCheats
    {
        public static T Effective<T>(InscribeSetting<T> setting, bool locked, T value)
        {
            if (setting == null) throw new ArgumentNullException(nameof(setting));
            if (setting.Scope != SettingScope.Cheat) throw new ArgumentException(setting.Id + " is not a cheat.", nameof(setting));
            return locked ? setting.Safe : setting.Normalize(value);
        }
    }
}
