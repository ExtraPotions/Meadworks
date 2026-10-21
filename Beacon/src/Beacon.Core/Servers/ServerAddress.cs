// Beacon - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original, behavior only: the address forms follow AutoConnector 1.0.0's documented file format (port 2456 when
// omitted, games hosted by a player as steam:<id> or playfab:<id>); no AutoConnector code was read.
using System;
using System.Globalization;

namespace ExtraPotions.Beacon.Servers
{
    public enum AddressKind { Host, Steam, PlayFab }

    /// <summary>
    /// One normalized server address (design F5, F6): host names lower case, port 2456 when omitted, IPv6 written in
    /// brackets with its port, hosted games as <c>steam:&lt;digits&gt;</c> or <c>playfab:&lt;ID&gt;</c> (upper case, data only).
    /// Equality is by the canonical text.
    /// </summary>
    public sealed class ServerAddress : IEquatable<ServerAddress>
    {
        public const int DefaultPort = 2456;

        ServerAddress(AddressKind kind, string host, int port)
        {
            Kind = kind;
            Host = host;
            Port = port;
        }

        public AddressKind Kind { get; }

        /// <summary>Host name or IP for <see cref="AddressKind.Host"/>; the platform id for hosted games.</summary>
        public string Host { get; }

        /// <summary>Port for <see cref="AddressKind.Host"/>; 0 for hosted games.</summary>
        public int Port { get; }

        public static ServerAddress Parse(string text) =>
            TryParse(text, out var address) ? address! : throw new FormatException("Not a server address.");

        public static bool TryParse(string? text, out ServerAddress? address)
        {
            address = null;
            var t = (text ?? "").Trim();
            if (t.Length == 0) return false;
            var lower = t.ToLowerInvariant();
            if (lower.StartsWith("steam:", StringComparison.Ordinal))
            {
                var id = t.Substring(6);
                if (id.Length == 0 || id.Length > 20 || !AllDigits(id)) return false;
                address = new ServerAddress(AddressKind.Steam, id, 0);
                return true;
            }
            if (lower.StartsWith("playfab:", StringComparison.Ordinal))
            {
                var id = t.Substring(8);
                if (id.Length == 0 || id.Length > 64 || !AllAlphaNumeric(id)) return false;
                address = new ServerAddress(AddressKind.PlayFab, id.ToUpperInvariant(), 0);
                return true;
            }
            string host;
            var port = DefaultPort;
            if (lower[0] == '[')
            {
                var close = lower.IndexOf(']');
                if (close < 2) return false;
                host = lower.Substring(1, close - 1);
                var rest = lower.Substring(close + 1);
                if (rest.Length > 0 && (rest[0] != ':' || !TryPort(rest.Substring(1), out port))) return false;
                if (host.IndexOf(':') < 0) return false;
            }
            else
            {
                var colons = Count(lower, ':');
                if (colons > 1)
                {
                    host = lower; // bare IPv6: no port possible
                }
                else if (colons == 1)
                {
                    var at = lower.IndexOf(':');
                    host = lower.Substring(0, at);
                    if (!TryPort(lower.Substring(at + 1), out port)) return false;
                }
                else host = lower;
            }
            if (!ValidHost(host)) return false;
            address = new ServerAddress(AddressKind.Host, host, port);
            return true;
        }

        public override string ToString() => Kind switch
        {
            AddressKind.Steam => "steam:" + Host,
            AddressKind.PlayFab => "playfab:" + Host,
            _ => (Host.IndexOf(':') >= 0 ? "[" + Host + "]" : Host) + ":" + Port.ToString(CultureInfo.InvariantCulture),
        };

        public bool Equals(ServerAddress? other) => other is object && string.Equals(ToString(), other.ToString(), StringComparison.Ordinal);

        public override bool Equals(object? obj) => Equals(obj as ServerAddress);

        public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(ToString());

        public static bool operator ==(ServerAddress? a, ServerAddress? b) => a is null ? b is null : a.Equals(b);

        public static bool operator !=(ServerAddress? a, ServerAddress? b) => !(a == b);

        static bool TryPort(string text, out int port) =>
            int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out port) && port >= 1 && port <= 65535;

        static int Count(string s, char c)
        {
            var n = 0;
            foreach (var x in s) if (x == c) n++;
            return n;
        }

        static bool AllDigits(string s)
        {
            foreach (var c in s) if (c < '0' || c > '9') return false;
            return true;
        }

        static bool AllAlphaNumeric(string s)
        {
            foreach (var c in s) if (!(c >= '0' && c <= '9' || c >= 'a' && c <= 'z' || c >= 'A' && c <= 'Z')) return false;
            return true;
        }

        static bool ValidHost(string host)
        {
            if (host.Length == 0 || host.Length > 253) return false;
            var ipv6 = host.IndexOf(':') >= 0;
            foreach (var c in host)
            {
                var ok = ipv6
                    ? c >= '0' && c <= '9' || c >= 'a' && c <= 'f' || c == ':' || c == '.'
                    : c >= '0' && c <= '9' || c >= 'a' && c <= 'z' || c == '.' || c == '-' || c == '_';
                if (!ok) return false;
            }
            return true;
        }
    }
}
