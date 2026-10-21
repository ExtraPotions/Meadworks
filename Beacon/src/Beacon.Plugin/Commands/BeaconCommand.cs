// Beacon - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: the `beacon` console verbs, the converter's way in until the kit panel (BC05) exists (design section 9, C09).
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Security.Cryptography;
using BepInEx;
using ExtraPotions.Beacon.Conversion;
using ExtraPotions.Beacon.Localization;
using ExtraPotions.Beacon.Secrets;
using Jotunn.Entities;

namespace ExtraPotions.Beacon.Runtime
{
    /// <summary>
    /// <c>beacon status</c>, <c>beacon join &lt;name&gt;</c> (BC03b: joins a saved server through the game's own flow with the
    /// selected character), <c>beacon cancel</c> (BC04b: stops an auto-join countdown or a reconnect cycle),
    /// <c>beacon import preview</c>, <c>beacon import apply</c> and
    /// <c>beacon import blank-source confirm</c> (Q2 (a): blanks AutoConnector's plain-text passwords, no copy kept; asks for
    /// <c>confirm</c> first). No output ever contains a password: previews say "password present".
    /// </summary>
    public sealed class BeaconCommand : ConsoleCommand
    {
        public override string Name => "beacon";

        public override string Help => "Beacon server favorites: status, join <name>, cancel, import preview, import apply, import blank-source confirm.";

        static string SourcePath => Path.Combine(Paths.ConfigPath, AutoConnectorImport.SourceFile.Replace('/', Path.DirectorySeparatorChar));
        static string? previewSourceHash, previewListHash, blankSourceHash;
        /// <summary>Identifies the latest preview; Almanac's conversion plan compares it before Apply.</summary>
        internal static (string? Source, string? List) PreviewStamp => (previewSourceHash, previewListHash);
        internal static bool CanBlankSource
        {
            get
            {
                if (blankSourceHash == null || BeaconImportSecrets.Busy) return false;
                try { var source = ReadSource(); return source != null && blankSourceHash == Fingerprint(source); }
                catch (Exception error) when (error is IOException || error is UnauthorizedAccessException) { return false; }
            }
        }
        static string Fingerprint(string text)
        {
            using var hash = SHA256.Create();
            return Convert.ToBase64String(hash.ComputeHash(Encoding.UTF8.GetBytes(text)));
        }

        public override void Run(string[] args)
        {
            try
            {
                foreach (var line in Execute(args ?? Array.Empty<string>())) Print(line);
            }
            catch (Exception error)
            {
                Print("Beacon command failed: " + error.GetType().Name);
            }
        }

        static void Print(string line) => global::Console.instance?.Print(BeaconSecrets.Redactor.Clean(line));

        static string[] Execute(string[] args)
        {
            var verb = args.Length == 0 ? "status" : args[0].ToLowerInvariant();
            var sub = args.Length > 1 ? args[1].ToLowerInvariant() : "";
            var log = Plugin.Log;
            switch (verb)
            {
                case "help": return new[] { BeaconText.Text(BeaconTextKeys.CommandHelp) };
                case "status": return new[] { Status() };
                case "join": return new[] { Join(string.Join(" ", args.Skip(1))) };
                case "cancel": return new[] { BeaconAutoJoin.Cancel(log) ?? BeaconText.Text(BeaconTextKeys.CommandCancelNone) };
                case "import" when sub == "preview": return Preview();
                case "import" when sub == "apply": return Apply(log);
                case "import" when sub == "blank-source": return Blank(args.Length > 2 && args[2].Equals("confirm", StringComparison.OrdinalIgnoreCase));
                case "import": return new[] { BeaconText.Text(BeaconTextKeys.CommandUsage) };
                default: return new[] { BeaconText.Format(BeaconTextKeys.CommandUnknownVerb, verb) };
            }
        }

        static string Status()
        {
            var store = BeaconSecrets.Store;
            var stored = BeaconServers.List.Entries.Count(e => store.Contains(e.SecretRef));
            return BeaconText.Format(BeaconTextKeys.CommandStatus, BeaconServers.List.Count, stored, BeaconText.Text(SecretStores.StorageLineKey(store.Backend)));
        }

        static string Join(string name)
        {
            if (BeaconImportSecrets.Busy) return BeaconText.Text("import_password_pending");
            var entry = BeaconServers.List.FindByName(name.Trim());
            if (entry == null) return BeaconText.Format(BeaconTextKeys.CommandJoinUnknown, name.Trim());
            if (!BeaconJoin.Active)
                return BeaconText.Format(BeaconTextKeys.StepAsideJoin, BeaconModules.StepAside.Notices.Select(n => n.SourceName).FirstOrDefault() ?? "AutoConnector");
            return BeaconMenu.Join(entry.Address, null)
                ? BeaconText.Format(BeaconTextKeys.CommandJoinStarted, entry.Name)
                : BeaconText.Text(BeaconTextKeys.CommandJoinNotReady);
        }

        static string? ReadSource() => File.Exists(SourcePath) ? File.ReadAllText(SourcePath, Encoding.UTF8) : null;

        internal static string[] Preview()
        {
            var source = ReadSource();
            if (source == null) return new[] { BeaconText.Text(BeaconTextKeys.ImportNone) };
            var servers = AutoConnectorImport.Parse(source);
            foreach (var server in servers) BeaconSecrets.Redactor.Track(server.Password);
            try
            {
                var lines = AutoConnectorImport.Preview(AutoConnectorImport.Plan(BeaconServers.List, servers)).Select(BeaconSecrets.Redactor.Clean).ToArray();
                previewSourceHash = Fingerprint(source);
                previewListHash = Fingerprint(ExtraPotions.Beacon.Servers.ServerListCodec.Write(BeaconServers.List));
                return lines;
            }
            finally { foreach (var server in servers) BeaconSecrets.Redactor.Untrack(server.Password); }
        }

        internal static string[] Apply(ExtraPotions.Meadworks.Crucible.MeadworksLog? log)
        {
            var source = ReadSource();
            if (source == null || log == null) return new[] { BeaconText.Text(BeaconTextKeys.ImportNone) };
            if (BeaconImportSecrets.Busy) return new[] { BeaconText.Text("import_password_pending") };
            if (previewSourceHash != Fingerprint(source) || previewListHash != Fingerprint(ExtraPotions.Beacon.Servers.ServerListCodec.Write(BeaconServers.List)))
                return new[] { BeaconText.Text("import_preview_again") };
            var servers = AutoConnectorImport.Parse(source);
            foreach (var s in servers) BeaconSecrets.Redactor.Track(s.Password);
            try
            {
                var plan = AutoConnectorImport.Plan(BeaconServers.List, servers);
                ImportResult? result = null;
                if (!BeaconServers.Amend(candidate => result = AutoConnectorImport.Apply(candidate, new DisabledSecretStore(), plan), log))
                    return new[] { BeaconText.Text("import_write_failed") };
                blankSourceHash = previewSourceHash;
                previewSourceHash = previewListHash = null;
                foreach (var item in plan.Where(p => p.Outcome == ImportOutcome.Add && p.Server.Password != null))
                {
                    var entry = BeaconServers.List.FindByName(item.Server.Name);
                    if (entry != null) BeaconImportSecrets.Start(entry.SecretRef, entry.Name, item.Server.Password!);
                }
                return new[] { BeaconText.Format(BeaconTextKeys.ImportDone, result!.Added, result.Skipped) }
                    .Concat(BeaconImportSecrets.Busy ? new[] { BeaconText.Text("import_password_pending") } : Array.Empty<string>())
                    .Concat(new[] { BeaconText.Text(BeaconTextKeys.ImportSourcePlain), BeaconText.Text(BeaconTextKeys.CommandConfirmBlank) })
                    .Select(BeaconSecrets.Redactor.Clean)
                    .ToArray();
            }
            finally
            {
                foreach (var s in servers) BeaconSecrets.Redactor.Untrack(s.Password);
            }
        }

        internal static string[] Blank(bool confirmed)
        {
            if (BeaconImportSecrets.Busy) return new[] { BeaconText.Text("import_password_pending") };
            var source = ReadSource();
            if (source == null) return new[] { BeaconText.Text(BeaconTextKeys.ImportNone) };
            if (!confirmed) return new[] { BeaconText.Text(BeaconTextKeys.CommandConfirmBlank) };
            if (blankSourceHash == null || blankSourceHash != Fingerprint(source)) return new[] { BeaconText.Text("import_preview_again") };
            var text = AutoConnectorImport.BlankPasswords(source, out var count);
            if (count > 0) BeaconServers.WriteAtomically(SourcePath, text);
            ResetPreview();
            return new[] { BeaconText.Format(BeaconTextKeys.ImportBlankDone, count) };
        }

        internal static void ResetPreview() { previewSourceHash = previewListHash = blankSourceHash = null; }
    }
}
