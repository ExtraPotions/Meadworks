// Beacon - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original, behavior only: reads the servers.yaml format AutoConnector 1.0.0 documents in its README and file header
// (name keys, ServerAddress, ServerAliases, ServerPass, single-quote quoting with doubled quotes). No AutoConnector code was read.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ExtraPotions.Beacon.Secrets;
using ExtraPotions.Beacon.Servers;

namespace ExtraPotions.Beacon.Conversion
{
    public enum ImportProblem { None, MissingAddress, InvalidAddress }

    /// <summary>One server read from servers.yaml. <see cref="ToString"/> never shows the password.</summary>
    public sealed class ImportedServer
    {
        internal ImportedServer(string name, string? addressText, IReadOnlyList<string> aliasTexts, string? password)
        {
            Name = name;
            AddressText = addressText;
            Password = string.IsNullOrEmpty(password) ? null : password;
            if (string.IsNullOrWhiteSpace(addressText)) Problem = ImportProblem.MissingAddress;
            else if (!ServerAddress.TryParse(addressText, out var a)) Problem = ImportProblem.InvalidAddress;
            else Address = a;
            Aliases = aliasTexts.Select(t => ServerAddress.TryParse(t, out var x) ? x : null).Where(x => x != null).Select(x => x!).ToList();
        }

        public string Name { get; }
        public string? AddressText { get; }
        public ServerAddress? Address { get; }
        public IReadOnlyList<ServerAddress> Aliases { get; }
        public ImportProblem Problem { get; }
        public bool HasPassword => Password != null;

        /// <summary>The plain-text password from the source file; only passed to the secret store.</summary>
        public string? Password { get; }

        public override string ToString() => Name + " (" + (Address?.ToString() ?? "no address") + "), " + (HasPassword ? "password present" : "no password");
    }

    public enum ImportOutcome { Add, SameName, AddressTaken, Invalid }

    public sealed class ImportPlanItem
    {
        internal ImportPlanItem(ImportedServer server, ImportOutcome outcome)
        {
            Server = server;
            Outcome = outcome;
        }

        public ImportedServer Server { get; }
        public ImportOutcome Outcome { get; }

        public override string ToString() => Outcome + ": " + Server;
    }

    public sealed class ImportResult
    {
        internal ImportResult(int added, int skipped, IReadOnlyList<string> passwordNotStored)
        {
            Added = added;
            Skipped = skipped;
            PasswordNotStored = passwordNotStored;
        }

        public int Added { get; }
        public int Skipped { get; }

        /// <summary>Servers imported without their password because the secret store refused it.</summary>
        public IReadOnlyList<string> PasswordNotStored { get; }
    }

    /// <summary>C09 Preview and Apply for AutoConnector (design F12, section 9, Q2 (a)).</summary>
    public static class AutoConnectorImport
    {
        public const string SourceGuid = "MidnightsFX.Autoconnect";

        /// <summary>Relative to <c>BepInEx/config</c>.</summary>
        public const string SourceFile = "AutoConnector/servers.yaml";

        const string AddressKey = "ServerAddress";
        const string AliasesKey = "ServerAliases";
        const string PassKey = "ServerPass";

        public static IReadOnlyList<ImportedServer> Parse(string yaml)
        {
            var result = new List<ImportedServer>();
            string? name = null, address = null, pass = null, listKey = null;
            var aliases = new List<string>();

            void Flush()
            {
                if (name != null) result.Add(new ImportedServer(name, address, aliases.ToList(), pass));
                name = address = pass = listKey = null;
                aliases.Clear();
            }

            foreach (var raw in (yaml ?? "").Replace("\r\n", "\n").Split('\n'))
            {
                var line = raw.TrimEnd('\r', ' ', '\t');
                var body = line.TrimStart(' ');
                if (body.Length == 0 || body[0] == '#') continue;
                var indent = line.Length - body.Length;
                if (indent == 0 && body[0] != '-')
                {
                    Flush();
                    name = TopKey(body);
                    continue;
                }
                if (name == null) continue;
                if (body.StartsWith("- ", StringComparison.Ordinal) || body == "-")
                {
                    if (listKey == AliasesKey) aliases.Add(Scalar(body.Substring(1)));
                    continue;
                }
                var colon = body.IndexOf(':');
                if (colon <= 0) continue;
                var key = body.Substring(0, colon).Trim();
                var value = body.Substring(colon + 1);
                listKey = key;
                if (key == AddressKey) address = Scalar(value);
                else if (key == PassKey) pass = Scalar(value);
                else if (key == AliasesKey)
                {
                    var v = value.Trim();
                    if (v.StartsWith("[", StringComparison.Ordinal) && v.EndsWith("]", StringComparison.Ordinal))
                        aliases.AddRange(v.Substring(1, v.Length - 2).Split(',').Select(Scalar).Where(s => s.Length > 0));
                }
            }
            Flush();
            return result;
        }

        public static IReadOnlyList<ImportPlanItem> Plan(ServerList existing, IEnumerable<ImportedServer> servers)
        {
            if (existing == null) throw new ArgumentNullException(nameof(existing));
            var names = new HashSet<string>(existing.Entries.Select(e => e.Name), StringComparer.OrdinalIgnoreCase);
            var addresses = new HashSet<ServerAddress>(existing.Entries.SelectMany(e => e.AllAddresses));
            var plan = new List<ImportPlanItem>();
            foreach (var s in servers)
            {
                ImportOutcome outcome;
                if (s.Address == null || s.Name.Trim().Length == 0) outcome = ImportOutcome.Invalid;
                else if (names.Contains(s.Name.Trim())) outcome = ImportOutcome.SameName;
                else if (new[] { s.Address }.Concat(s.Aliases).Any(addresses.Contains)) outcome = ImportOutcome.AddressTaken;
                else
                {
                    outcome = ImportOutcome.Add;
                    names.Add(s.Name.Trim());
                    foreach (var a in new[] { s.Address }.Concat(s.Aliases)) addresses.Add(a);
                }
                plan.Add(new ImportPlanItem(s, outcome));
            }
            return plan;
        }

        /// <summary>Preview lines (English, for the log and console); a password shows only as "password present".</summary>
        public static IReadOnlyList<string> Preview(IEnumerable<ImportPlanItem> plan) => plan.Select(p =>
        {
            var s = p.Server;
            var aliasNote = s.Aliases.Count == 0 ? "" : " (+" + s.Aliases.Count + (s.Aliases.Count == 1 ? " alias)" : " aliases)");
            var what = p.Outcome switch
            {
                ImportOutcome.Add => "import",
                ImportOutcome.SameName => "skip, a server with this name is saved",
                ImportOutcome.AddressTaken => "skip, this address is saved",
                _ => s.Problem == ImportProblem.MissingAddress ? "skip, no address" : "skip, invalid address",
            };
            return s.Name + "  " + (s.Address?.ToString() ?? "-") + aliasNote + ", " + (s.HasPassword ? "password present" : "no password") + ": " + what;
        }).ToList();

        /// <summary>Adds every planned server to <paramref name="list"/> and stores its password through <paramref name="store"/>.</summary>
        public static ImportResult Apply(ServerList list, ISecretStore store, IEnumerable<ImportPlanItem> plan)
        {
            if (list == null) throw new ArgumentNullException(nameof(list));
            if (store == null) throw new ArgumentNullException(nameof(store));
            int added = 0, skipped = 0;
            var notStored = new List<string>();
            foreach (var item in plan)
            {
                if (item.Outcome != ImportOutcome.Add) { skipped++; continue; }
                var s = item.Server;
                ServerEntry entry;
                try { entry = list.Add(s.Name, s.Address!, s.Aliases); }
                catch (ArgumentException) { skipped++; continue; }
                added++;
                if (s.Password != null && !store.Store(entry.SecretRef, s.Password)) notStored.Add(entry.Name);
            }
            return new ImportResult(added, skipped, notStored);
        }

        /// <summary>
        /// Q2 (a): the same text with every non-empty <c>ServerPass</c> value replaced by <c>''</c>. Everything else, the header
        /// included, is kept byte for byte. No copy of the old values is kept.
        /// </summary>
        public static string BlankPasswords(string yaml, out int blanked)
        {
            blanked = 0;
            var text = yaml ?? "";
            var b = new StringBuilder(text.Length);
            var start = 0;
            while (start <= text.Length)
            {
                var nl = text.IndexOf('\n', start);
                var end = nl < 0 ? text.Length : nl;
                var line = text.Substring(start, end - start);
                var cr = line.EndsWith("\r", StringComparison.Ordinal);
                var content = cr ? line.Substring(0, line.Length - 1) : line;
                var body = content.TrimStart(' ');
                if (content.Length > body.Length && body.StartsWith(PassKey + ":", StringComparison.Ordinal) && Scalar(body.Substring(PassKey.Length + 1)).Length > 0)
                {
                    content = content.Substring(0, content.Length - body.Length) + PassKey + ": ''";
                    blanked++;
                }
                b.Append(content);
                if (cr) b.Append('\r');
                if (nl < 0) break;
                b.Append('\n');
                start = nl + 1;
            }
            return b.ToString();
        }

        static string TopKey(string body)
        {
            if (body[0] == '\'' || body[0] == '"') return Scalar(body);
            for (var i = 0; i < body.Length; i++)
                if (body[i] == ':' && (i + 1 == body.Length || body[i + 1] == ' ')) return body.Substring(0, i).Trim();
            return body.Trim();
        }

        /// <summary>A YAML scalar: single quoted ('' is a quote), double quoted (basic escapes), or plain (a " #" starts a comment).</summary>
        static string Scalar(string raw)
        {
            var s = raw.Trim();
            if (s.Length == 0) return "";
            if (s[0] == '\'')
            {
                var b = new StringBuilder();
                for (var i = 1; i < s.Length; i++)
                {
                    if (s[i] != '\'') { b.Append(s[i]); continue; }
                    if (i + 1 < s.Length && s[i + 1] == '\'') { b.Append('\''); i++; continue; }
                    break;
                }
                return b.ToString();
            }
            if (s[0] == '"')
            {
                var b = new StringBuilder();
                for (var i = 1; i < s.Length; i++)
                {
                    var c = s[i];
                    if (c == '"') break;
                    if (c == '\\' && i + 1 < s.Length)
                    {
                        var e = s[++i];
                        b.Append(e == 'n' ? '\n' : e == 't' ? '\t' : e);
                        continue;
                    }
                    b.Append(c);
                }
                return b.ToString();
            }
            if (s[0] == '#') return "";
            var comment = s.IndexOf(" #", StringComparison.Ordinal);
            return (comment < 0 ? s : s.Substring(0, comment)).Trim();
        }
    }
}
