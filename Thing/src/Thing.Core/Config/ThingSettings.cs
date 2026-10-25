// Thing - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: design section 4 as one table, plus the converter's optional action hotkeys (design 10). The radius
// limits of World Administration follow Valheim_Admin_Tool 0.5.0's README (terrain 1 to 50 m, drops 50 m); behaviour only.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ExtraPotions.Thing.Admin;

namespace ExtraPotions.Thing.Config
{
    /// <summary>Which server files the remote editor may open (design 4.5, Q3).</summary>
    public enum RemoteConfigScope { MeadworksOnly, AllowList, AllConfigFiles }

    public static class ThingSettings
    {
        const SettingScope Local = SettingScope.Local;
        const SettingScope Server = SettingScope.ServerAuthoritative;

        /// <summary>Message of the day length cap (design 4.3).</summary>
        public const int MotdMaxLength = 500;

        /// <summary>The announcements file in the data folder (design 4.3); its entries are <c>minutes|text</c>.</summary>
        public const string AnnouncementsFile = "Thing.Announcements.yml";

        public static class General
        {
            public const string Section = "General";
            public static readonly ThingSetting<string> AdminWindowKey = ThingSetting.Shortcut(Section, "Admin Window Key", "", Local,
                "Opens and closes the Thing admin window (admins only; others see a short hint). Empty: none.");
            public static readonly ThingSetting<bool> ConfirmDestructive = ThingSetting.Toggle(Section, "Confirm Destructive Actions", true, Local,
                "Asks before kick, ban, terrain reset, remove drops and remote configuration writes.");
            public static readonly ThingSetting<bool> AdminListEditable = ThingSetting.Toggle(Section, "Admin List Editable In Game", false, Server,
                "Lets existing admins add and remove admins in game. Off: adminlist.txt is changed only on the server.");
            public static readonly ThingSetting<bool> AllowSubAdminDelegation = ThingSetting.Toggle(Section, "Allow Sub-Admin Delegation", true, Server,
                "Allows the server host or current admin to delegate all non-destructive permissions to online players as sub-admin.");
            public static readonly ThingSetting<int> AuditLogDays = ThingSetting.Integer(Section, "Audit Log Days", 30, 1, 365, Server,
                "Days the audit log keeps its daily files.");
            public static readonly ThingSetting<bool> ShowPlatformIds = ThingSetting.Toggle(Section, "Show Platform IDs", true, Server,
                "Shows platform IDs in the player list (to admins only either way).");
            public static readonly ThingSetting<string> LockedPlayerSettings = ThingSetting.Text(Section, "Locked Player Settings", "", Server,
                "Comma list of Local Thing settings (Section.Key) every player uses with the server's value.", NormalizeList);
        }

        public static class Moderation
        {
            public const string Section = "Moderation";
            public static readonly ThingSetting<bool> KickReasonRequired = ThingSetting.Toggle(Section, "Kick Reason Required", false, Server,
                "Kicks, bans and warnings need a reason.");
            public static readonly ThingSetting<int> TempBanDefaultHours = ThingSetting.Integer(Section, "Temp Ban Default Hours", 24, 1, 8760, Server,
                "Length of a temporary ban when the admin gives none.");
            public static readonly ThingSetting<int> MuteDefaultMinutes = ThingSetting.Integer(Section, "Mute Default Minutes", 30, 1, 10080, Server,
                "Length of a mute when the admin gives none.");
            public static readonly ThingSetting<bool> TeleportNeedsTargetConfirmation = ThingSetting.Toggle(Section, "Teleport Requires Confirmation By Target", false, Server,
                "A player brought to an admin must accept first.");
        }

        public static class Communication
        {
            public const string Section = "Communication";
            public static readonly ThingSetting<string> MessageOfTheDay = ThingSetting.Text(Section, "Message Of The Day", "", Server,
                "Shown to each player on joining; empty for none (up to 500 characters, sent as written).", NormalizeMotd);
            public static readonly ThingSetting<string> ShutdownWarnings = ThingSetting.Text(Section, "Shutdown Warning Minutes", "10,5,1", Server,
                "Minutes before a shutdown at which everyone is warned (comma list, 1 to 120).", NormalizeWarnings);
        }

        public static class World
        {
            public const string Section = "World Administration";
            public static readonly ThingSetting<int> TerrainMaxRadius = ThingSetting.Integer(Section, "Terrain Reset Max Radius", 50, 1, 50, Server,
                "Largest terrain reset radius in meters.");
            public static readonly ThingSetting<bool> TerrainNonAdminOwnPieces = ThingSetting.Toggle(Section, "Terrain Reset Non-Admin Own Pieces", true, Server,
                "Non-admins may reset terrain around their own pieces only.");
            public static readonly ThingSetting<int> RemoveDropsRadius = ThingSetting.Integer(Section, "Remove Drops Radius", 50, 5, 100, Server,
                "Radius in meters of remove drops.");
            public static readonly ThingSetting<bool> RemoveDropsForEveryone = ThingSetting.Toggle(Section, "Remove Drops For Everyone", false, Server,
                "Every player may remove loose drops around themselves. Off: admins only.");
            public static readonly ThingSetting<bool> GlobalTimeReleaseOnLeave = ThingSetting.Toggle(Section, "Global Time Release On Leave", true, Server,
                "A frozen global time is released when the admin who set it leaves or loses admin rights.");
            public static readonly ThingSetting<bool> WardOverrideAllowed = ThingSetting.Toggle(Section, "Ward Override Allowed", true, Server,
                "Confirmed admins may bypass ward protection for their own character for the session.");
        }

        public static class Remote
        {
            public const string Section = "Remote Configuration";
            public static readonly ThingSetting<bool> Editing = ThingSetting.Toggle(Section, "Remote Config Editing", true, Server,
                "Admins may read and edit server configuration from inside the game.");
            public static readonly ThingSetting<RemoteConfigScope> Scope = ThingSetting.Choice(Section, "Remote Config Scope", RemoteConfigScope.MeadworksOnly, Server,
                "Files the remote editor may open: Meadworks files only, those plus the allow list, or every config file.");
            public static readonly ThingSetting<string> AllowList = ThingSetting.Text(Section, "Remote Config Allow List", "", Server,
                "Comma list of other file names in BepInEx/config the remote editor may open.", NormalizeFileList);
            public static readonly ThingSetting<int> BackupCount = ThingSetting.Integer(Section, "Remote Backup Count", 10, 0, 100, Server,
                "Backups kept per file before remote writes.");
        }

        public static class Hotkeys
        {
            public const string Section = "Hotkeys";
            public static readonly ThingSetting<string> WardOverride = ThingSetting.Shortcut(Section, "Ward Override Key", "", Local,
                "Optional key that turns the ward override on or off. Admins only.");
            public static readonly ThingSetting<string> RemoveDrops = ThingSetting.Shortcut(Section, "Remove Drops Key", "", Local,
                "Optional key that previews the dropped items within Remove Drops Radius around you; run thing drops confirm to remove them. Admins only.");
        }

        public static IReadOnlyList<ThingSetting> All { get; } = Array.AsReadOnly(new ThingSetting[]
        {
            General.AdminWindowKey, General.ConfirmDestructive, General.AdminListEditable, General.AllowSubAdminDelegation, General.AuditLogDays,
            General.ShowPlatformIds, General.LockedPlayerSettings,
            Moderation.KickReasonRequired, Moderation.TempBanDefaultHours, Moderation.MuteDefaultMinutes, Moderation.TeleportNeedsTargetConfirmation,
            Communication.MessageOfTheDay, Communication.ShutdownWarnings,
            World.TerrainMaxRadius, World.TerrainNonAdminOwnPieces, World.RemoveDropsRadius, World.RemoveDropsForEveryone,
            World.GlobalTimeReleaseOnLeave, World.WardOverrideAllowed,
            Remote.Editing, Remote.Scope, Remote.AllowList, Remote.BackupCount,
            Hotkeys.WardOverride, Hotkeys.RemoveDrops,
        });

        public static ThingSetting? Find(string? id)
        {
            if (id == null) return null;
            var trimmed = id.Trim();
            return All.FirstOrDefault(s => string.Equals(s.Id, trimmed, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>The admin decision's server switches from current values.</summary>
        public static ServerRules Rules(bool adminListEditable, bool remoteConfigEditing, bool wardOverrideAllowed, bool terrainNonAdminOwnPieces, bool removeDropsForEveryone) =>
            new ServerRules(adminListEditable, remoteConfigEditing, wardOverrideAllowed, terrainNonAdminOwnPieces, removeDropsForEveryone);

        static string NormalizeMotd(string text)
        {
            var t = (text ?? "").Trim();
            return t.Length > MotdMaxLength ? t.Substring(0, MotdMaxLength) : t;
        }

        /// <summary>Comma list of whole minutes 1 to 120, descending, without duplicates; invalid parts are dropped.</summary>
        public static string NormalizeWarnings(string text)
        {
            var minutes = new SortedSet<int>(Comparer<int>.Create((a, b) => b.CompareTo(a)));
            foreach (var part in (text ?? "").Split(','))
                if (int.TryParse(part.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var m) && m >= 1 && m <= 120) minutes.Add(m);
            return string.Join(",", minutes.Select(m => m.ToString(CultureInfo.InvariantCulture)));
        }

        /// <summary>Trimmed comma list without empty parts or case-insensitive duplicates (first spelling wins).</summary>
        public static string NormalizeList(string text)
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            return string.Join(",", (text ?? "").Split(',').Select(p => p.Trim()).Where(p => p.Length > 0 && seen.Add(p)));
        }

        static string NormalizeFileList(string text) => NormalizeList(text);
    }
}
