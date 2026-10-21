// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
namespace ExtraPotions.Hex;

// Remote debug kill and drops. A dedicated server holds the world as ZDOs and instantiates almost none of it, so the server
// authorizes the request, lists the targets by ZDO, and hands each owner its own targets; the owner acts on the live instance.
public enum HexOwnerAction : byte { Kill = 1, Drops = 2 }

public readonly struct HexTargetId : IEquatable<HexTargetId>
{
    public long User { get; }
    public uint Id { get; }
    public HexTargetId(long user, uint id) { User = user; Id = id; }
    public bool Equals(HexTargetId other) => User == other.User && Id == other.Id;
    public override bool Equals(object? obj) => obj is HexTargetId other && Equals(other);
    public override int GetHashCode() => unchecked((User.GetHashCode() * 397) ^ (int)Id);
    public override string ToString() => User + ":" + Id;
}

public sealed class HexOwnerCommand
{
    public HexOwnerAction Action { get; }
    public float X { get; }
    public float Y { get; }
    public float Z { get; }
    public IReadOnlyList<HexTargetId> Targets { get; }
    public HexOwnerCommand(HexOwnerAction action, float x, float y, float z, IReadOnlyList<HexTargetId> targets)
    { Action = action; X = x; Y = y; Z = z; Targets = targets; }
}

public readonly struct HexOwnerReply
{
    public int Applied { get; }
    public int Skipped { get; }
    public HexOwnerReply(int applied, int skipped) { Applied = applied; Skipped = skipped; }
}

public static class HexOwnerData
{
    public const string CommandId = "meadworks.hex.debug_owner";
    public const int MaxBatch = 64;
    public const float KillRadius = 1000f;
    // About the area a client keeps loaded around itself; the native removedrops command only reaches loaded drops.
    public const float DropsRadius = 128f;
    public static readonly Version Version = new(1, 0);
    static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    static bool Defined(HexOwnerAction action) => action == HexOwnerAction.Kill || action == HexOwnerAction.Drops;
    public static float Radius(HexOwnerAction action) => action == HexOwnerAction.Drops ? DropsRadius : KillRadius;
    /// <summary>Distance check that refuses non-finite positions instead of letting NaN compare as in range.</summary>
    public static bool Within(float cx, float cy, float cz, float x, float y, float z, float radius)
    {
        if (!Finite(cx) || !Finite(cy) || !Finite(cz) || !Finite(x) || !Finite(y) || !Finite(z)) return false;
        double dx = x - cx, dy = y - cy, dz = z - cz;
        return dx * dx + dy * dy + dz * dz <= (double)radius * radius;
    }
    public static byte[] Encode(HexOwnerCommand command)
    {
        if (command == null || !Defined(command.Action) || !Finite(command.X) || !Finite(command.Y) || !Finite(command.Z) || command.Targets == null
            || command.Targets.Count < 1 || command.Targets.Count > MaxBatch || command.Targets.Distinct().Count() != command.Targets.Count)
            throw new ArgumentException("Invalid owner command.");
        using var stream = new MemoryStream(); using var writer = new BinaryWriter(stream);
        writer.Write((byte)1); writer.Write((byte)command.Action); writer.Write(command.X); writer.Write(command.Y); writer.Write(command.Z);
        writer.Write((ushort)command.Targets.Count);
        foreach (var target in command.Targets) { writer.Write(target.User); writer.Write(target.Id); }
        return stream.ToArray();
    }
    public static HexOwnerCommand Decode(byte[] bytes)
    {
        if (bytes == null || bytes.Length < 16 || bytes[0] != 1 || !Defined((HexOwnerAction)bytes[1])) throw new FormatException("Invalid owner command.");
        using var reader = new BinaryReader(new MemoryStream(bytes, false)); reader.ReadByte();
        var action = (HexOwnerAction)reader.ReadByte(); var x = reader.ReadSingle(); var y = reader.ReadSingle(); var z = reader.ReadSingle();
        int count = reader.ReadUInt16();
        if (!Finite(x) || !Finite(y) || !Finite(z) || count < 1 || count > MaxBatch || bytes.Length != 16 + count * 12) throw new FormatException("Invalid owner command.");
        var targets = new List<HexTargetId>(count);
        for (var i = 0; i < count; i++) targets.Add(new HexTargetId(reader.ReadInt64(), reader.ReadUInt32()));
        if (targets.Distinct().Count() != count) throw new FormatException("Repeated owner target.");
        return new HexOwnerCommand(action, x, y, z, targets);
    }
    static bool Valid(int applied, int skipped) => applied >= 0 && skipped >= 0 && applied <= MaxBatch && skipped <= MaxBatch && applied + skipped >= 1 && applied + skipped <= MaxBatch;
    public static byte[] EncodeReply(HexOwnerReply reply)
    {
        if (!Valid(reply.Applied, reply.Skipped)) throw new ArgumentException("Invalid owner reply.");
        using var stream = new MemoryStream(); using var writer = new BinaryWriter(stream);
        writer.Write((byte)1); writer.Write(reply.Applied); writer.Write(reply.Skipped); return stream.ToArray();
    }
    public static HexOwnerReply DecodeReply(byte[] bytes)
    {
        if (bytes == null || bytes.Length != 9 || bytes[0] != 1) throw new FormatException("Invalid owner reply.");
        using var reader = new BinaryReader(new MemoryStream(bytes, false)); reader.ReadByte();
        var applied = reader.ReadInt32(); var skipped = reader.ReadInt32();
        return Valid(applied, skipped) ? new HexOwnerReply(applied, skipped) : throw new FormatException("Invalid owner counts.");
    }
}

public readonly struct HexOwnedTarget
{
    public long Owner { get; }
    public HexTargetId Id { get; }
    public HexOwnedTarget(long owner, HexTargetId id) { Owner = owner; Id = id; }
}

public sealed class HexOwnerBatch
{
    public long Owner { get; }
    public IReadOnlyList<HexTargetId> Ids { get; }
    public HexOwnerBatch(long owner, IReadOnlyList<HexTargetId> ids) { Owner = owner; Ids = ids; }
}

public sealed class HexOwnerPlan
{
    public IReadOnlyList<HexOwnerBatch> Batches { get; }
    /// <summary>Targets that have an owner; the number the request is judged against.</summary>
    public int Targets { get; }
    /// <summary>Candidates nobody owns right now (not loaded by any client); native killenemies and removedrops never reach them either.</summary>
    public int Unowned { get; }
    public bool OverLimit { get; }
    internal HexOwnerPlan(IReadOnlyList<HexOwnerBatch> batches, int targets, int unowned, bool overLimit)
    { Batches = batches; Targets = targets; Unowned = unowned; OverLimit = overLimit; }
}

public static class HexOwnerPlanner
{
    public static HexOwnerPlan Plan(IEnumerable<HexOwnedTarget> targets, int maxTargets, int batchSize)
    {
        if (maxTargets < 1) throw new ArgumentOutOfRangeException(nameof(maxTargets));
        if (batchSize < 1) throw new ArgumentOutOfRangeException(nameof(batchSize));
        var seen = new HashSet<HexTargetId>(); var order = new List<long>(); var byOwner = new Dictionary<long, List<HexTargetId>>();
        var unowned = 0; var owned = 0;
        foreach (var target in targets)
        {
            if (!seen.Add(target.Id)) continue;
            if (target.Owner == 0) { unowned++; continue; }
            owned++;
            if (!byOwner.TryGetValue(target.Owner, out var list)) { list = new List<HexTargetId>(); byOwner.Add(target.Owner, list); order.Add(target.Owner); }
            list.Add(target.Id);
        }
        if (owned > maxTargets) return new HexOwnerPlan(Array.Empty<HexOwnerBatch>(), owned, unowned, true);
        var batches = new List<HexOwnerBatch>();
        foreach (var owner in order)
            for (var i = 0; i < byOwner[owner].Count; i += batchSize)
                batches.Add(new HexOwnerBatch(owner, byOwner[owner].Skip(i).Take(batchSize).ToArray()));
        return new HexOwnerPlan(batches, owned, unowned, false);
    }
}

public enum HexBatchState { Applied, Refused, Unknown }

/// <summary>Adds up what the owners reported. A refused batch is certain (nothing ran); an unknown one is not, and no count is promised then.</summary>
public sealed class HexOwnerTally
{
    readonly int targets;
    int submitted, recorded;
    public bool Uncertain { get; private set; }
    public HexOwnerTally(int targets) { this.targets = targets >= 0 ? targets : throw new ArgumentOutOfRangeException(nameof(targets)); }
    public void Record(int batchSize, HexBatchState state, HexOwnerReply reply)
    {
        recorded += batchSize;
        if (recorded > targets || batchSize < 1) { Uncertain = true; return; }
        if (state == HexBatchState.Refused) return;
        if (state == HexBatchState.Unknown) { Uncertain = true; return; }
        if (reply.Applied < 0 || reply.Skipped < 0 || reply.Applied + reply.Skipped != batchSize) { Uncertain = true; return; }
        submitted += reply.Applied;
    }
    public HexKillReply Reply => Uncertain ? throw new InvalidOperationException("Owner outcome uncertain.") : new HexKillReply(targets, submitted, targets - submitted);
}
