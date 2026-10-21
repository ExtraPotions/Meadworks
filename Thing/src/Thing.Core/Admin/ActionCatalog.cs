// Thing - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: the action inventory of design section 1.1 as one table. The world tools (TeleportTo, Bring, GlobalTime,
// ReleaseTime, TerrainReset, RemoveDrops, WardOverride, ShowSeed) follow Valheim_Admin_Tool 0.5.0's README, behaviour only.
using System;
using System.Collections.Generic;
using System.Linq;

namespace ExtraPotions.Thing.Admin
{
    /// <summary>Every Thing admin action (design 1.1 rows A, B, C and D). The order is the catalog order.</summary>
    public enum ThingAction
    {
        ListPlayers, Kick, Ban, TempBan, Unban, PermitAdd, PermitRemove, TeleportTo, Bring, Mute, Unmute, FreezePlayer, UnfreezePlayer, Warn, AuditView,
        AdminListView, AdminListEdit, Broadcast, MessageOfTheDay, Announcements, Shutdown, CancelShutdown, SaveWorld,
        GlobalTime, ReleaseTime, ResetDungeons, ResetResources, RegenerateNature, RegenerationPage, TerrainReset, RemoveDrops,
        WardOverride, ShowSeed, PoiService, RemoteConfigRead, RemoteConfigWrite, HexSpawn, PermitListView,
        GrantEdit, InventoryView, InventoryTake, InventoryDelete,
    }

    /// <summary>The admin window's pages (design 9).</summary>
    public enum AdminPage { People, ModerationLog, Messages, World, Regeneration, ServerConfig }

    /// <summary>The server setting that must be on for an action (design 4).</summary>
    public enum ActionGate { None, AdminListEditable, RemoteConfigEditing, WardOverrideAllowed }

    public sealed class ActionInfo
    {
        internal ActionInfo(ThingAction action, string verb, AdminPage page, bool targetsOthers, bool destructive, bool overlapsAdminTool, bool needsDelve, ActionGate gate)
        {
            Action = action;
            Verb = verb;
            Page = page;
            TargetsOthers = targetsOthers;
            Destructive = destructive;
            OverlapsAdminTool = overlapsAdminTool;
            NeedsDelve = needsDelve;
            Gate = gate;
        }

        public ThingAction Action { get; }

        /// <summary>Lowercase verb for the console, the relay and the audit log.</summary>
        public string Verb { get; }

        public AdminPage Page { get; }

        /// <summary>Acts on or speaks to other players; hidden in single player (design 3.1).</summary>
        public bool TargetsOthers { get; }

        /// <summary>Asks for confirmation while <c>Confirm Destructive Actions</c> is on (design 4.1).</summary>
        public bool Destructive { get; }

        /// <summary>Also offered by Valheim_Admin_Tool; hidden while it is loaded on the admin's client (Q2, design 10).</summary>
        public bool OverlapsAdminTool { get; }

        /// <summary>Executed by Delve (design C2, C3); refused with "needs Delve on the server" otherwise.</summary>
        public bool NeedsDelve { get; }

        public ActionGate Gate { get; }
    }

    public static class ActionCatalog
    {
        static ActionInfo I(ThingAction a, string verb, AdminPage page, bool others = false, bool destructive = false, bool vat = false, bool delve = false, ActionGate gate = ActionGate.None) =>
            new ActionInfo(a, verb, page, others, destructive, vat, delve, gate);

        public static IReadOnlyList<ActionInfo> All { get; } = Array.AsReadOnly(new[]
        {
            I(ThingAction.ListPlayers, "players", AdminPage.People),
            I(ThingAction.Kick, "kick", AdminPage.People, others: true, destructive: true),
            I(ThingAction.Ban, "ban", AdminPage.People, others: true, destructive: true),
            I(ThingAction.TempBan, "tempban", AdminPage.People, others: true, destructive: true),
            I(ThingAction.Unban, "unban", AdminPage.People, others: true),
            I(ThingAction.PermitAdd, "permit", AdminPage.People, others: true),
            I(ThingAction.PermitRemove, "unpermit", AdminPage.People, others: true),
            I(ThingAction.TeleportTo, "goto", AdminPage.People, others: true, vat: true),
            I(ThingAction.Bring, "bring", AdminPage.People, others: true, vat: true),
            I(ThingAction.Mute, "mute", AdminPage.People, others: true),
            I(ThingAction.Unmute, "unmute", AdminPage.People, others: true),
            I(ThingAction.FreezePlayer, "freeze", AdminPage.People, others: true),
            I(ThingAction.UnfreezePlayer, "unfreeze", AdminPage.People, others: true),
            I(ThingAction.Warn, "warn", AdminPage.People, others: true),
            I(ThingAction.AuditView, "audit", AdminPage.ModerationLog),
            I(ThingAction.AdminListView, "admins", AdminPage.People),
            I(ThingAction.AdminListEdit, "admin", AdminPage.People, gate: ActionGate.AdminListEditable),
            I(ThingAction.Broadcast, "say", AdminPage.Messages, others: true),
            I(ThingAction.MessageOfTheDay, "motd", AdminPage.Messages, others: true),
            I(ThingAction.Announcements, "announce", AdminPage.Messages, others: true),
            I(ThingAction.Shutdown, "shutdown", AdminPage.Messages, others: true),
            I(ThingAction.CancelShutdown, "cancelshutdown", AdminPage.Messages, others: true),
            I(ThingAction.SaveWorld, "save", AdminPage.Messages),
            I(ThingAction.GlobalTime, "time", AdminPage.World, vat: true),
            I(ThingAction.ReleaseTime, "resumetime", AdminPage.World, vat: true),
            I(ThingAction.ResetDungeons, "resetdungeons", AdminPage.Regeneration, delve: true),
            I(ThingAction.ResetResources, "resetresources", AdminPage.Regeneration, delve: true),
            I(ThingAction.RegenerateNature, "regenerate", AdminPage.Regeneration, delve: true),
            I(ThingAction.RegenerationPage, "regeneration", AdminPage.Regeneration, delve: true),
            I(ThingAction.TerrainReset, "terrain", AdminPage.World, destructive: true, vat: true),
            I(ThingAction.RemoveDrops, "drops", AdminPage.World, destructive: true, vat: true),
            I(ThingAction.WardOverride, "ward", AdminPage.World, vat: true, gate: ActionGate.WardOverrideAllowed),
            I(ThingAction.ShowSeed, "seed", AdminPage.World, vat: true),
            I(ThingAction.PoiService, "poi", AdminPage.World),
            I(ThingAction.RemoteConfigRead, "config", AdminPage.ServerConfig, gate: ActionGate.RemoteConfigEditing),
            I(ThingAction.RemoteConfigWrite, "set", AdminPage.ServerConfig, destructive: true, gate: ActionGate.RemoteConfigEditing),
            I(ThingAction.HexSpawn, "hex-spawn", AdminPage.ModerationLog),
            I(ThingAction.PermitListView, "permitted", AdminPage.People),
            I(ThingAction.GrantEdit, "grant", AdminPage.People),
            I(ThingAction.InventoryView, "inv-view", AdminPage.People, others: true),
            I(ThingAction.InventoryTake, "inv-take", AdminPage.People, others: true),
            I(ThingAction.InventoryDelete, "inv-delete", AdminPage.People, others: true),
        });

        static readonly Dictionary<ThingAction, ActionInfo> byAction = All.ToDictionary(i => i.Action);
        static readonly Dictionary<string, ActionInfo> byVerb = All.ToDictionary(i => i.Verb, StringComparer.Ordinal);

        public static ActionInfo Info(ThingAction action) =>
            byAction.TryGetValue(action, out var info) ? info : throw new ArgumentException("Unknown action.", nameof(action));

        /// <summary>The action of a verb (lowercase, exact); null when unknown.</summary>
        public static ActionInfo? FromVerb(string? verb) => verb != null && byVerb.TryGetValue(verb, out var info) ? info : null;

        /// <summary>Whether the admin window shows an action on this client (Q2: overlapping World tools hide while VAT is loaded).</summary>
        public static bool Visible(ThingAction action, bool adminToolLoaded) => !(adminToolLoaded && Info(action).OverlapsAdminTool);
    }
}
