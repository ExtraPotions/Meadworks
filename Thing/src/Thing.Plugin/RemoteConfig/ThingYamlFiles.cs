// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
// Existing DataFileRegistration owns parsing, publication and disk replacement; this service owns only bounded request state.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using ExtraPotions.Meadworks.Crucible;
using ExtraPotions.Thing.Admin;
using ExtraPotions.Thing.Config;
using ExtraPotions.Thing.Moderation;
using ExtraPotions.Thing.RemoteConfig;
using CruciblePlugin = ExtraPotions.Meadworks.Crucible.Plugin;

namespace ExtraPotions.Thing.Runtime
{
    internal static class ThingYamlFiles
    {
        const int Budget = 4 * 1024 * 1024;
        static readonly Stopwatch clock = Stopwatch.StartNew();
        static readonly Dictionary<string, Document> documents = new Dictionary<string, Document>(StringComparer.Ordinal);
        static readonly HashSet<string> backups = new HashSet<string>(StringComparer.Ordinal);
        static long generation = -1;
        sealed class Document
        {
            internal string Token = "", Version = "", DiskHash = "", Connection = "";
            internal Actor Admin;
            internal bool Local;
            internal long Generation;
            internal ZNet Net = null!;
            internal ZNetPeer? Peer;
            internal ZRpc? Rpc;
            internal object? Socket;
            internal DataFileRegistration Owner = null!;
            internal byte[] Original = Array.Empty<byte>();
            internal YamlUpload? Upload;
            internal double Expires;
        }
        static int Reserved => documents.Values.Sum(d => d.Original.Length + (d.Upload?.Length ?? 0));
        internal static DataFileRegistration? Registered(string path)
        {
            var full = Path.GetFullPath(path);
            var comparison = Path.DirectorySeparatorChar == '\\' ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            if (!full.EndsWith(".yml", comparison) && !full.EndsWith(".yaml", comparison)) return null;
            return MeadworksDataFiles.Registered.SingleOrDefault(f => string.Equals(f.Path, full, comparison));
        }
        static ZNetPeer? Peer(Actor admin, string connection)
        {
            if (!long.TryParse(connection, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var uid)) return null;
            return ZNet.instance?.GetPeers().SingleOrDefault(p => p != null && p.m_uid == uid && p.IsReady() && p.m_rpc?.IsConnected() == true && p.m_socket?.GetHostName() == admin.Id);
        }
        static bool Live(Document d)
        {
            if (d.Expires <= clock.Elapsed.TotalSeconds || d.Generation != CruciblePlugin.Session.Generation || !ReferenceEquals(d.Net, ZNet.instance)
                || !ThingConfigFiles.Authorized(d.Admin, d.Local)) return false;
            return d.Local || ReferenceEquals(Peer(d.Admin, d.Connection), d.Peer) && d.Peer != null && ReferenceEquals(d.Peer.m_rpc, d.Rpc) && ReferenceEquals(d.Peer.m_socket, d.Socket);
        }
        static bool Bound(Document d) => Live(d) && ThingConfigFiles.Allowed(d.Owner.Path) && ReferenceEquals(Registered(d.Owner.Path), d.Owner);
        static bool Current(Document d)
        {
            if (!Bound(d) || !FileVersion.Matches(d.DiskHash, ThingMessages.ReadBounded(d.Owner.Path))) return false;
            var original = YamlTransfer.Text(d.Original);
            return string.Equals(d.Owner.Read(), original, StringComparison.Ordinal) && Bound(d);
        }
        internal static void Tick()
        {
            if (!CruciblePlugin.IsReady || generation != CruciblePlugin.Session.Generation)
            { Reset(); generation = CruciblePlugin.IsReady ? CruciblePlugin.Session.Generation : -1; return; }
            if (documents.Count > 0) foreach (var item in documents.ToArray()) if (!Live(item.Value)) documents.Remove(item.Key);
        }
        internal static void Reset() { documents.Clear(); backups.Clear(); generation = -1; }
        static IReadOnlyList<string> Reply(string key) => new[] { ThingText.Text(key) };
        internal static IReadOnlyList<string> Run(ConfigEditRequest request, Actor admin, bool local, string connection)
        {
            Tick();
            if (!ThingConfigFiles.Authorized(admin, local) || !local && Peer(admin, connection) == null) return Reply("config_unavailable");
            try
            {
                if (request.Verb == "yaml-open") return Open(request.File, admin, local, connection);
                if (!documents.TryGetValue(request.Token, out var d) || d.Admin.Id != admin.Id || d.Local != local || d.Connection != connection || !Live(d)) return Reply("config_stale");
                if (request.Verb == "yaml-cancel") { documents.Remove(d.Token); return new[] { "THING_YAML_CANCELED" }; }
                if (!Current(d)) { documents.Remove(d.Token); return Reply("config_stale"); }
                d.Expires = clock.Elapsed.TotalSeconds + 60;
                switch (request.Verb)
                {
                    case "yaml-read":
                        if (request.Offset > d.Original.Length) return Reply("config_invalid");
                        return YamlTransfer.Chunk(d.Token, request.Offset, d.Original.Skip(request.Offset).Take(YamlTransfer.ChunkBytes).ToArray());
                    case "yaml-begin":
                        if (d.Upload != null || Reserved + request.Total > Budget) return Reply("config_invalid");
                        d.Upload = new YamlUpload(request.Total, request.Version); return new[] { "THING_YAML_READY" };
                    case "yaml-put":
                        if (d.Upload == null || !d.Upload.Append(request.Offset, request.Bytes)) { documents.Remove(d.Token); return Reply("config_invalid"); }
                        return new[] { "THING_YAML_RECEIVED\t" + d.Upload.Received.ToString(CultureInfo.InvariantCulture) };
                    case "yaml-commit": return Apply(d);
                    default: return Reply("config_invalid");
                }
            }
            catch { documents.Remove(request.Token); return Reply("config_failed"); }
        }
        static IReadOnlyList<string> Open(string fileId, Actor admin, bool local, string connection)
        {
            var owner = MeadworksDataFiles.Registered.SingleOrDefault(f => ThingConfigFiles.Id(Path.DirectorySeparatorChar == '\\' ? Path.GetFullPath(f.Path).ToUpperInvariant() : Path.GetFullPath(f.Path)) == fileId);
            if (owner == null || !ReferenceEquals(Registered(owner.Path), owner) || !ThingConfigFiles.Allowed(owner.Path)) return Reply("config_unbound");
            foreach (var old in documents.Values.Where(d => d.Admin.Id == admin.Id && d.Local == local).ToArray()) documents.Remove(old.Token);
            var disk = ThingMessages.ReadBounded(owner.Path);
            var originalText = owner.Read();
            if (originalText.Length > YamlTransfer.MaxBytes || System.Text.Encoding.UTF8.GetByteCount(originalText) > YamlTransfer.MaxBytes || documents.Count >= 32) return Reply("config_invalid");
            var original = YamlTransfer.Bytes(originalText);
            if (original.Length > YamlTransfer.MaxBytes || Reserved + original.Length > Budget) return Reply("config_invalid");
            var peer = local ? null : Peer(admin, connection);
            var d = new Document { Token = ThingConfigFiles.Id(Guid.NewGuid().ToString("N")), Admin = admin, Local = local, Connection = connection,
                Generation = CruciblePlugin.Session.Generation, Net = ZNet.instance, Peer = peer, Rpc = peer?.m_rpc, Socket = peer?.m_socket,
                Owner = owner, Original = original, DiskHash = FileVersion.Hash(disk), Expires = clock.Elapsed.TotalSeconds + 60 };
            d.Version = ThingConfigFiles.Id(d.DiskHash + "\n" + FileVersion.Hash(original) + "\n" + owner.ProductId + "\n" + owner.Name + "\n" + d.Generation.ToString(CultureInfo.InvariantCulture));
            if (!Current(d)) return Reply("config_stale");
            documents.Add(d.Token, d);
            ThingServer.Audit(admin, ThingAction.RemoteConfigRead, default, AuditOutcome.Done, "registered YAML read " + ThingConfigFiles.Relative(owner.Path));
            return YamlTransfer.Header(d.Token, d.Version, original.Length);
        }
        static IReadOnlyList<string> Apply(Document d)
        {
            // Consume before invoking owner code. A lost reply or false/throwing callback must never cause a second Apply.
            documents.Remove(d.Token);
            if (d.Upload == null || !d.Upload.Take(out var replacement)) return Reply("config_invalid");
            var started = false; var result = "config_failed";
            try
            {
                if (!Current(d)) return Reply("config_stale");
                if (d.Owner.Validate(replacement).Count != 0) return Reply("config_invalid");
                if (!Current(d)) return Reply("config_stale");
                var path = Path.GetFullPath(d.Owner.Path); var backupBase = path + ".thing-remote"; var backupId = Path.DirectorySeparatorChar == '\\' ? path.ToUpperInvariant() : path;
                if (!backups.Contains(backupId)) { File.Copy(path, Backups.Name(backupBase, DateTime.UtcNow), false); backups.Add(backupId); }
                if (!Current(d)) return Reply("config_stale");
                started = true;
                var accepted = d.Owner.Apply(YamlTransfer.Text(d.Original), replacement);
                var actual = Bound(d) ? d.Owner.Read() : null;
                var diskText = Bound(d) ? YamlTransfer.Text(ThingMessages.ReadBounded(path)) : null;
                if (diskText?.StartsWith("\uFEFF", StringComparison.Ordinal) == true) diskText = diskText.Substring(1);
                result = accepted && actual == replacement && diskText == replacement ? "config_yaml_saved" : "config_unknown";
                if (result == "config_yaml_saved")
                    foreach (var backup in Backups.ToDelete(backupBase, Directory.EnumerateFiles(Path.GetDirectoryName(path)!, Path.GetFileName(backupBase) + ".*.bak"), Plugin.Settings!.Get(ThingSettings.Remote.BackupCount)))
                        try { File.Delete(backup); } catch { }
            }
            catch { result = started ? "config_unknown" : "config_failed"; }
            ThingServer.Audit(d.Admin, ThingAction.RemoteConfigWrite, default, result == "config_yaml_saved" ? AuditOutcome.Done : AuditOutcome.Failed,
                "registered YAML " + ThingConfigFiles.Relative(d.Owner.Path) + ": " + result);
            return Reply(result);
        }
    }
}
