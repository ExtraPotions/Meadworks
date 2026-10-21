// Thing - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: server-owned per-player admin permissions. A listed admin is not stored here; the check grants them every right.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using ExtraPotions.Thing.Moderation;

namespace ExtraPotions.Thing.Admin
{
    /// <summary>One routed admin tool. None means the action stays limited to listed admins and the local authority.</summary>
    [Flags]
    public enum AdminRight
    {
        None = 0,
        World = 1,
        Spawn = 2,
        Teleport = 4,
        Freeze = 8,
        Inventory = 16,
        Ward = 32,
        All = World | Spawn | Teleport | Freeze | Inventory | Ward,
    }

    public static class AdminPermissions
    {
        public const string CapabilityId = "meadworks.thing.admin_permission";
        public static readonly Version CapabilityVersion = new Version(1, 0);

        public static AdminRight For(ThingAction action)
        {
            switch (action)
            {
                case ThingAction.GlobalTime:
                case ThingAction.ReleaseTime:
                case ThingAction.ShowSeed:
                case ThingAction.TerrainReset:
                case ThingAction.RemoveDrops:
                    return AdminRight.World;
                case ThingAction.HexSpawn: return AdminRight.Spawn;
                case ThingAction.TeleportTo:
                case ThingAction.Bring: return AdminRight.Teleport;
                case ThingAction.FreezePlayer:
                case ThingAction.UnfreezePlayer: return AdminRight.Freeze;
                case ThingAction.InventoryView:
                case ThingAction.InventoryTake:
                case ThingAction.InventoryDelete: return AdminRight.Inventory;
                case ThingAction.WardOverride: return AdminRight.Ward;
                default: return AdminRight.None;
            }
        }

        public static bool Covers(AdminRight held, ThingAction action)
        {
            var need = For(action);
            return need != AdminRight.None && (held & need) == need;
        }

        public static bool TryBit(string name, out AdminRight bit)
        {
            switch ((name ?? "").Trim().ToLowerInvariant())
            {
                case "world": bit = AdminRight.World; return true;
                case "spawn": bit = AdminRight.Spawn; return true;
                case "teleport": bit = AdminRight.Teleport; return true;
                case "freeze": bit = AdminRight.Freeze; return true;
                case "inventory": bit = AdminRight.Inventory; return true;
                case "ward": bit = AdminRight.Ward; return true;
                default: bit = AdminRight.None; return false;
            }
        }

        public static string Format(AdminRight set)
        {
            var names = new List<string>();
            if ((set & AdminRight.World) != 0) names.Add("world");
            if ((set & AdminRight.Spawn) != 0) names.Add("spawn");
            if ((set & AdminRight.Teleport) != 0) names.Add("teleport");
            if ((set & AdminRight.Freeze) != 0) names.Add("freeze");
            if ((set & AdminRight.Inventory) != 0) names.Add("inventory");
            if ((set & AdminRight.Ward) != 0) names.Add("ward");
            return string.Join(",", names);
        }

        public static bool TryParseSet(string text, out AdminRight set)
        {
            set = AdminRight.None;
            if (string.IsNullOrWhiteSpace(text)) return true;
            foreach (var part in text.Split(','))
            {
                if (!TryBit(part, out var bit)) { set = AdminRight.None; return false; }
                set |= bit;
            }
            return true;
        }

        /// <summary>The line the server sends. <c>admin</c> means a listed admin or the local authority, not a stored row.</summary>
        public static string Wire(bool fullAdmin, AdminRight set) => fullAdmin ? "GRANTS\tadmin" : "GRANTS\t" + Format(set);

        public static bool TryWire(string line, out bool fullAdmin, out AdminRight set)
        {
            fullAdmin = false; set = AdminRight.None;
            if (line == null || !line.StartsWith("GRANTS\t", StringComparison.Ordinal)) return false;
            var body = line.Substring("GRANTS\t".Length);
            if (body == "admin") { fullAdmin = true; set = AdminRight.All; return true; }
            return TryParseSet(body, out set);
        }
    }

    /// <summary>Platform-id grants keyed by world id. Missing ids have no rights. The file is the server's copy.</summary>
    public sealed class PermissionTable
    {
        const string Header = "# Thing player permissions (server-owned; world id, platform id, rights)";
        readonly Dictionary<long, Dictionary<string, AdminRight>> worlds = new Dictionary<long, Dictionary<string, AdminRight>>();

        public AdminRight Get(long world, string platform)
        {
            if (world == 0 || !worlds.TryGetValue(world, out var rows)) return AdminRight.None;
            return rows.TryGetValue(platform ?? "", out var set) ? set : AdminRight.None;
        }

        public IReadOnlyList<string> Ids(long world)
        {
            if (world == 0 || !worlds.TryGetValue(world, out var rows)) return Array.Empty<string>();
            return rows.Keys.OrderBy(id => id, StringComparer.Ordinal).ToArray();
        }

        public bool Remember(long world, string platform)
        {
            if (!Key(world, platform)) return false;
            var rows = Rows(world);
            if (rows.ContainsKey(platform)) return false;
            rows[platform] = AdminRight.None;
            return true;
        }

        public bool Forget(long world, string platform)
        {
            if (world == 0 || string.IsNullOrEmpty(platform) || !worlds.TryGetValue(world, out var rows)) return false;
            return rows.Remove(platform);
        }

        public bool Change(long world, string platform, AdminRight bit, bool on)
        {
            if (!Key(world, platform) || bit == AdminRight.None || (bit & (bit - 1)) != 0 || (AdminRight.All & bit) == 0) return false;
            var rows = Rows(world);
            rows.TryGetValue(platform, out var current);
            var next = on ? current | bit : current & ~bit;
            if (next == current && rows.ContainsKey(platform)) return false;
            rows[platform] = next;
            return true;
        }

        public string Save()
        {
            var text = new StringBuilder(Header).Append('\n');
            foreach (var world in worlds.Keys.OrderBy(id => id))
                foreach (var row in worlds[world].OrderBy(pair => pair.Key, StringComparer.Ordinal))
                    text.Append(world.ToString(CultureInfo.InvariantCulture)).Append('\t').Append(row.Key).Append('\t').Append(AdminPermissions.Format(row.Value)).Append('\n');
            return text.ToString();
        }

        public static PermissionTable Load(string text, out int skipped)
        {
            var table = new PermissionTable();
            skipped = 0;
            foreach (var raw in (text ?? "").Split('\n'))
            {
                var line = raw.TrimEnd('\r');
                if (line.Trim().Length == 0 || line.StartsWith("#", StringComparison.Ordinal)) continue;
                var parts = line.Split('\t');
                if (parts.Length != 3 || !long.TryParse(parts[0], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var world) || world == 0
                    || !GameListIdentity.Valid(parts[1]) || !AdminPermissions.TryParseSet(parts[2], out var set))
                { skipped++; continue; }
                table.Rows(world)[parts[1]] = set;
            }
            return table;
        }

        static bool Key(long world, string platform) => world != 0 && GameListIdentity.Valid(platform);
        Dictionary<string, AdminRight> Rows(long world)
        {
            if (!worlds.TryGetValue(world, out var rows)) worlds[world] = rows = new Dictionary<string, AdminRight>(StringComparer.Ordinal);
            return rows;
        }
    }
}
