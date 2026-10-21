// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
namespace ExtraPotions.Hex;

public readonly struct HexKillReply
{
    public int Targets { get; }
    public int Submitted { get; }
    public int Skipped { get; }
    public HexKillReply(int targets, int submitted, int skipped) { Targets = targets; Submitted = submitted; Skipped = skipped; }
}

/// <summary>Confirmed area request. The radius is meters, from 5 to 1000.</summary>
public readonly struct HexAreaRequest
{
    public float Radius { get; }
    public HexAreaRequest(float radius) { Radius = radius; }
}

// Confirmed area payload on the existing Crucible Operations transport. Kill, drops and tame share it. No target list is accepted from the client.
public static class HexKillData
{
    public const string OperationId = "meadworks.hex.debug_kill", DropsOperationId = "meadworks.hex.debug_drops",
        TameOperationId = "meadworks.hex.debug_tame", EventOperationId = "meadworks.hex.debug_event";
    public const int MaxTargets = 512;
    // Crucible routes remote operations at protocol 1.0 only. The request payload carries its own format byte (2), so a
    // pre-radius request is refused by DecodeRequest.
    public static readonly Version Version = new(1, 0);
    public static byte[] EncodeRequest(HexAreaRequest request)
    {
        if (!HexOwnerData.ValidRadius(request.Radius)) throw new ArgumentException("Invalid radius.");
        using var stream = new MemoryStream(); using var writer = new BinaryWriter(stream);
        writer.Write((byte)2); writer.Write((byte)1); writer.Write(request.Radius); return stream.ToArray();
    }
    public static HexAreaRequest DecodeRequest(byte[] bytes)
    {
        if (bytes == null || bytes.Length != 6 || bytes[0] != 2 || bytes[1] != 1) throw new FormatException("Invalid kill request.");
        using var reader = new BinaryReader(new MemoryStream(bytes, false)); reader.ReadByte(); reader.ReadByte();
        var radius = reader.ReadSingle();
        return HexOwnerData.ValidRadius(radius) ? new HexAreaRequest(radius) : throw new FormatException("Invalid radius.");
    }
    public static byte[] EncodeEvent(bool confirmed) => confirmed ? new byte[] { 1, 1 } : throw new ArgumentException("Event confirmation required.");
    public static bool DecodeEvent(byte[] bytes) => bytes != null && bytes.Length == 2 && bytes[0] == 1 && bytes[1] == 1
        ? true : throw new FormatException("Invalid event request.");
    static bool Valid(HexKillReply value) => value.Targets >= 0 && value.Targets <= MaxTargets && value.Submitted >= 0
        && value.Skipped >= 0 && value.Submitted <= value.Targets && value.Skipped == value.Targets - value.Submitted;
    public static byte[] EncodeReply(HexKillReply value)
    {
        if (!Valid(value)) throw new ArgumentException("Invalid kill result.");
        using var stream = new MemoryStream(); using var writer = new BinaryWriter(stream);
        writer.Write((byte)1); writer.Write(value.Targets); writer.Write(value.Submitted); writer.Write(value.Skipped); return stream.ToArray();
    }
    public static HexKillReply DecodeReply(byte[] bytes)
    {
        if (bytes == null || bytes.Length != 13 || bytes[0] != 1) throw new FormatException("Invalid kill result.");
        using var reader = new BinaryReader(new MemoryStream(bytes, false)); reader.ReadByte();
        var value = new HexKillReply(reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32());
        return Valid(value) ? value : throw new FormatException("Invalid kill counts.");
    }
}
