// Thing - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: the server-side admin decision of design 3.1, 5 and E1. Valheim_Admin_Tool's README states the same
// rule (admin rights from the connection's platform ID against the server's adminlist.txt; a display name grants nothing);
// behaviour only.
using System;

namespace ExtraPotions.Thing.Admin
{
    /// <summary>What this process is in the session.</summary>
    public enum ThingRole { SinglePlayer, Host, DedicatedServer }

    /// <summary>The server's answer to one request (C03 reasons for the refusals).</summary>
    public enum Verdict { Allowed, AllowedOwnPiecesOnly, NotAdmin, NoOtherPlayers, NeedsDelve, DisabledByServer, ProductOff, NeedsRelay }

    /// <summary>
    /// The facts the server takes for one request. <paramref name="fromLocalAuthority"/> is true for the single player, the
    /// host's own player and the server console. <paramref name="requesterIsAdmin"/> comes only from the connection's
    /// platform ID checked against the server's live <c>adminlist.txt</c> (the game's <c>ZNet.IsAdmin</c>); there is no name input.
    /// </summary>
    public readonly struct RequestFacts
    {
        public RequestFacts(ThingRole role, bool fromLocalAuthority, bool requesterIsAdmin, bool delvePresent, bool mischiefGranted = false, AdminRight granted = AdminRight.None)
        {
            Role = role;
            FromLocalAuthority = fromLocalAuthority;
            RequesterIsAdmin = requesterIsAdmin;
            DelvePresent = delvePresent;
            MischiefGranted = mischiefGranted;
            Granted = granted;
        }

        public ThingRole Role { get; }
        public bool FromLocalAuthority { get; }
        public bool RequesterIsAdmin { get; }
        public bool DelvePresent { get; }

        /// <summary>Hex's Mischief gate granted the requester (only consulted for the POI service, design C8).</summary>
        public bool MischiefGranted { get; }

        /// <summary>Rights stored for this platform id. The local authority and listed admins do not need a row.</summary>
        public AdminRight Granted { get; }
    }

    /// <summary>The ServerAuthoritative switches the decision reads (design 4.1 and 4.4 and 4.5).</summary>
    public readonly struct ServerRules
    {
        public ServerRules(bool adminListEditable, bool remoteConfigEditing, bool wardOverrideAllowed, bool terrainNonAdminOwnPieces, bool removeDropsForEveryone)
        {
            AdminListEditable = adminListEditable;
            RemoteConfigEditing = remoteConfigEditing;
            WardOverrideAllowed = wardOverrideAllowed;
            TerrainNonAdminOwnPieces = terrainNonAdminOwnPieces;
            RemoveDropsForEveryone = removeDropsForEveryone;
        }

        /// <summary>The design defaults: admin list read-only (Q1), remote editing on, ward override on, terrain own pieces on, drops admin only.</summary>
        public static ServerRules Defaults { get; } = new ServerRules(false, true, true, true, false);

        public bool AdminListEditable { get; }
        public bool RemoteConfigEditing { get; }
        public bool WardOverrideAllowed { get; }
        public bool TerrainNonAdminOwnPieces { get; }
        public bool RemoveDropsForEveryone { get; }

        public ServerRules With(bool? adminListEditable = null, bool? remoteConfigEditing = null, bool? wardOverrideAllowed = null,
            bool? terrainNonAdminOwnPieces = null, bool? removeDropsForEveryone = null) =>
            new ServerRules(adminListEditable ?? AdminListEditable, remoteConfigEditing ?? RemoteConfigEditing, wardOverrideAllowed ?? WardOverrideAllowed,
                terrainNonAdminOwnPieces ?? TerrainNonAdminOwnPieces, removeDropsForEveryone ?? RemoveDropsForEveryone);

        public bool Allows(ActionGate gate)
        {
            switch (gate)
            {
                case ActionGate.None: return true;
                case ActionGate.AdminListEditable: return AdminListEditable;
                case ActionGate.RemoteConfigEditing: return RemoteConfigEditing;
                case ActionGate.WardOverrideAllowed: return WardOverrideAllowed;
                default: return false;
            }
        }
    }

    public static class AdminCheck
    {
        /// <summary>
        /// The server's decision, in order: actions on other players do not exist in single player; Delve actions need Delve;
        /// a server switch can turn an action off for everyone; the local authority and listed admins may act; a stored
        /// grant may allow that one tool; other non-admins only where a server switch opens a world tool to them
        /// (terrain around their own pieces, remove drops) or Hex's gate granted the POI service.
        /// </summary>
        public static Verdict Decide(ThingAction action, RequestFacts facts, ServerRules rules)
        {
            var info = ActionCatalog.Info(action);
            if (info.TargetsOthers && facts.Role == ThingRole.SinglePlayer) return Verdict.NoOtherPlayers;
            if (info.NeedsDelve && !facts.DelvePresent) return Verdict.NeedsDelve;
            if (!rules.Allows(info.Gate) && !(facts.FromLocalAuthority &&
                (action == ThingAction.RemoteConfigRead || action == ThingAction.RemoteConfigWrite))) return Verdict.DisabledByServer;
            if (facts.FromLocalAuthority || facts.RequesterIsAdmin) return Verdict.Allowed;
            if (AdminPermissions.Covers(facts.Granted, action)) return Verdict.Allowed;
            switch (action)
            {
                case ThingAction.TerrainReset when rules.TerrainNonAdminOwnPieces: return Verdict.AllowedOwnPiecesOnly;
                case ThingAction.RemoveDrops when rules.RemoveDropsForEveryone: return Verdict.Allowed;
                case ThingAction.PoiService when facts.MischiefGranted: return Verdict.Allowed;
                default: return Verdict.NotAdmin;
            }
        }

        public static bool Permits(Verdict verdict) => verdict == Verdict.Allowed || verdict == Verdict.AllowedOwnPiecesOnly;

        /// <summary>The localization key of a refusal.</summary>
        public static string TextKey(Verdict verdict)
        {
            switch (verdict)
            {
                case Verdict.NotAdmin: return "refused_not_admin";
                case Verdict.NoOtherPlayers: return "refused_no_other_players";
                case Verdict.NeedsDelve: return "refused_needs_delve";
                case Verdict.DisabledByServer: return "refused_disabled_by_server";
                case Verdict.ProductOff: return "refused_product_off";
                case Verdict.NeedsRelay: return "refused_needs_relay";
                default: throw new ArgumentException("Allowed verdicts have no refusal text.", nameof(verdict));
            }
        }
    }
}
