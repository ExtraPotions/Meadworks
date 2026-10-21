// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-owned reuse: Almanac ConfigDiscovery's actual plugin/one-level field scan, native adapters and C09 owner writes.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using BepInEx.Logging;
using System.Threading;
using ExtraPotions.Meadworks.Crucible;
using ExtraPotions.Thing.Admin;
using ExtraPotions.Thing.Config;
using ExtraPotions.Thing.Moderation;
using ExtraPotions.Thing.RemoteConfig;
using ExtraPotions.Thing.Runtime.ConfigEditing;
using ExtraPotions.Thing.UI;
using CruciblePlugin = ExtraPotions.Meadworks.Crucible.Plugin;

namespace ExtraPotions.Thing.Runtime
{
    internal static class ThingConfigFiles
    {
        sealed class FileItem { internal string Path = "", Relative = "", Id = ""; internal ConfigFile? File; }
        static long generation = -1;
        static readonly HashSet<string> backedUp = new HashSet<string>(StringComparer.Ordinal);
        static readonly UTF8Encoding utf8 = new UTF8Encoding(false, true);
        static bool Windows => Path.DirectorySeparatorChar == '\\';
        static StringComparison Comparison => Windows ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        static string Canonical(string path) => Windows ? Path.GetFullPath(path).ToUpperInvariant() : Path.GetFullPath(path);
        internal static string Id(string value) => FileVersion.Hash(utf8.GetBytes(value));
        static string EntryId(ConfigEntryBase entry) => Id(entry.Definition.Section + "\0" + entry.Definition.Key);
        internal static bool Authorized(Actor admin, bool local)
        {
            if (!Plugin.Ready || !CruciblePlugin.IsReady || !CruciblePlugin.Session.AuthorityReady || !ThingServer.CurrentAuthority || !ThingModules.ProductActive(out _) || Plugin.Settings == null) return false;
            if (local) return true;
            var net = ZNet.instance;
            return SettingsAccess.CanConfigure(false, net != null && net.IsAdmin(admin.Id), Plugin.Settings.AllowAdminSettingsEditing, Plugin.Settings.Get(ThingSettings.Remote.Editing)) && net != null && net.GetPeers().Any(p => p != null && p.IsReady() && p.m_rpc?.IsConnected() == true && p.m_socket?.GetHostName() == admin.Id);
        }
        static FilePolicy Policy() => new FilePolicy(Plugin.Settings!.Get(ThingSettings.Remote.Scope), Plugin.Settings.Get(ThingSettings.Remote.AllowList),
            MeadworksDataFiles.Registered.Select(f => Relative(f.Path)).Where(p => p != null)!, !Windows);
        internal static string? Relative(string path)
        {
            var root = Path.GetFullPath(Paths.ConfigPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            var full = Path.GetFullPath(path);
            return full.StartsWith(root, Comparison) ? full.Substring(root.Length).Replace(Path.DirectorySeparatorChar, '/') : null;
        }
        internal static bool Allowed(string path)
        {
            var relative = Relative(path); if (relative == null || !File.Exists(path)) return false;
            var info = new FileInfo(path);
            var linked = (info.Attributes & FileAttributes.ReparsePoint) != 0;
            for (var parent = info.Directory; parent != null; parent = parent.Parent)
            {
                linked |= (parent.Attributes & FileAttributes.ReparsePoint) != 0;
                if (Canonical(parent.FullName) == Canonical(Paths.ConfigPath)) break;
            }
            return RemoteFiles.Check(new FileRequest(relative, info.Length, linked), Policy()) == FileVerdict.Allowed;
        }
        // A request-time scan avoids installing a second Almanac discovery singleton or constructor hook.
        static FileItem[] Discover()
        {
            var files = new Dictionary<string, FileItem>(Windows ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
            var ambiguous = new HashSet<string>(files.Comparer);
            void Add(string path, ConfigFile? handle)
            {
                try
                {
                    if (!Allowed(path)) return;
                    var key = Canonical(path);
                    if (!files.TryGetValue(key, out var item)) files[key] = item = new FileItem { Path = Path.GetFullPath(path), Relative = Relative(path)!, Id = Id(key) };
                    if (handle == null) return;
                    if (item.File != null && !ReferenceEquals(item.File, handle)) ambiguous.Add(key);
                    else item.File = handle;
                }
                catch { /* One unavailable plugin file must not prevent other allowed files from being listed. */ }
            }
            foreach (var info in Chainloader.PluginInfos.Values.Take(4096).ToArray())
            {
                var instance = info.Instance; if (instance == null) continue;
                Add(instance.Config.ConfigFilePath, instance.Config);
                for (var at = instance.GetType(); at != null && at != typeof(object); at = at.BaseType)
                    foreach (var field in at.GetFields(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly).Take(4096))
                    {
                        if (!typeof(ConfigFile).IsAssignableFrom(field.FieldType)) continue;
                        try { if (field.GetValue(field.IsStatic ? null : instance) is ConfigFile file) Add(file.ConfigFilePath, file); } catch { }
                    }
            }
            foreach (var path in Directory.EnumerateFiles(Paths.ConfigPath, "*.cfg", SearchOption.TopDirectoryOnly).Take(10000)) Add(path, null);
            foreach (var file in MeadworksDataFiles.Registered.Take(10000)) Add(file.Path, null);
            foreach (var key in ambiguous) files[key].File = null;
            return files.Values.OrderBy(f => f.Relative, StringComparer.Ordinal).Take(10000).ToArray();
        }
        static bool Sensitive(ConfigEntryBase entry)
        {
            var name = new string((entry.Definition.Section + entry.Definition.Key).Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());
            return new[] { "password", "passwd", "token", "secret", "apikey", "credential", "privatekey" }.Any(name.Contains);
        }
        static ConfigEntryBase[] Entries(FileItem item) => item.File == null ? Array.Empty<ConfigEntryBase>() : item.File.Select(p => p.Value)
            .Where(e => !Sensitive(e) && (!MeadworksConfig.TryGetMetadata(e, out var metadata) || metadata.Visibility == ConfigVisibility.Shown) && NativeTags.Read(e.Description.Tags).Attributes.Browsable != false && (MeadworksConfig.TryGetMetadata(e, out _) || !ThingExternalConfig.Inspect(e).Hidden))
            .OrderBy(e => e.Definition.Section, StringComparer.Ordinal).ThenBy(e => e.Definition.Key, StringComparer.Ordinal).Take(10000).ToArray();
        static string Version(FileItem file, ConfigEntryBase entry)
        {
            using (var stream = new MemoryStream())
            {
                var bytes = ThingMessages.ReadBounded(file.Path); stream.Write(bytes, 0, bytes.Length);
                using (var writer = new BinaryWriter(stream, utf8, true))
                {
                    writer.Write(CruciblePlugin.Session.Generation); writer.Write(EntryId(entry));
                    writer.Write(TomlTypeConverter.ConvertToString(entry.BoxedValue, entry.SettingType));
                    writer.Write(MeadworksConfig.TryGetMetadata(entry, out var m) ? m.Decision.ToString() + "\n" + TomlTypeConverter.ConvertToString(m.SafeValue, entry.SettingType) : "unmanaged\n" + ThingExternalConfig.Inspect(entry).Stamp);
                }
                return FileVersion.Hash(stream.ToArray());
            }
        }
        static bool Editable(NativeEntry native, out ConfigEntryMetadata metadata)
        {
            var entry = native.Entry;
            if (Plugin.Settings?.IsSettingsPolicy(entry) == true) { metadata = null!; return false; }
            var managed = MeadworksConfig.TryGetMetadata(entry, out metadata!);
            if (Sensitive(entry) || native.Attributes.Browsable == false || native.Attributes.ReadOnly == true || native.Attributes.CharterReadOnly || native.Attributes.GenericReadOnly
                || !native.Shape.HasConverter || native.Attributes.HasCodecDelegates) return false;
            if (managed) return metadata.Visibility == ConfigVisibility.Shown && metadata.Decision.Allowed;
            var external = ThingExternalConfig.Inspect(entry); return external.Allowed && !external.Hidden;
        }
        internal static IReadOnlyList<string> Run(ConfigEditRequest request, Actor admin, bool local, string connection = "")
        {
            if (!Authorized(admin, local)) return Reply("config_unavailable");
            if (generation != CruciblePlugin.Session.Generation) { generation = CruciblePlugin.Session.Generation; backedUp.Clear(); }
            try
            {
                if (request.Verb.StartsWith("yaml-", StringComparison.Ordinal)) return ThingYamlFiles.Run(request, admin, local, connection);
                ThingExternalConfig.BeginRequest();
                var files = Discover();
                if (request.Verb == "list")
                {
                    ThingServer.Audit(admin, ThingAction.RemoteConfigRead, default, AuditOutcome.Done, "allowed config file list");
                    return ThingUiPage.Encode("config_files", request.Offset, files.Select(f => new ThingUiRow(ThingYamlFiles.Registered(f.Path) != null ? "yaml" : f.File == null ? "readonly" : "bound", f.Id, f.Relative, ThingText.Text(ThingYamlFiles.Registered(f.Path) != null ? "config_yaml" : f.File == null ? "config_unbound" : "config_bound"))));
                }
                var file = files.SingleOrDefault(f => f.Id == request.File);
                if (file == null || file.File == null) return Reply("config_unbound");
                var entries = Entries(file);
                if (request.Verb == "entries")
                {
                    ThingServer.Audit(admin, ThingAction.RemoteConfigRead, default, AuditOutcome.Done, "config entries " + file.Relative);
                    return ThingUiPage.Encode("config_entries", request.Offset, entries.Select(e => new ThingUiRow("entry", EntryId(e), e.Definition.Section + "." + e.Definition.Key, e.SettingType.Name)));
                }
                var entry = entries.SingleOrDefault(e => EntryId(e) == request.Entry);
                if (entry == null) return Reply("config_unavailable");
                var native = new NativeEntry(entry); var editable = Editable(native, out _);
                if (request.Verb == "list-get")
                {
                    ThingServer.Audit(admin, ThingAction.RemoteConfigRead, default, AuditOutcome.Done, "typed list read " + file.Relative + " " + entry.Definition);
                    return ThingConfigLists.Read(file.Id, EntryId(entry), Version(file, entry), native, editable);
                }
                if (request.Verb == "list-set")
                {
                    if (!editable) return Reply("config_readonly");
                    if (Version(file, entry) != request.Version) return Reply("config_stale");
                    try { request = request.WithValue(ThingConfigLists.Serialize(native, request.ListValues)); } catch { return Reply("config_invalid"); }
                }
                if (request.Writes) return Write(request, file, native, admin, local);
                ThingServer.Audit(admin, ThingAction.RemoteConfigRead, default, AuditOutcome.Done, "config entry " + file.Relative + " " + entry.Definition);
                return new ConfigEntryDocument { File = file.Id, Entry = EntryId(entry), Version = Version(file, entry), Section = entry.Definition.Section, Key = entry.Definition.Key,
                    Description = entry.Description.Description, Current = native.Shape.HasConverter ? native.RawSerialized() : entry.BoxedValue?.ToString() ?? "", Default = native.Shape.HasConverter ? TomlTypeConverter.ConvertToString(entry.DefaultValue, entry.SettingType) : entry.DefaultValue?.ToString() ?? "",
                    Effective = MeadworksConfig.TryGetMetadata(entry, out var metadata) && metadata.Decision.Source == ConfigValueSource.Locked
                        ? TomlTypeConverter.ConvertToString(metadata.SafeValue, entry.SettingType) : native.Shape.HasConverter ? native.RawSerialized() : entry.BoxedValue?.ToString() ?? "",
                    Editable = editable, Reason = editable ? "" : ThingText.Text("config_readonly"), Shape = native.Shape, Attributes = native.Attributes }.Encode();
            }
            catch { return Reply("config_unavailable"); }
        }
        static IReadOnlyList<string> Write(ConfigEditRequest request, FileItem file, NativeEntry native, Actor admin, bool local)
        {
            if (!Editable(native, out var metadata)) return Reply("config_readonly");
            var entry = native.Entry;
            if (!Authorized(admin, local) || Version(file, entry) != request.Version) return Reply("config_stale");
            var parsed = native.Parse(request.Value);
            if (!parsed.Valid && native.Shape.Kind == ExtraPotions.Thing.ConfigEditing.Editors.SettingKind.Enum && native.Shape.IsFlags && request.Value == "0")
            {
                var zero = Enum.ToObject(entry.SettingType, 0);
                if (entry.Description.AcceptableValues == null || entry.Description.AcceptableValues.IsValid(zero))
                    parsed = new NativeParsedValue(zero, TomlTypeConverter.ConvertToString(zero, entry.SettingType), false);
            }
            if (!parsed.Valid || utf8.GetByteCount(parsed.Serialized) > 4096) return Reply("config_invalid");
            var started = false; var result = "config_failed";
            bool Current()
            {
                if (metadata == null) ThingExternalConfig.BeginRequest();
                if (!Authorized(admin, local) || generation != CruciblePlugin.Session.Generation || !Allowed(file.Path) || !Editable(native, out _)) return false;
                var current = Discover().SingleOrDefault(f => f.Id == file.Id);
                return current != null && ReferenceEquals(current.File, file.File) && current.File![entry.Definition] == entry && Version(file, entry) == request.Version;
            }
            try
            {
                if (!Current()) return Reply("config_stale");
                var target = MeadworksConversion.Target(file.File!);
                Action apply = () =>
                {
                    if (!Current()) throw new InvalidOperationException("StaleRemoteConfig");
                    if (!backedUp.Contains(Canonical(file.Path)))
                    {
                        // Reuse Thing's existing first-write/session backup names and retention helpers.
                        var backup = Backups.Name(file.Path, DateTime.UtcNow); File.Copy(file.Path, backup, false); backedUp.Add(Canonical(file.Path));
                    }
                    if (!Current()) throw new InvalidOperationException("StaleRemoteConfig");
                    started = true;
                    using (var errors = new HandlerErrors())
                    {
                        target.WriteAtomic(new Dictionary<string, object> { [entry.Definition.Section + "." + entry.Definition.Key] = parsed.Value! });
                        if (errors.Error != null) throw new InvalidOperationException("OwnerHandlerFailed", errors.Error);
                    }
                };
                var applied = metadata != null ? MeadworksConfig.AmendBatch(metadata.ProductId, new[] { entry }, apply, entry.Definition.Key).Applied
                    : ThingExternalConfig.Apply(entry, apply, Current);
                if (applied)
                {
                    var actual = native.RawSerialized();
                    var diskFile = new ConfigFile(file.Path, false);
                    var bind = typeof(ConfigFile).GetMethods().Single(m => m.Name == "Bind" && m.IsGenericMethodDefinition && m.GetParameters().Length == 3 && m.GetParameters()[0].ParameterType == typeof(ConfigDefinition));
                    var disk = (ConfigEntryBase)bind.MakeGenericMethod(entry.SettingType).Invoke(diskFile, new[] { (object)entry.Definition, entry.DefaultValue, entry.Description })!;
                    result = actual == parsed.Serialized && TomlTypeConverter.ConvertToString(disk.BoxedValue, entry.SettingType) == actual ? "config_saved" : "config_unknown";
                    if (result == "config_saved")
                        foreach (var backup in Backups.ToDelete(file.Path, Directory.EnumerateFiles(Path.GetDirectoryName(file.Path)!, Path.GetFileName(file.Path) + ".*.bak"), Plugin.Settings!.Get(ThingSettings.Remote.BackupCount)))
                            try { File.Delete(backup); } catch { /* Retention failure cannot undo the verified owner write. */ }
                }
                else result = started ? "config_unknown" : "config_failed";
            }
            catch (InvalidOperationException error) when (error.Message == "StaleRemoteConfig" && !started) { result = "config_stale"; }
            catch { result = started ? "config_unknown" : "config_failed"; }
            ThingServer.Audit(admin, ThingAction.RemoteConfigWrite, default, result == "config_saved" ? AuditOutcome.Done : AuditOutcome.Failed, file.Relative + " " + entry.Definition + ": " + result);
            if (result == "config_saved")
            {
                try { return ConfigWire.WriteEcho(Version(file, entry), native.RawSerialized()); }
                catch { return Reply("config_unknown"); }
            }
            return Reply(result);
        }
        // Reuse Almanac EntryWriter's assignment-thread observation; BepInEx logs handler exceptions instead of propagating them.
        sealed class HandlerErrors : ILogListener
        {
            readonly int thread = Thread.CurrentThread.ManagedThreadId;
            internal Exception? Error { get; private set; }
            internal HandlerErrors() => Logger.Listeners.Add(this);
            public void LogEvent(object sender, LogEventArgs args)
            { if (Thread.CurrentThread.ManagedThreadId == thread && (args.Level & LogLevel.Error) != 0 && args.Data is Exception error) Error ??= error; }
            public void Dispose() => Logger.Listeners.Remove(this);
        }
        static IReadOnlyList<string> Reply(string key) => new[] { ThingText.Text(key) };
        internal static void Reset() { backedUp.Clear(); generation = -1; ThingExternalConfig.Reset(); }
    }
}
