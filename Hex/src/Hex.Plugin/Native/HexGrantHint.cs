// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: a display hint for Thing's per-player grants. The server still decides.
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ExtraPotions.Meadworks.Crucible;
using UnityEngine;
using CruciblePlugin = ExtraPotions.Meadworks.Crucible.Plugin;

namespace ExtraPotions.Hex.Runtime.Native
{
    internal static class AdminPermissionLink
    {
        internal const string Id = "meadworks.thing.admin_permission";
        internal static readonly System.Version Version = new System.Version(1, 0);

        internal static bool Has(string platform, string name)
        {
            if (string.IsNullOrWhiteSpace(platform) || string.IsNullOrWhiteSpace(name) || !CruciblePlugin.IsReady) return false;
            try
            {
                if (!CruciblePlugin.Services.Capabilities.TryResolve<Func<string, string, bool>>(Id, Version, out var probe) || probe == null) return false;
                return probe(platform, name);
            }
            catch { return false; }
        }
    }

    internal static class HexGrantHint
    {
        internal static bool World, Spawn;
        static float next;
        static long generation = -1;
        static Task<OperationResult<IReadOnlyList<string>>> pending;

        internal static bool WorldAllowed()
        {
            var decision = MeadworksMischief.Decide(Plugin.Guid, MischiefCategory.World);
            // The client only asks. The server still requires a listed admin or the world grant.
            return HexToolAccess.Allows(false, World, decision.Allowed, decision.Reason == ConfigBlockReason.NotAdmin, false);
        }

        internal static bool SpawnAllowed()
        {
            var decision = MeadworksMischief.Decide(Plugin.Guid, MischiefCategory.Economy);
            return HexToolAccess.Allows(false, Spawn, decision.Allowed, decision.Reason == ConfigBlockReason.NotAdmin, false);
        }

        internal static void Tick()
        {
            if (!Plugin.Ready || !CruciblePlugin.IsReady)
            {
                World = Spawn = false; pending = null; return;
            }
            var session = CruciblePlugin.Session;
            if (generation != session.Generation) { generation = session.Generation; World = Spawn = false; next = 0f; pending = null; }
            if (session.IsAuthority || !session.AuthorityReady) return;
            if (pending != null && !pending.IsCompleted) return;
            if (pending != null) { Read(pending); pending = null; }
            if (Time.unscaledTime < next) return;
            next = Time.unscaledTime + 10f;
            try
            {
                pending = CruciblePlugin.RequestClient.InvokeAsync<IReadOnlyList<string>, IReadOnlyList<string>>(
                    "meadworks.thing.admin_command", new[] { "grant", "self" }, CancellationToken.None);
            }
            catch { pending = null; World = Spawn = false; }
        }

        static void Read(Task<OperationResult<IReadOnlyList<string>>> task)
        {
            World = Spawn = false;
            if (task.Status != TaskStatus.RanToCompletion) return;
            var result = task.Result;
            if (result.State != OperationState.Succeeded || result.Reply == null) return;
            foreach (var line in result.Reply)
            {
                if (line == null || !line.StartsWith("GRANTS\t", StringComparison.Ordinal)) continue;
                var body = line.Substring("GRANTS\t".Length);
                if (body == "admin") { World = Spawn = true; return; }
                World = body.IndexOf("world", StringComparison.Ordinal) >= 0;
                Spawn = body.IndexOf("spawn", StringComparison.Ordinal) >= 0;
                return;
            }
        }
    }
}
