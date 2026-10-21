// Ember - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: payload codecs of Ember's owned peer commands (design 3.1). BetterSleepBruh's routed RPCs are
// replaced; nothing of their wire format is kept.
using System;
using System.Buffers.Binary;

namespace ExtraPotions.Ember.Sleep
{
    /// <summary>
    /// Codecs for the sleep peer commands. The design names them ember.sleep.bedChanged and ember.sleep.state; Crucible
    /// operation ids must be lowercase and live under "meadworks.", so the wire ids are meadworks.ember.sleep.bed_changed
    /// and meadworks.ember.sleep.state. Every payload starts with a version byte; unknown versions are refused.
    /// </summary>
    public static class SleepCommands
    {
        public const string BedChangedName = "ember.sleep.bedChanged";
        public const string StateName = "ember.sleep.state";
        public const string BedChangedId = "meadworks.ember.sleep.bed_changed";
        public const string StateId = "meadworks.ember.sleep.state";

        /// <summary>Version 2 adds the boost total to the state reply (BetterSleepBruh 2.1.0 station timers).</summary>
        public const byte Version = 2;
        public const int MaxPlayers = 1024;
        public const double MaxExtraRate = 30.0;

        /// <summary>Version, flags, players (int32 LE), sleeping (int32 LE), extra rate and boost total (IEEE double LE).</summary>
        public const int StateLength = 1 + 1 + 4 + 4 + 8 + 8;

        const byte FlagWindowOpen = 0x01;
        const byte FlagBoosting = 0x02;
        const byte FlagFullSkip = 0x04;
        const byte KnownFlags = FlagWindowOpen | FlagBoosting | FlagFullSkip;

        /// <summary>Client to authority: "my player got into or out of bed; recount". No data beyond the version.</summary>
        public static byte[] EncodeBedChanged() => new[] { Version };

        public static bool TryDecodeBedChanged(byte[]? payload, out string? error) => VersionOnly(payload, out error);

        /// <summary>Client to authority: "send me the current sleep state". No data beyond the version.</summary>
        public static byte[] EncodeStateRequest() => new[] { Version };

        public static bool TryDecodeStateRequest(byte[]? payload, out string? error) => VersionOnly(payload, out error);

        /// <summary>Authority to client: the state reply. Throws ArgumentException for a state outside the bounds.</summary>
        public static byte[] EncodeState(SleepState state)
        {
            var problem = Validate(state);
            if (problem != null)
                throw new ArgumentException("Invalid sleep state: " + problem, nameof(state));
            var bytes = new byte[StateLength];
            bytes[0] = Version;
            bytes[1] = (byte)((state.WindowOpen ? FlagWindowOpen : 0) | (state.Boosting ? FlagBoosting : 0) | (state.FullSkip ? FlagFullSkip : 0));
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(2, 4), state.Players);
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(6, 4), state.Sleeping);
            BinaryPrimitives.WriteInt64LittleEndian(bytes.AsSpan(10, 8), BitConverter.DoubleToInt64Bits(state.ExtraRate));
            BinaryPrimitives.WriteInt64LittleEndian(bytes.AsSpan(18, 8), BitConverter.DoubleToInt64Bits(state.BoostTotal));
            return bytes;
        }

        public static bool TryDecodeState(byte[]? payload, out SleepState state, out string? error)
        {
            state = default;
            if (payload == null || payload.Length == 0)
            {
                error = "empty payload";
                return false;
            }
            if (payload[0] != Version)
            {
                error = "unknown version " + payload[0];
                return false;
            }
            if (payload.Length != StateLength)
            {
                error = "expected " + StateLength + " bytes, got " + payload.Length;
                return false;
            }
            byte flags = payload[1];
            if ((flags & ~KnownFlags) != 0)
            {
                error = "unknown flags";
                return false;
            }
            int players = BinaryPrimitives.ReadInt32LittleEndian(payload.AsSpan(2, 4));
            int sleeping = BinaryPrimitives.ReadInt32LittleEndian(payload.AsSpan(6, 4));
            double rate = BitConverter.Int64BitsToDouble(BinaryPrimitives.ReadInt64LittleEndian(payload.AsSpan(10, 8)));
            double total = BitConverter.Int64BitsToDouble(BinaryPrimitives.ReadInt64LittleEndian(payload.AsSpan(18, 8)));
            var decoded = new SleepState((flags & FlagWindowOpen) != 0, players, sleeping, rate, (flags & FlagBoosting) != 0, (flags & FlagFullSkip) != 0, total);
            error = Validate(decoded);
            if (error != null)
                return false;
            state = decoded;
            return true;
        }

        static string? Validate(SleepState s)
        {
            if (s.Players < 0 || s.Players > MaxPlayers)
                return "players out of range";
            if (s.Sleeping < 0 || s.Sleeping > s.Players)
                return "sleeping out of range";
            if (double.IsNaN(s.ExtraRate) || double.IsInfinity(s.ExtraRate) || s.ExtraRate < 0.0 || s.ExtraRate > MaxExtraRate)
                return "extra rate out of range";
            if (s.Boosting != (s.ExtraRate > 0.0))
                return "boosting must match a positive extra rate";
            if (double.IsNaN(s.BoostTotal) || double.IsInfinity(s.BoostTotal) || s.BoostTotal < 0.0 || s.BoostTotal > StationTimers.MaxBoostTotal)
                return "boost total out of range";
            return null;
        }

        static bool VersionOnly(byte[]? payload, out string? error)
        {
            if (payload == null || payload.Length == 0)
            {
                error = "empty payload";
                return false;
            }
            if (payload[0] != Version)
            {
                error = "unknown version " + payload[0];
                return false;
            }
            if (payload.Length != 1)
            {
                error = "unexpected data after the version";
                return false;
            }
            error = null;
            return true;
        }
    }
}
