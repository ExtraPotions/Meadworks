// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
namespace ExtraPotions.Hex;

public enum HexSpawnKind : byte { Item = 1, Creature = 2 }
public sealed class HexSpawnRequest
{
    public HexSpawnKind Kind { get; }
    public string Prefab { get; }
    public int Amount { get; }
    public int Level { get; }
    public bool Tamed { get; }
    public bool Inventory { get; }
    public HexSpawnRequest(HexSpawnKind kind, string prefab, int amount, int level, bool tamed, bool inventory)
    { Kind = kind; Prefab = prefab; Amount = amount; Level = level; Tamed = tamed; Inventory = inventory; }
}
public sealed class HexSpawnReply
{
    public string Receipt { get; }
    public int Created { get; }
    public bool Partial { get; }
    public HexSpawnReply(string receipt, int created, bool partial) { Receipt = receipt; Created = created; Partial = partial; }
}

// Bounded product payload on the existing Crucible Operation transport; no independent network protocol.
public static class HexSpawnData
{
    public const string OperationId = "meadworks.hex.spawn", AuditCapability = "meadworks.thing.hex_spawn_audit";
    public static readonly Version Version = new(1, 0);
    public const int MaxAmount = 9999, MaxCreatureLevel = 100;
    public static bool PrefabValid(string prefab) => !string.IsNullOrWhiteSpace(prefab) && prefab.Length <= 160 && prefab.All(c => !char.IsControl(c));
    public static bool Valid(HexSpawnRequest request) => request != null && Enum.IsDefined(typeof(HexSpawnKind), request.Kind)
        && PrefabValid(request.Prefab) && request.Amount >= 1 && request.Amount <= MaxAmount && request.Level >= 1
        && (request.Kind == HexSpawnKind.Item ? !request.Tamed : request.Level <= MaxCreatureLevel && !request.Inventory);
    public static bool Supported(HexSpawnRequest request, int itemMaxQuality, bool tameable)
        => Valid(request) && (request.Kind == HexSpawnKind.Item ? request.Level <= itemMaxQuality : !request.Tamed || tameable);
    static bool Flag(BinaryReader reader) { var value = reader.ReadByte(); return value <= 1 ? value == 1 : throw new FormatException("Invalid spawn flag."); }
    static BinaryReader Reader(byte[] bytes, int maximum)
    {
        if (bytes == null || bytes.Length < 2 || bytes.Length > maximum) throw new FormatException("Invalid spawn payload length.");
        var reader = new BinaryReader(new MemoryStream(bytes, false), new UTF8Encoding(false, true));
        if (reader.ReadByte() != 1) { reader.Dispose(); throw new FormatException("Invalid spawn format."); }
        return reader;
    }
    public static byte[] Encode(HexSpawnRequest request)
    {
        if (!Valid(request)) throw new ArgumentException("Invalid spawn request.");
        using var stream = new MemoryStream(); using var writer = new BinaryWriter(stream, Encoding.UTF8, true);
        writer.Write((byte)1); writer.Write((byte)request.Kind); writer.Write(request.Prefab);
        writer.Write(request.Amount); writer.Write(request.Level); writer.Write(request.Tamed); writer.Write(request.Inventory);
        return stream.ToArray();
    }
    public static HexSpawnRequest Decode(byte[] bytes)
    {
        using var reader = Reader(bytes, 1024);
        var request = new HexSpawnRequest((HexSpawnKind)reader.ReadByte(), reader.ReadString(), reader.ReadInt32(), reader.ReadInt32(), Flag(reader), Flag(reader));
        if (!Valid(request) || reader.BaseStream.Position != reader.BaseStream.Length) throw new FormatException("Invalid spawn request.");
        return request;
    }
    static bool Valid(HexSpawnReply reply) => reply != null && Guid.TryParseExact(reply.Receipt, "N", out _) && reply.Created >= 0 && reply.Created <= MaxAmount;
    public static byte[] Encode(HexSpawnReply reply)
    {
        if (!Valid(reply)) throw new ArgumentException("Invalid spawn reply.");
        using var stream = new MemoryStream(); using var writer = new BinaryWriter(stream, Encoding.UTF8, true);
        writer.Write((byte)1); writer.Write(reply.Receipt); writer.Write(reply.Created); writer.Write(reply.Partial);
        return stream.ToArray();
    }
    public static HexSpawnReply DecodeReply(byte[] bytes)
    {
        using var reader = Reader(bytes, 128);
        var reply = new HexSpawnReply(reader.ReadString(), reader.ReadInt32(), Flag(reader));
        if (!Valid(reply) || reader.BaseStream.Position != reader.BaseStream.Length) throw new FormatException("Invalid spawn reply.");
        return reply;
    }
    static readonly Regex Markup = new(@"</?(?:color|b|i|u|s|size|material|quad|sprite|link|mark|align|alpha|font|style|rotate|sup|sub|nobr|noparse|cspace|mspace|pos|space|indent|margin|line-height|line-indent|lowercase|uppercase|smallcaps|voffset|width|gradient|page)(?:[=\s][^<>]*)?>",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);
    static readonly Regex Unresolved = new(@"^(?:\$[A-Za-z0-9_.]+|\[[A-Za-z0-9_.]+\])$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    /// <summary>
    /// The name a catalog tile shows: rich text markup removed, control and zero-width characters turned into spaces, an unresolved
    /// localization token treated as missing, and the prefab name used when nothing readable is left.
    /// </summary>
    public static string DisplayName(string? localized, string prefab)
    {
        var text = Markup.Replace(localized ?? "", "");
        var plain = new StringBuilder(text.Length);
        foreach (var ch in text)
            plain.Append(char.IsControl(ch) || CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.Format ? ' ' : ch);
        var name = Regex.Replace(plain.ToString(), @"\s+", " ").Trim();
        return name.Length == 0 || Unresolved.IsMatch(name) ? prefab : name;
    }
    public static string Fold(string value)
    {
        var result = new StringBuilder();
        foreach (var ch in (value ?? "").Normalize(NormalizationForm.FormD))
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark && !char.IsWhiteSpace(ch) && ch != '_' && ch != '-') result.Append(char.ToUpperInvariant(ch));
        return result.ToString().Normalize(NormalizationForm.FormC);
    }
}
