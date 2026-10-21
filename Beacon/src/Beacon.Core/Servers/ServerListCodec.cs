// Beacon - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: the servers.json format (design 4.1). The file holds no password, no hash and no encoding of one.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using ExtraPotions.Beacon.Secrets;
using ExtraPotions.Beacon.Text;

namespace ExtraPotions.Beacon.Servers
{
    /// <summary>
    /// Reads and writes <c>servers.json</c> (schema 1). Only the fields in <see cref="FieldNames"/> are written; any other
    /// field in a file (for example a password someone typed in by hand) is ignored and disappears on the next save.
    /// </summary>
    public static class ServerListCodec
    {
        public const int Schema = 1;

        public static IReadOnlyList<string> FieldNames { get; } = Array.AsReadOnly(new[] { "schema", "servers", "name", "address", "aliases", "default", "ref" });

        public static string Write(ServerList list)
        {
            if (list == null) throw new ArgumentNullException(nameof(list));
            var b = new StringBuilder();
            b.Append("{\n  \"schema\": ").Append(Schema.ToString(CultureInfo.InvariantCulture)).Append(",\n  \"servers\": [");
            for (var i = 0; i < list.Count; i++)
            {
                var e = list.Entries[i];
                b.Append(i == 0 ? "\n" : ",\n");
                b.Append("    {\"name\": ").Append(MiniJson.Quote(e.Name))
                    .Append(", \"address\": ").Append(MiniJson.Quote(e.Address.ToString()))
                    .Append(", \"aliases\": [").Append(string.Join(", ", e.Aliases.Select(a => MiniJson.Quote(a.ToString())))).Append(']')
                    .Append(", \"default\": ").Append(e.IsDefault ? "true" : "false")
                    .Append(", \"ref\": ").Append(MiniJson.Quote(e.SecretRef)).Append('}');
            }
            b.Append(list.Count == 0 ? "]\n}\n" : "\n  ]\n}\n");
            return b.ToString();
        }

        /// <summary>An empty or whitespace text is an empty list (first start).</summary>
        public static ServerList Read(string text)
        {
            var list = new ServerList();
            if (string.IsNullOrWhiteSpace(text)) return list;
            if (!(MiniJson.Parse(text) is Dictionary<string, object?> root)) throw new FormatException("servers.json: expected an object.");
            if (!(root.TryGetValue("schema", out var schema) && schema is double s && s == Schema)) throw new FormatException("servers.json: unknown schema.");
            if (!root.TryGetValue("servers", out var raw) || raw == null) return list;
            if (!(raw is List<object?> servers)) throw new FormatException("servers.json: 'servers' must be an array.");
            string? defaultName = null;
            foreach (var item in servers)
            {
                if (!(item is Dictionary<string, object?> o)) throw new FormatException("servers.json: a server must be an object.");
                var name = Str(o, "name") ?? throw new FormatException("servers.json: a server has no name.");
                var address = Address(Str(o, "address"));
                var aliases = new List<ServerAddress>();
                if (o.TryGetValue("aliases", out var al) && al is List<object?> alList)
                    foreach (var a in alList) aliases.Add(Address(a as string));
                var reference = Str(o, "ref");
                if (!SecretReference.IsValid(reference)) throw new FormatException("servers.json: server '" + name + "' has no valid ref.");
                try
                {
                    list.Insert(name, address, aliases, reference);
                }
                catch (ArgumentException error)
                {
                    throw new FormatException("servers.json: " + error.Message);
                }
                if (o.TryGetValue("default", out var d) && d is bool isDefault && isDefault) defaultName = name;
            }
            if (defaultName != null) list.SetDefault(defaultName);
            return list;
        }

        static string? Str(Dictionary<string, object?> o, string key) => o.TryGetValue(key, out var v) ? v as string : null;

        static ServerAddress Address(string? text) =>
            ServerAddress.TryParse(text, out var a) ? a! : throw new FormatException("servers.json: invalid address.");
    }
}
