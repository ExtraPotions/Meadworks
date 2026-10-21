// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Owner writes reuse C01/C09, FileVersion/Backups and the existing ledger sibling-temp/replace pattern.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using BepInEx.Configuration;
using ExtraPotions.Meadworks.Crucible;
using ExtraPotions.Thing.Admin;
using ExtraPotions.Thing.Commands;
using ExtraPotions.Thing.Communication;
using ExtraPotions.Thing.Config;
using ExtraPotions.Thing.Moderation;
using ExtraPotions.Thing.RemoteConfig;
using ExtraPotions.Thing.UI;
using CruciblePlugin = ExtraPotions.Meadworks.Crucible.Plugin;

namespace ExtraPotions.Thing.Runtime
{
    internal static class ThingMessages
    {
        static readonly UTF8Encoding utf8 = new UTF8Encoding(false, true);
        static readonly HashSet<string> written = new HashSet<string>(StringComparer.Ordinal);
        static IDisposable? dataFile;
        static MeadworksLog? log;
        static long failedGeneration = -1;
        internal static void Initialize(MeadworksLog logger) => log = logger;
        internal static void Tick()
        {
            if (!Authority) { if (dataFile != null || written.Count != 0) Reset(); return; }
            if (dataFile != null || failedGeneration == CruciblePlugin.Session.Generation) return;
            try { Attach(); } catch (Exception error) { failedGeneration = CruciblePlugin.Session.Generation; log?.Warning("Announcement file registration failed: " + error.GetType().Name); }
        }
        sealed class Source
        {
            internal byte[] Bytes = Array.Empty<byte>();
            internal string Text = "", Version = "";
            internal bool Bom;
        }
        enum WriteResult { Done, Stale, Invalid, Failed, Unknown }

        static bool Authority => Plugin.Ready && Plugin.Settings != null && ZNet.instance != null && ZNet.instance.IsServer()
            && ThingServer.CurrentAuthority && CruciblePlugin.Session.AuthorityReady && ThingModules.ProductActive(out _);
        static void RequireAuthority() { if (!Authority) throw new InvalidOperationException("ThingAuthorityUnavailable"); }
        internal static void Attach()
        {
            Reset(); RequireAuthority();
            dataFile = MeadworksDataFiles.Register(new DataFileRegistration(Plugin.Guid, ThingSettings.AnnouncementsFile, ThingPaths.AnnouncementsFile,
                () => { RequireAuthority(); return ReadAnnouncements().Text; }, Validate,
                (original, replacement) =>
                {
                    RequireAuthority();
                    if (!Plugin.Settings!.Get(ThingSettings.Remote.Editing)) return false;
                    var current = ReadAnnouncements();
                    if (!string.Equals(current.Text, original, StringComparison.Ordinal)) return false;
                    var result = WriteAnnouncements(current.Version, replacement);
                    ThingServer.Audit(new Actor("host", "server file editor"), ThingAction.Announcements, default,
                        result == WriteResult.Done ? AuditOutcome.Done : AuditOutcome.Failed, "registered file edit: " + result);
                    return result == WriteResult.Done;
                }));
        }
        internal static void Reset() { dataFile?.Dispose(); dataFile = null; written.Clear(); failedGeneration = -1; }

        internal static IReadOnlyList<string> Run(MessageEditRequest request, Actor admin, bool localAuthority)
        {
            if (!Authority || (!localAuthority && !ZNet.instance.IsAdmin(admin.Id))) return new[] { ThingText.Text("refused_not_admin") };
            try
            {
                if (request.Verb == "motd") return Motd(request, admin);
                var source = ReadAnnouncements();
                var validation = Validate(source.Text);
                if (validation.Count != 0) return Reply("message_invalid");
                var parsed = Announcements.Parse(source.Text);
                if (request.Action == "list")
                {
                    ThingServer.Audit(admin, ThingAction.Announcements, default, AuditOutcome.Done, "announcement list read");
                    return ThingUiPage.Encode("announcements", request.Index,
                        parsed.Entries.Select((entry, at) => new ThingUiRow("announce", at.ToString(CultureInfo.InvariantCulture), entry.Minutes.ToString(CultureInfo.InvariantCulture), entry.Text)), source.Version);
                }
                if (request.Action == "get")
                {
                    if (request.Index >= parsed.Entries.Count) return Reply("message_stale");
                    var item = parsed.Entries[request.Index];
                    ThingServer.Audit(admin, ThingAction.Announcements, default, AuditOutcome.Done, "announcement read " + request.Index.ToString(CultureInfo.InvariantCulture));
                    return new MessageDocument("announce", source.Version, request.Index, item.Minutes, item.Text).Encode();
                }
                if (!string.Equals(request.Version, source.Version, StringComparison.OrdinalIgnoreCase)) return Reply("message_stale");
                var updated = MessageEditing.EditAnnouncements(source.Text, request);
                var result = WriteAnnouncements(request.Version, updated);
                ThingServer.Audit(admin, ThingAction.Announcements, default, result == WriteResult.Done ? AuditOutcome.Done : AuditOutcome.Failed,
                    request.Action + " announcement " + request.Index.ToString(CultureInfo.InvariantCulture) + ": " + result);
                return Reply(Key(result));
            }
            catch (FormatException) { return Reply("message_invalid"); }
            catch (ArgumentException) { return Reply("message_invalid"); }
            catch { return Reply("message_failed"); }
        }
        static IReadOnlyList<string> Motd(MessageEditRequest request, Actor admin)
        {
            var settings = Plugin.Settings!;
            var entry = (ConfigEntry<string>)settings.Entry(ThingSettings.Communication.MessageOfTheDay);
            string Version() => FileVersion.Hash(ReadBounded(settings.File.ConfigFilePath).Concat(utf8.GetBytes("\n" + entry.Value + "\n" + settings.Get(ThingSettings.Communication.MessageOfTheDay))).ToArray());
            if (request.Action == "get")
            {
                ThingServer.Audit(admin, ThingAction.MessageOfTheDay, default, AuditOutcome.Done, "message of the day read");
                return new MessageDocument("motd", Version(), 0, 0, settings.Get(ThingSettings.Communication.MessageOfTheDay)).Encode();
            }
            if (!string.Equals(request.Version, Version(), StringComparison.OrdinalIgnoreCase)) return Reply("message_stale");
            var desired = request.Action == "clear" ? "" : request.Text;
            var target = MeadworksConversion.Target(settings.File);
            var started = false;
            WriteResult result;
            try
            {
                var amendment = MeadworksConfig.AmendBatch(Plugin.Guid, new ConfigEntryBase[] { entry }, () =>
                {
                    if (!string.Equals(request.Version, Version(), StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("StaleMessageSource");
                    target.Backup();
                    if (!string.Equals(request.Version, Version(), StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("StaleMessageSource");
                    started = true;
                    target.WriteAtomic(new Dictionary<string, object> { [entry.Definition.Section + "." + entry.Definition.Key] = desired });
                }, ThingSettings.Communication.MessageOfTheDay.Key);
                if (!amendment.Applied) result = started ? WriteResult.Unknown : WriteResult.Failed;
                else
                {
                    // BepInEx's own reader verifies the serialized value, including multiline escaping.
                    var disk = new ConfigFile(settings.File.ConfigFilePath, false).Bind(entry.Definition, "", entry.Description).Value;
                    result = string.Equals(entry.Value, desired, StringComparison.Ordinal) && string.Equals(disk, desired, StringComparison.Ordinal)
                        && string.Equals(settings.Get(ThingSettings.Communication.MessageOfTheDay), desired, StringComparison.Ordinal)
                        ? WriteResult.Done : WriteResult.Unknown;
                }
            }
            catch (InvalidOperationException error) when (error.Message == "StaleMessageSource" && !started) { result = WriteResult.Stale; }
            catch { result = started ? WriteResult.Unknown : WriteResult.Failed; }
            ThingServer.Audit(admin, ThingAction.MessageOfTheDay, default, result == WriteResult.Done ? AuditOutcome.Done : AuditOutcome.Failed,
                request.Action + " message of the day: " + result);
            return Reply(Key(result));
        }
        static Source ReadAnnouncements()
        {
            RequireAuthority();
            var bytes = ReadBounded(ThingPaths.AnnouncementsFile);
            var bom = bytes.Length >= 3 && bytes[0] == 0xef && bytes[1] == 0xbb && bytes[2] == 0xbf;
            return new Source { Bytes = bytes, Text = utf8.GetString(bytes, bom ? 3 : 0, bytes.Length - (bom ? 3 : 0)), Version = FileVersion.Hash(bytes), Bom = bom };
        }
        internal static byte[] ReadBounded(string path)
        {
            // These are fixed owner paths; the existing remote-files guard also refuses links and files above 1 MiB.
            if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0 || (File.GetAttributes(Path.GetDirectoryName(path)!) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("LinkedMessageSource");
            using (var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                if (file.Length > RemoteFiles.MaxBytes) throw new IOException("MessageSourceTooLarge");
                using (var result = new MemoryStream())
                {
                    var buffer = new byte[4096]; int count;
                    while ((count = file.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        if (result.Length + count > RemoteFiles.MaxBytes) throw new IOException("MessageSourceTooLarge");
                        result.Write(buffer, 0, count);
                    }
                    return result.ToArray();
                }
            }
        }
        static IReadOnlyList<string> Validate(string text) => MessageEditing.ValidateSource(text);
        static WriteResult WriteAnnouncements(string expected, string replacement)
        {
            RequireAuthority();
            if (Validate(replacement).Count != 0) return WriteResult.Invalid;
            var temporary = ThingPaths.AnnouncementsFile + ".edit-" + Guid.NewGuid().ToString("N") + ".tmp";
            var owned = false; var published = false;
            try
            {
                var current = ReadAnnouncements();
                if (!string.Equals(current.Version, expected, StringComparison.OrdinalIgnoreCase)) return WriteResult.Stale;
                if (Validate(current.Text).Count != 0) return WriteResult.Invalid;
                var bytes = utf8.GetBytes(replacement);
                if (current.Bom) bytes = new byte[] { 0xef, 0xbb, 0xbf }.Concat(bytes).ToArray();
                if (Backups.FirstWriteThisSession(ThingPaths.AnnouncementsFile, written))
                {
                    File.Copy(ThingPaths.AnnouncementsFile, Backups.Name(ThingPaths.AnnouncementsFile, DateTime.UtcNow), false);
                    written.Add(ThingPaths.AnnouncementsFile);
                }
                using (var file = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                { owned = true; file.Write(bytes, 0, bytes.Length); file.Flush(true); }
                if (!FileVersion.Matches(expected, ReadBounded(ThingPaths.AnnouncementsFile))) return WriteResult.Stale;
                published = true; // Once replace is attempted, an exception is an uncertain outcome.
                File.Replace(temporary, ThingPaths.AnnouncementsFile, null); owned = false;
                if (!FileVersion.Matches(FileVersion.Hash(bytes), ReadBounded(ThingPaths.AnnouncementsFile))) return WriteResult.Unknown;
                ThingServer.ReloadAnnouncements(Announcements.Parse(replacement).Entries);
                var keep = Plugin.Settings!.Get(ThingSettings.Remote.BackupCount);
                foreach (var path in Backups.ToDelete(ThingPaths.AnnouncementsFile, Directory.EnumerateFiles(ThingPaths.DataFolder, ThingSettings.AnnouncementsFile + ".*.bak"), keep))
                    try { File.Delete(path); } catch { /* Retention failure does not undo a verified owner write. */ }
                return WriteResult.Done;
            }
            catch { return published ? WriteResult.Unknown : WriteResult.Failed; }
            finally { if (owned) try { File.Delete(temporary); } catch { } }
        }
        static string Key(WriteResult result) => result == WriteResult.Done ? "message_saved" : result == WriteResult.Stale ? "message_stale"
            : result == WriteResult.Invalid ? "message_invalid" : result == WriteResult.Unknown ? "message_unknown" : "message_failed";
        static IReadOnlyList<string> Reply(string key) => new[] { ThingText.Text(key) };
    }
}
