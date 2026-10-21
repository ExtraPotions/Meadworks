// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: bounded data-only metadata/value frames for the existing authenticated Thing operation.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using ExtraPotions.Thing.Communication;
using ExtraPotions.Thing.ConfigEditing.Attributes;
using ExtraPotions.Thing.ConfigEditing.Editors;

namespace ExtraPotions.Thing.RemoteConfig
{
    public sealed class ConfigEditRequest
    {
        public string Verb { get; private set; } = "";
        public string File { get; private set; } = "";
        public string Entry { get; private set; } = "";
        public string Version { get; private set; } = "";
        public string Value { get; private set; } = "";
        public int Offset { get; private set; }
        public string Token { get; private set; } = "";
        public int Total { get; private set; }
        public byte[] Bytes { get; private set; } = Array.Empty<byte>();
        public IReadOnlyList<string> ListValues { get; private set; } = Array.Empty<string>();
        public ConfigEditRequest WithValue(string value) => new ConfigEditRequest { Verb = "set", File = File, Entry = Entry, Version = Version, Value = value };
        public bool Writes => Verb == "list-set" || Verb == "set" || Verb == "yaml-begin" || Verb == "yaml-put" || Verb == "yaml-commit";
        public static bool TryParse(IReadOnlyList<string> args, out ConfigEditRequest request)
        {
            request = null!;
            if (args == null || args.Count == 0) return false;
            var verb = args[0].ToLowerInvariant();
            var item = new ConfigEditRequest();
            if (verb == "set")
            {
                if (args.Count < 5 || !MessageEditing.Version(args[1]) || !MessageEditing.Version(args[2]) || !MessageEditing.Version(args[3])) return false;
                try { item.Value = ConfigWire.Unpack(args.Skip(4).ToArray(), 4096); } catch { return false; }
                item.Verb = "set"; item.File = args[1]; item.Entry = args[2]; item.Version = args[3]; request = item; return true;
            }
            if (verb != "config" || args.Count < 2) return false;
            item.Verb = args[1].ToLowerInvariant();
            if (item.Verb == "list-get" || item.Verb == "list-set")
            {
                if (args.Count < 4 || !MessageEditing.Version(args[2]) || !MessageEditing.Version(args[3])) return false;
                item.File = args[2]; item.Entry = args[3];
                if (item.Verb == "list-get") { if (args.Count != 4) return false; }
                else
                {
                    if (args.Count < 6 || !MessageEditing.Version(args[4])) return false; item.Version = args[4];
                    try { item.ListValues = ConfigListDocument.UnpackValues(args.Skip(5).ToArray()); } catch { return false; }
                }
                request = item; return true;
            }
            if (item.Verb.StartsWith("yaml-", StringComparison.Ordinal))
            {
                if (args.Count < 3 || !MessageEditing.Version(args[2])) return false;
                item.Token = args[2];
                switch (item.Verb)
                {
                    case "yaml-open": if (args.Count != 3) return false; item.File = args[2]; break;
                    case "yaml-cancel": case "yaml-commit": if (args.Count != 3) return false; break;
                    case "yaml-read":
                        if (args.Count != 4 || !int.TryParse(args[3], NumberStyles.None, CultureInfo.InvariantCulture, out var atByte) || atByte < 0 || atByte > YamlTransfer.MaxBytes) return false;
                        item.Offset = atByte; break;
                    case "yaml-begin":
                        if (args.Count != 5 || !int.TryParse(args[3], NumberStyles.None, CultureInfo.InvariantCulture, out var length) || length < 0 || length > YamlTransfer.MaxBytes || !MessageEditing.Version(args[4])) return false;
                        item.Total = length; item.Version = args[4]; break;
                    case "yaml-put":
                        if (args.Count < 5 || !int.TryParse(args[3], NumberStyles.None, CultureInfo.InvariantCulture, out var yamlOffset) || yamlOffset < 0 || yamlOffset > YamlTransfer.MaxBytes) return false;
                        try { item.Bytes = YamlTransfer.Unpack(args.Skip(4).ToArray()); } catch { return false; }
                        if (item.Bytes.Length == 0) return false; item.Offset = yamlOffset; break;
                    default: return false;
                }
                request = item; return true;
            }
            if (item.Verb == "list")
            { if (args.Count != 2 && args.Count != 3) return false; }
            else if (item.Verb == "entries")
            { if ((args.Count != 3 && args.Count != 4) || !MessageEditing.Version(args[2])) return false; item.File = args[2]; }
            else if (item.Verb == "get")
            { if (args.Count != 4 || !MessageEditing.Version(args[2]) || !MessageEditing.Version(args[3])) return false; item.File = args[2]; item.Entry = args[3]; request = item; return true; }
            else return false;
            var at = item.Verb == "list" ? 2 : 3;
            if (args.Count > at && (!int.TryParse(args[at], NumberStyles.None, CultureInfo.InvariantCulture, out var offset) || offset < 0 || offset >= UI.ThingUiPage.MaxTotal)) return false;
            item.Offset = args.Count > at ? int.Parse(args[at], CultureInfo.InvariantCulture) : 0;
            request = item; return true;
        }
    }
    public sealed class ConfigEntryDocument
    {
        public string File = "", Entry = "", Version = "", Section = "", Key = "", Description = "", Current = "", Default = "", Reason = "", Effective = "";
        public bool Editable;
        public SettingShape Shape = SettingShape.String();
        public EntryAttributes Attributes = AttributeReader.Read(Array.Empty<TagObject>());
        public IReadOnlyList<string> Encode()
        {
            if (!MessageEditing.Version(File) || !MessageEditing.Version(Entry) || !MessageEditing.Version(Version)) throw new FormatException("Invalid config document identity.");
            using (var stream = new MemoryStream())
            {
                using (var writer = new BinaryWriter(stream, ConfigWire.Utf8, true))
                {
                    writer.Write((byte)1);
                    foreach (var value in new[] { File, Entry, Version, Section, Key, Description, Current, Default, Reason, Effective }) writer.Write(value);
                    writer.Write(Editable); writer.Write((int)Shape.Kind); writer.Write(Shape.TypeName); writer.Write(Shape.HasConverter); writer.Write(Shape.IsFlags);
                    writer.Write(Shape.HasRange); writer.Write(Shape.RangeMin); writer.Write(Shape.RangeMax);
                    WriteList(writer, Shape.EnumNames); WriteList(writer, Shape.AcceptableList); WriteList(writer, Shape.KeyNames);
                    writer.Write(Attributes.RequiresRestart); writer.Write(Attributes.Multiline); writer.Write(Attributes.ListSeparator ?? "");
                    writer.Write(Attributes.Unit ?? ""); writer.Write(Attributes.Step.HasValue); writer.Write(Attributes.Step ?? 0); writer.Write(Attributes.ShowRangeAsPercent == true);
                }
                if (stream.Length > ConfigWire.MaxDocumentBytes) throw new FormatException("Config metadata exceeds one bounded frame.");
                return new[] { ConfigWire.Marker }.Concat(ConfigWire.Pack(stream.ToArray())).ToArray();
            }
        }
        static void WriteList(BinaryWriter writer, IReadOnlyList<string>? list)
        { if (list?.Count > 512) throw new FormatException("Too many metadata choices."); writer.Write(list?.Count ?? -1); if (list != null) foreach (var item in list) writer.Write(item); }
        static string[]? ReadList(BinaryReader reader)
        { var count = reader.ReadInt32(); if (count == -1) return null; if (count < 0 || count > 512) throw new FormatException(); var list = new string[count]; for (var i = 0; i < count; i++) list[i] = reader.ReadString(); return list; }
        public static bool TryDecode(IReadOnlyList<string> lines, out ConfigEntryDocument document)
        {
            document = null!;
            if (lines == null || lines.Count < 2 || lines.Count > 16 || lines[0] != ConfigWire.Marker) return false;
            try
            {
                var bytes = ConfigWire.UnpackBytes(lines.Skip(1).ToArray(), ConfigWire.MaxDocumentBytes);
                using (var stream = new MemoryStream(bytes)) using (var reader = new BinaryReader(stream, ConfigWire.Utf8))
                {
                    if (reader.ReadByte() != 1) return false;
                    var d = new ConfigEntryDocument { File = reader.ReadString(), Entry = reader.ReadString(), Version = reader.ReadString(), Section = reader.ReadString(), Key = reader.ReadString(),
                        Description = reader.ReadString(), Current = reader.ReadString(), Default = reader.ReadString(), Reason = reader.ReadString(), Effective = reader.ReadString(), Editable = reader.ReadBoolean() };
                    if (!MessageEditing.Version(d.File) || !MessageEditing.Version(d.Entry) || !MessageEditing.Version(d.Version) || d.Current.Length > 4096 || d.Default.Length > 4096) return false;
                    var kind = (SettingKind)reader.ReadInt32(); if (!Enum.IsDefined(typeof(SettingKind), kind)) return false;
                    var type = reader.ReadString(); var converter = reader.ReadBoolean(); var flags = reader.ReadBoolean(); var ranged = reader.ReadBoolean(); var min = reader.ReadDouble(); var max = reader.ReadDouble();
                    var names = ReadList(reader); var choices = ReadList(reader); var keys = ReadList(reader);
                    d.Shape = kind == SettingKind.Enum ? SettingShape.Enum(type, names ?? Array.Empty<string>(), flags) : SettingShape.Of(kind, type, converter);
                    if (ranged) { if (double.IsNaN(min) || double.IsInfinity(min) || double.IsNaN(max) || double.IsInfinity(max)) return false; d.Shape = d.Shape.WithRange(min, max); }
                    if (choices != null) d.Shape = d.Shape.WithList(choices); if (keys != null) d.Shape = d.Shape.WithKeyNames(keys);
                    d.Attributes.RequiresRestart = reader.ReadBoolean(); d.Attributes.Multiline = reader.ReadBoolean();
                    var separator = reader.ReadString(); d.Attributes.ListSeparator = separator.Length == 0 ? null : separator;
                    var unit = reader.ReadString(); d.Attributes.Unit = unit.Length == 0 ? null : unit;
                    var hasStep = reader.ReadBoolean(); var step = reader.ReadDouble();
                    if (hasStep) { if (double.IsNaN(step) || double.IsInfinity(step) || step <= 0) return false; d.Attributes.Step = step; }
                    d.Attributes.ShowRangeAsPercent = reader.ReadBoolean();
                    if (stream.Position != stream.Length) return false;
                    document = d; return true;
                }
            }
            catch { return false; }
        }
    }
    public static class ConfigWire
    {
        public const string Marker = "THING_CONFIG_ENTRY";
        public const int MaxDocumentBytes = 7168;
        internal static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);
        public static IReadOnlyList<string> PackValue(string value) => Pack(Utf8.GetBytes(value ?? ""));
        public static IReadOnlyList<string> WriteEcho(string version, string value)
        {
            if (!MessageEditing.Version(version) || Utf8.GetByteCount(value) > 4096) throw new FormatException();
            return new[] { "THING_CONFIG_WRITE\t" + version }.Concat(PackValue(value)).ToArray();
        }
        public static bool TryWriteEcho(IReadOnlyList<string> lines, out string version, out string value)
        {
            version = value = "";
            if (lines == null || lines.Count < 2 || lines.Count > 16) return false;
            var head = lines[0].Split('\t');
            if (head.Length != 2 || head[0] != "THING_CONFIG_WRITE" || !MessageEditing.Version(head[1])) return false;
            try { value = Unpack(lines.Skip(1).ToArray(), 4096); version = head[1]; return true; } catch { return false; }
        }
        internal static IReadOnlyList<string> Pack(byte[] bytes)
        {
            var text = Convert.ToBase64String(bytes); var parts = new List<string>();
            for (var i = 0; i < text.Length; i += 700) parts.Add(text.Substring(i, Math.Min(700, text.Length - i)));
            if (parts.Count == 0) parts.Add(""); return parts;
        }
        internal static string Unpack(IReadOnlyList<string> parts, int max) => Utf8.GetString(UnpackBytes(parts, max));
        internal static byte[] UnpackBytes(IReadOnlyList<string> parts, int max)
        {
            if (parts.Count < 1 || parts.Count > 15 || parts.Any(p => p == null || p.Length > 700)) throw new FormatException();
            var bytes = Convert.FromBase64String(string.Concat(parts)); if (bytes.Length > max) throw new FormatException(); return bytes;
        }
    }
}
