// Delve - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: the meadworks.delve.admin capability (Crucible.Contracts DelveAdminContract). The admin pages and
// commands of another product (Thing) reach Delve only through it, never through Delve's assemblies (user decision
// 2026-10-12 22:41). Delve keeps its own authority checks; the caller keeps its admin checks, its texts and its audit.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using BepInEx.Configuration;
using ExtraPotions.Delve.Config;
using ExtraPotions.Delve.Engine;
using ExtraPotions.Delve.Surface;
using ExtraPotions.Meadworks.Contracts;
using ExtraPotions.Meadworks.Crucible;
using CruciblePlugin = ExtraPotions.Meadworks.Crucible.Plugin;

namespace ExtraPotions.Delve.Runtime
{
    /// <summary>
    /// Runs <see cref="DelveAdminContract"/> operations on the server. Every reply line is tab-separated raw data (no
    /// localized text, fields never contain a tab or line break); booleans are 1/0, numbers invariant round-trip. An
    /// operation Delve cannot run now (not the server, engine stopped, bad arguments, a vanished caller) is unhandled:
    /// (false, no lines). Arguments are positional, followed by optional <c>key=value</c> options:
    /// <list type="bullet">
    /// <item><c>status</c>: line 1 <c>version	&lt;64 hex&gt;</c>, the SHA-256 of Delve's config file and its dry-run values (a
    /// stale-edit check for <c>dry_run</c>); then per kind, in kind order, <c>kind	Kind	configuredDryRun	checked	tracked
    /// waiting	deferred	acted	blocked	delay	clock	effectiveDryRun	active	note</c> (<c>checked</c> 0 leaves the counts 0).</item>
    /// <item><c>actions</c>: the last actions, newest first, one per line.</item>
    /// <item><c>dry_run</c> Kind on|off [version=hex]: writes that kind's Dry Run; one line <c>saved</c>, <c>stale</c>,
    /// <c>failed</c> (nothing written) or <c>unknown</c> (written, not confirmed).</item>
    /// <item><c>request</c> Kind... [caller=uid]: asks for those kinds at the next check, with the Regenerate cheat
    /// verdict for the caller (a peer's uid, or the local authority without it); one line
    /// <c>requested	verdict	Kind...</c> listing the kinds that are on.</item>
    /// <item><c>sites</c> x z [from=n]: known dungeon sites in kind and id order,
    /// <c>site	Kind	id	generator	requested	due	remaining	clock	distance	dryRun</c> (remaining empty when unknown,
    /// distance in meters from x, z).</item>
    /// <item><c>nature</c> x z [from=n]: known nature resources in kind and id order,
    /// <c>site	Kind	id	prefab	requested	due	remaining	clock	position	dryRun</c>; x, z are not used.</item>
    /// <item><c>nearest</c> x z [caller=uid platform=id]: queues the FullReset site nearest x, z for one guarded
    /// evaluation, re-checked when it runs (the server, session, Delve, the cheat verdict and, for a peer, that it is still
    /// the same connected admin with the same character); one line <c>queued	generator	id</c>, <c>refused	id</c>,
    /// <c>none</c> or <c>position_unknown</c>.</item>
    /// <item><c>surface_nature</c> / <c>surface_resources</c> x y z name [caller=uid]: the surface reset around the
    /// point; Delve's own reply lines (English, as its console command answers). Without a caller the local console also
    /// gets the result.</item>
    /// </list>
    /// <c>from=n</c> skips the first n lines, so a caller reads more than <see cref="DelveAdminContract.MaximumLines"/>.
    /// </summary>
    public static class DelveAdmin
    {
        private static readonly string[] OptionKeys = { "from", "caller", "platform", "version" };
        private static readonly Tuple<bool, IReadOnlyList<string>> Unhandled = Tuple.Create(false, (IReadOnlyList<string>)Array.Empty<string>());

        public static void Publish(ProductRegistration registration) =>
            registration.AddCapability<Func<string, IReadOnlyList<string>, Tuple<bool, IReadOnlyList<string>>>>(
                DelveAdminContract.Id, DelveAdminContract.ContractVersion, Run);

        public static Tuple<bool, IReadOnlyList<string>> Run(string operation, IReadOnlyList<string> arguments)
        {
            var engine = Plugin.Engine; var settings = Plugin.Settings; var net = ZNet.instance;
            if (operation == null || arguments == null || arguments.Any(a => a == null) || !Plugin.Ready || engine == null || settings == null
                || !engine.Running || net == null || !net.IsServer()) return Unhandled;
            // Options are the known key=value words; anything else (a player name with '=' too) is positional.
            var options = new Dictionary<string, string>(StringComparer.Ordinal);
            var positional = new List<string>();
            foreach (var a in arguments)
            {
                int eq = a.IndexOf('=');
                if (eq > 0 && Array.IndexOf(OptionKeys, a.Substring(0, eq)) >= 0) options[a.Substring(0, eq)] = a.Substring(eq + 1);
                else positional.Add(a);
            }
            int from = options.TryGetValue("from", out var f) && int.TryParse(f, NumberStyles.None, CultureInfo.InvariantCulture, out var n) ? n : 0;
            ZNetPeer? peer = null;
            if (options.TryGetValue("caller", out var c))
            {
                if (!long.TryParse(c, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var uid) || (peer = net.GetPeer(uid)) == null) return Unhandled;
            }
            switch (operation)
            {
                case DelveAdminContract.Status: return Done(Status(engine, settings));
                case DelveAdminContract.Actions: return Done(engine.Status.LastActions.Select(Clean));
                case DelveAdminContract.DryRun:
                    if (positional.Count != 2 || !TryKind(positional[0], out var dryKind) || positional[1] != "on" && positional[1] != "off") return Unhandled;
                    return Done(new[] { WriteDryRun(settings, dryKind, positional[1] == "on", options.TryGetValue("version", out var v) ? v : null) });
                case DelveAdminContract.Request:
                    var kinds = new List<DelveKind>();
                    foreach (var text in positional) { if (!TryKind(text, out var k)) return Unhandled; kinds.Add(k); }
                    if (kinds.Count == 0) return Unhandled;
                    var verdict = settings.RegenerateFor(true, Locked(peer));
                    BestiaryClaims.ClearIf(kinds, settings.Get(DelveSettings.General.DryRunAll));
                    var requested = engine.Request(kinds.Where(engine.Active), verdict == RegenerateVerdict.Allowed);
                    return Done(new[] { Line(new[] { "requested", verdict.ToString() }.Concat(requested.Select(k => k.ToString()))) });
                case DelveAdminContract.Sites:
                    if (!TryXz(positional, out var x, out var z)) return Unhandled;
                    return Done(engine.DungeonSites().Select(s => Line("site", s.Kind.ToString(), s.Id, s.Generator, Bit(s.Requested || s.EvaluationPending), Bit(s.Due),
                        s.Remaining.HasValue ? R(s.Remaining.Value) : "", s.Clock.ToString(),
                        R(Math.Sqrt(Math.Pow((double)s.Entrance.X - x, 2) + Math.Pow((double)s.Entrance.Z - z, 2))), Bit(s.DryRun))).Skip(from));
                case DelveAdminContract.Nature:
                    if (!TryXz(positional, out _, out _)) return Unhandled;
                    return Done(engine.WorldSites().Select(s => Line("site", s.Kind.ToString(), s.Id, s.Prefab, Bit(s.Timing.Requested), Bit(s.Timing.Due),
                        s.Timing.Remaining.HasValue ? R(s.Timing.Remaining.Value) : "", s.Timing.Clock.ToString(), s.Position.ToString(), Bit(s.Timing.DryRun))).Skip(from));
                case DelveAdminContract.Nearest:
                    if (!TryXz(positional, out var nx, out var nz) || peer != null && !options.ContainsKey("platform")) return Unhandled;
                    return Done(new[] { Nearest(engine, settings, net, nx, nz, peer, peer != null ? options["platform"] : "") });
                case DelveAdminContract.SurfaceNature:
                case DelveAdminContract.SurfaceResources:
                    if (positional.Count != 4 || !Float(positional[0], out var sx) || !Float(positional[1], out var sy) || !Float(positional[2], out var sz)) return Unhandled;
                    return Done(DelveSurface.Begin(new UnityEngine.Vector3(sx, sy, sz), operation == DelveAdminContract.SurfaceNature ? SurfaceScope.Nature : SurfaceScope.Resources,
                        peer?.m_uid ?? ZNet.GetUID(), positional[3], peer == null ? (Action<string>)(line => global::Console.instance?.Print(line)) : null));
            }
            return Unhandled;
        }

        private static IEnumerable<string> Status(DelveEngine engine, DelveConfig settings)
        {
            yield return Line("version", Version(settings));
            foreach (DelveKind kind in Enum.GetValues(typeof(DelveKind)))
            {
                var report = engine.Status.Get(kind); var clock = engine.ClockOf(kind);
                yield return Line("kind", kind.ToString(), Bit(settings.Get(DelveSettings.Kind(kind).DryRun)), Bit(report != null),
                    I(report?.Tracked ?? 0), I(report?.Waiting ?? 0), I(report?.Deferred ?? 0), I(report?.Acted ?? 0), I(report?.Blocked.Values.Sum() ?? 0),
                    R(clock.Delay), clock.Clock.ToString(), Bit(engine.DryRun(kind)), Bit(engine.Active(kind)), report?.Note ?? "");
            }
        }

        /// <summary>The config file and every dry-run value: an edit made elsewhere since the caller read the page changes it.</summary>
        private static string Version(DelveConfig settings)
        {
            var text = "\n" + settings.Get(DelveSettings.General.DryRunAll) + "\n" + string.Join("\n", Enum.GetValues(typeof(DelveKind)).Cast<DelveKind>()
                .Select(kind => settings.Entry(DelveSettings.Kind(kind).DryRun).BoxedValue + ":" + settings.Get(DelveSettings.Kind(kind).DryRun)));
            using (var sha = SHA256.Create())
            {
                var digest = sha.ComputeHash(ReadBounded(settings.File.ConfigFilePath).Concat(Encoding.UTF8.GetBytes(text)).ToArray());
                return string.Concat(digest.Select(b => b.ToString("x2", CultureInfo.InvariantCulture)));
            }
        }

        private static byte[] ReadBounded(string path)
        {
            const long Maximum = 1024 * 1024;
            if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0 || (File.GetAttributes(Path.GetDirectoryName(path)!) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("LinkedConfig");
            using (var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                if (file.Length > Maximum) throw new IOException("ConfigTooLarge");
                using (var result = new MemoryStream())
                {
                    var buffer = new byte[4096]; int count;
                    while ((count = file.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        if (result.Length + count > Maximum) throw new IOException("ConfigTooLarge");
                        result.Write(buffer, 0, count);
                    }
                    return result.ToArray();
                }
            }
        }

        private static string WriteDryRun(DelveConfig settings, DelveKind kind, bool enabled, string? version)
        {
            if (version != null && version != Version(settings)) return "stale";
            var entry = (ConfigEntry<bool>)settings.Entry(DelveSettings.Kind(kind).DryRun);
            var target = MeadworksConversion.Target(settings.File); var started = false;
            try
            {
                var amend = MeadworksConfig.AmendBatch(Plugin.Guid, new ConfigEntryBase[] { entry }, () =>
                {
                    if (version != null && version != Version(settings)) throw new InvalidOperationException("StaleDelveSource");
                    target.Backup();
                    if (version != null && version != Version(settings)) throw new InvalidOperationException("StaleDelveSource");
                    started = true;
                    target.WriteAtomic(new Dictionary<string, object> { [entry.Definition.Section + "." + entry.Definition.Key] = enabled });
                });
                var disk = new ConfigFile(settings.File.ConfigFilePath, false).Bind(entry.Definition, false, entry.Description).Value;
                return amend.Applied && entry.Value == enabled && disk == enabled && settings.Get(DelveSettings.Kind(kind).DryRun) == enabled ? "saved" : started ? "unknown" : "failed";
            }
            catch (InvalidOperationException error) when (!started && error.Message == "StaleDelveSource") { return "stale"; }
            catch { return started ? "unknown" : "failed"; }
        }

        /// <summary>The Regenerate cheat lock for the caller: a peer by its connection, otherwise the local authority.</summary>
        private static bool Locked(ZNetPeer? peer) => peer != null
            ? !MeadworksMischief.DecideForPeer(peer.m_rpc, MischiefCategory.World).Allowed
            : !MeadworksMischief.Decide(Plugin.Guid, MischiefCategory.World).Allowed;

        private static string Nearest(DelveEngine engine, DelveConfig settings, ZNet net, float x, float z, ZNetPeer? peer, string platform)
        {
            var generation = CruciblePlugin.Session.Generation;
            var rpc = peer?.m_rpc; var socket = peer?.m_socket; var peerUid = peer?.m_uid;
            var character = peer == null ? Player.m_localPlayer?.GetComponent<ZNetView>()?.GetZDO() : ZDOMan.instance?.GetZDO(peer.m_characterID);
            if (character == null || character.m_uid.IsNone() || peer != null && (character.GetOwner() != peer.m_uid || socket == null || socket.GetHostName() != platform))
                return "position_unknown";
            var site = engine.DungeonSites().Where(s => s.Kind == DelveKind.FullReset)
                .OrderBy(s => Math.Pow((double)s.Entrance.X - x, 2) + Math.Pow((double)s.Entrance.Z - z, 2))
                .ThenBy(s => s.Id, StringComparer.Ordinal).FirstOrDefault();
            if (site == null) return "none";
            bool skip = settings.RegenerateFor(true, Locked(peer)) == RegenerateVerdict.Allowed;
            bool StillAllowed()
            {
                if (!ReferenceEquals(ZNet.instance, net) || !net.IsServer() || !CruciblePlugin.Session.AuthorityReady
                    || CruciblePlugin.Session.Generation != generation || !ReferenceEquals(Plugin.Engine, engine) || !ReferenceEquals(Plugin.Settings, settings)) return false;
                if (peer != null && (!peer.IsReady() || peer.m_rpc == null || !peer.m_rpc.IsConnected() || peer.m_socket == null
                    || !ReferenceEquals(peer.m_rpc, rpc) || !ReferenceEquals(peer.m_socket, socket) || peer.m_uid != peerUid || peer.m_characterID != character.m_uid
                    || !net.GetPeers().Any(p => ReferenceEquals(p, peer)) || peer.m_socket.GetHostName() != platform || !net.IsAdmin(platform))) return false;
                var current = peer == null ? Player.m_localPlayer?.GetComponent<ZNetView>()?.GetZDO() : ZDOMan.instance?.GetZDO(peer.m_characterID);
                if (!ReferenceEquals(current, character) || peer != null && current!.GetOwner() != peer.m_uid) return false;
                return !skip || settings.RegenerateFor(true, Locked(peer)) == RegenerateVerdict.Allowed;
            }
            return engine.RequestDungeon(site.Id, skip, StillAllowed) ? Line("queued", site.Generator, site.Id) : Line("refused", site.Id);
        }

        private static Tuple<bool, IReadOnlyList<string>> Done(IEnumerable<string> lines) =>
            Tuple.Create(true, (IReadOnlyList<string>)lines.Take(DelveAdminContract.MaximumLines).ToArray());

        private static bool TryKind(string text, out DelveKind kind)
        {
            kind = default;
            return Enum.GetNames(typeof(DelveKind)).Contains(text) && Enum.TryParse(text, false, out kind);
        }
        private static bool TryXz(List<string> positional, out float x, out float z)
        {
            x = z = 0f;
            return positional.Count == 2 && Float(positional[0], out x) && Float(positional[1], out z);
        }
        private static bool Float(string text, out float value) =>
            float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value) && !float.IsNaN(value) && !float.IsInfinity(value);
        private static string Bit(bool value) => value ? "1" : "0";
        private static string I(int value) => value.ToString(CultureInfo.InvariantCulture);
        private static string R(double value) => value.ToString("R", CultureInfo.InvariantCulture);
        private static string Clean(string? text) => (text ?? "").Replace('\t', ' ').Replace('\r', ' ').Replace('\n', ' ');
        private static string Line(params string[] fields) => Line((IEnumerable<string>)fields);
        private static string Line(IEnumerable<string> fields) => string.Join("\t", fields.Select(Clean));
    }
}
