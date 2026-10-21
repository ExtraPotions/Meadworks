// Thing - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: Temporary remote admin path for the `thing` verbs (user decision 2026-10-08, Thing item 1 changed to b;
// same pattern as Tempest's RemoteCommand). REMOVE WHEN CRUCIBLE CS-D1 (admin command relay) LANDS: delete this file and the
// plugin's ThingRemoteCommands, and send the verbs through CS-D1 instead.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using ExtraPotions.Thing.Localization;

namespace ExtraPotions.Thing.Commands
{
    /// <summary>
    /// The Crucible Operations request a remote client sends for a <c>thing</c> verb (the console arguments as typed), the
    /// reply (the lines the server printed), and the server's first refusal check. The server then parses the arguments
    /// itself and runs the same admin decision (<c>AdminCheck</c>) as its own console, with the caller as the admin.
    /// </summary>
    public static class RemoteAdmin
    {
        public const string OperationId = "meadworks.thing.admin_command";
        public static readonly Version OperationVersion = new Version(1, 0);
        const byte Format = 1;

        public const int MaxArguments = 64;
        public const int MaxArgumentBytes = 1024;
        public const int MaxReplyLines = 16;

        public const string Inactive = "ThingInactive";
        public const string NotAdmin = "NotAdmin";

        /// <summary>Only verbs the server executes travel; help, usage errors and unknown verbs are answered locally.</summary>
        public static bool Sendable(ParsedCommand parsed) => parsed != null && (parsed.Kind == CommandKind.Action || parsed.Kind == CommandKind.Status);

        public static byte[] EncodeRequest(IReadOnlyList<string> args)
        {
            if (args == null || args.Count > MaxArguments) throw new ArgumentException("Too many arguments.", nameof(args));
            return Encode(args.Select(a =>
            {
                var bytes = Encoding.UTF8.GetBytes(a ?? "");
                if (bytes.Length > MaxArgumentBytes) throw new ArgumentException("Argument too long.", nameof(args));
                return bytes;
            }).ToList());
        }

        public static IReadOnlyList<string> DecodeRequest(byte[] bytes)
        {
            var list = Decode(bytes, MaxArguments);
            if (list.Count == 0) throw new FormatException("Thing admin command without a verb");
            return list;
        }

        /// <summary>Encodes at most <see cref="MaxReplyLines"/> lines; longer lines are cut to the argument limit.</summary>
        public static byte[] EncodeReply(IEnumerable<string> lines) =>
            Encode((lines ?? Enumerable.Empty<string>()).Take(MaxReplyLines).Select(l =>
            {
                var b = Encoding.UTF8.GetBytes(l ?? "");
                return b.Length > MaxArgumentBytes ? Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(b, 0, MaxArgumentBytes).TrimEnd('�')) : b;
            }).ToList());

        public static IReadOnlyList<string> DecodeReply(byte[] bytes) => Decode(bytes, MaxReplyLines);

        /// <summary>The server's refusal code before parsing, or null: Thing must be active there, and the caller a server admin by platform ID (C02).</summary>
        public static string? Refusal(bool productActive, bool isAdmin)
        {
            if (!productActive) return Inactive;
            if (!isAdmin) return NotAdmin;
            return null;
        }

        /// <summary>The catalog key shown to the caller for a refusal or transport code.</summary>
        public static string TextKey(string? code)
        {
            switch (code)
            {
                case Inactive: return "refused_product_off";
                case NotAdmin: return "refused_not_admin";
                default: return ThingTextKeys.CommandRemoteFailed;
            }
        }

        static byte[] Encode(IReadOnlyList<byte[]> parts)
        {
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(Format);
                writer.Write((byte)parts.Count);
                foreach (var p in parts)
                {
                    writer.Write((ushort)p.Length);
                    writer.Write(p);
                }
                writer.Flush();
                return stream.ToArray();
            }
        }

        static IReadOnlyList<string> Decode(byte[] bytes, int maxCount)
        {
            if (bytes == null || bytes.Length < 2 || bytes[0] != Format) throw new FormatException("Thing admin command format");
            var count = bytes[1];
            if (count > maxCount) throw new FormatException("Thing admin command count");
            var result = new List<string>(count);
            var at = 2;
            var strict = new UTF8Encoding(false, true);
            for (var i = 0; i < count; i++)
            {
                if (at + 2 > bytes.Length) throw new FormatException("Thing admin command truncated");
                var length = bytes[at] | (bytes[at + 1] << 8);
                at += 2;
                if (length > MaxArgumentBytes || at + length > bytes.Length) throw new FormatException("Thing admin command length");
                try { result.Add(strict.GetString(bytes, at, length)); }
                catch (ArgumentException) { throw new FormatException("Thing admin command text"); }
                at += length;
            }
            if (at != bytes.Length) throw new FormatException("Thing admin command trailing bytes");
            return result;
        }
    }
}
