// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: server-owned per-player grants for one world, stored beside the moderation ledger.
using System;
using System.Collections.Generic;
using System.IO;
using ExtraPotions.Meadworks.Crucible;
using ExtraPotions.Thing.Admin;
using ExtraPotions.Thing.Commands;
using ExtraPotions.Thing.Moderation;
using ExtraPotions.Thing.UI;

namespace ExtraPotions.Thing.Runtime
{
    internal static class ThingAccess
    {
        static PermissionTable table = new PermissionTable();
        static MeadworksLog log;
        static long world;
        static bool readable = true;

        internal static void Initialize(MeadworksLog logger) => log = logger;

        internal static void Bind(long worldId)
        {
            world = worldId;
            table = new PermissionTable();
            readable = true;
            if (worldId == 0) return;
            try
            {
                if (!File.Exists(ThingPaths.PermissionsFile)) return;
                table = PermissionTable.Load(File.ReadAllText(ThingPaths.PermissionsFile), out var skipped);
                if (skipped > 0) log?.Warning("Thing permissions: " + skipped + " unreadable lines skipped.");
            }
            catch (Exception error)
            {
                table = new PermissionTable();
                readable = false;
                log?.Warning("Thing permissions could not be read: " + error.GetType().Name + ": " + error.Message);
            }
        }

        internal static void Clear()
        {
            world = 0;
            table = new PermissionTable();
            readable = true;
        }

        internal static AdminRight Of(string platform) => table.Get(world, platform ?? "");

        internal static bool Has(string platform, AdminRight bit) => bit != AdminRight.None && (Of(platform) & bit) == bit;

        internal static bool Allows(string platform, string name) =>
            ThingServer.CurrentAuthority && AdminPermissions.TryBit(name, out var bit) && Has(platform, bit);

        internal static IEnumerable<ThingUiRow> Rows()
        {
            foreach (var id in table.Ids(world))
                yield return new ThingUiRow("grant", id, id, AdminPermissions.Format(table.Get(world, id)));
        }

        internal static IEnumerable<string> Edit(ParsedCommand command, Actor admin)
        {
            if (world == 0 || !readable) { yield return ThingText.Text("grant_unsaved"); yield break; }
            var id = command.Target ?? "";
            if (command.Text == "get") { yield return AdminPermissions.Wire(false, Of(id)); yield break; }
            var changed = false;
            if (command.Text == "add") changed = table.Remember(world, id);
            else if (command.Text == "remove") changed = table.Forget(world, id);
            else
            {
                var parts = (command.Text ?? "").Split(' ');
                if (parts.Length != 2 || !AdminPermissions.TryBit(parts[0], out var bit))
                { yield return ThingText.Text("grant_unsaved"); yield break; }
                changed = table.Change(world, id, bit, parts[1] == "on");
            }
            if (!changed)
            {
                yield return AdminPermissions.Wire(false, Of(id));
                yield return ThingText.Text("grant_unchanged");
                yield break;
            }
            if (!Save())
            {
                Bind(world);
                yield return ThingText.Text("grant_unsaved");
                yield break;
            }
            ThingServer.Audit(admin, ThingAction.GrantEdit, new Actor(id, ""), AuditOutcome.Done, command.Text);
            log?.Info("Thing permission " + command.Text + " for " + id + " by " + admin.Id + ".");
            yield return AdminPermissions.Wire(false, Of(id));
            yield return ThingText.Text("grant_saved");
        }

        static bool Save()
        {
            try
            {
                Directory.CreateDirectory(ThingPaths.DataFolder);
                var temp = ThingPaths.PermissionsFile + ".tmp";
                var text = table.Save();
                File.WriteAllText(temp, text);
                if (File.Exists(ThingPaths.PermissionsFile)) File.Replace(temp, ThingPaths.PermissionsFile, null);
                else File.Move(temp, ThingPaths.PermissionsFile);
                return File.ReadAllText(ThingPaths.PermissionsFile) == text;
            }
            catch (Exception error)
            {
                log?.Warning("Thing permissions write failed: " + error.GetType().Name + ": " + error.Message);
                return false;
            }
        }
    }
}
