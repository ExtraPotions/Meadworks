// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using ExtraPotions.Thing.Moderation;

namespace ExtraPotions.Thing.Admin
{
    public enum EscrowState { Prepared, CommitRequested, Committed, Released, Cancelled, ClaimRequested, Claimed, DeleteRequested, Deleted, DeleteCancelled }

    public sealed class EscrowRecord
    {
        public EscrowRecord(long world, string token, string adminId, string targetId, ItemSlot slot, string payload, string pickup, EscrowState state, string sourceCharacter = "", string receiverCharacter = "")
        {
            World = world; Token = token; AdminId = adminId; TargetId = targetId; Slot = slot; Payload = payload; Pickup = pickup; State = state;
            SourceCharacter = sourceCharacter; ReceiverCharacter = receiverCharacter;
        }

        public long World { get; }
        public string Token { get; }
        public string AdminId { get; }
        public string TargetId { get; }
        public ItemSlot Slot { get; }
        public string Payload { get; }
        public string Pickup { get; }
        public EscrowState State { get; }
        public string SourceCharacter { get; }
        public string ReceiverCharacter { get; }
    }

    public sealed class InventoryEscrowJournal
    {
        public const int MaxRecords = 4096;
        public const int MaxPayloadBytes = 11000;
        public const int MaxRecordBytes = 32768;
        const int Magic = 0x4D574553, Version = 1, DigestBytes = 32;
        static readonly Encoding Utf8 = new UTF8Encoding(false, true);
        readonly string directory;

        public InventoryEscrowJournal(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("An escrow directory is required.", nameof(path));
            directory = Path.GetFullPath(path);
        }

        public void Save(EscrowRecord record)
        {
            Validate(record);
            Directory.CreateDirectory(directory);
            var target = Path.Combine(directory, record.Token + ".escrow");
            var exists = File.Exists(target);
            if (exists)
            {
                var previous = Read(target);
                if (previous.World != record.World || previous.AdminId != record.AdminId || previous.TargetId != record.TargetId
                    || !previous.Slot.Same(record.Slot) || previous.Payload != record.Payload || previous.SourceCharacter != record.SourceCharacter)
                    throw new InvalidDataException("An escrow token cannot change its item or participants.");
                if (!Next(previous.State, record.State)) throw new InvalidDataException("An escrow receipt cannot reverse or skip its commit decision.");
                if (previous.Pickup != record.Pickup && !CanBindPickup(previous, record))
                    throw new InvalidDataException("An escrow token cannot change its bound pickup.");
                if (previous.ReceiverCharacter != record.ReceiverCharacter && !(previous.State == EscrowState.Released && record.State == EscrowState.ClaimRequested
                    && previous.ReceiverCharacter.Length == 0 && record.ReceiverCharacter.Length != 0))
                    throw new InvalidDataException("An escrow token cannot change its bound receiver character.");
            }
            else if (Directory.EnumerateFiles(directory, "*.escrow").Take(MaxRecords).Count() >= MaxRecords)
                throw new InvalidDataException("The escrow journal is full.");

            byte[] body;
            using (var buffer = new MemoryStream())
            {
                using (var writer = new BinaryWriter(buffer, Utf8, true))
                {
                    writer.Write(Magic); writer.Write(Version); writer.Write(record.World);
                    Write(writer, record.Token); Write(writer, record.AdminId); Write(writer, record.TargetId);
                    Write(writer, record.Slot.Text); Write(writer, record.Payload); Write(writer, record.Pickup);
                    writer.Write((int)record.State);
                    Write(writer, record.SourceCharacter);
                    Write(writer, record.ReceiverCharacter);
                }
                body = buffer.ToArray();
            }
            byte[] digest;
            using (var hash = SHA256.Create()) digest = hash.ComputeHash(body);
            var temporary = Path.Combine(directory, record.Token + "." + Guid.NewGuid().ToString("N") + ".tmp");
            try
            {
                using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
                {
                    output.Write(body, 0, body.Length); output.Write(digest, 0, digest.Length);
                    output.Flush(true);
                }
                // Replacement never deletes the only saved receipt before the new bytes are durable.
                if (exists) File.Replace(temporary, target, null);
                else File.Move(temporary, target);
            }
            finally
            {
                if (File.Exists(temporary)) File.Delete(temporary);
            }
        }

        public IReadOnlyList<EscrowRecord> LoadAll(long world)
        {
            if (world == 0) throw new ArgumentException("An escrow world is required.", nameof(world));
            if (!Directory.Exists(directory)) return Array.Empty<EscrowRecord>();
            var files = Directory.EnumerateFiles(directory, "*.escrow").Take(MaxRecords + 1).ToArray();
            if (files.Length > MaxRecords) throw new InvalidDataException("The escrow journal exceeds its record limit.");
            var records = new List<EscrowRecord>();
            foreach (var file in files.OrderBy(path => path, StringComparer.Ordinal))
            {
                var record = Read(file);
                if (record.World == world) records.Add(record);
            }
            return records;
        }

        static EscrowRecord Read(string path)
        {
            byte[] bytes;
            using (var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                if (input.Length < DigestBytes + 20 || input.Length > MaxRecordBytes) throw new InvalidDataException("Invalid escrow record length.");
                bytes = new byte[(int)input.Length];
                var offset = 0;
                while (offset < bytes.Length)
                {
                    var read = input.Read(bytes, offset, bytes.Length - offset);
                    if (read == 0) throw new InvalidDataException("Truncated escrow record.");
                    offset += read;
                }
            }
            var bodyLength = bytes.Length - DigestBytes;
            byte[] digest;
            using (var hash = SHA256.Create()) digest = hash.ComputeHash(bytes, 0, bodyLength);
            for (var i = 0; i < DigestBytes; i++)
                if (digest[i] != bytes[bodyLength + i]) throw new InvalidDataException("The escrow record checksum differs.");
            try
            {
                using (var buffer = new MemoryStream(bytes, 0, bodyLength, false))
                using (var reader = new BinaryReader(buffer, Utf8))
                {
                    if (reader.ReadInt32() != Magic || reader.ReadInt32() != Version) throw new InvalidDataException("Unknown escrow record version.");
                    var world = reader.ReadInt64();
                    var token = Read(reader, 32); var admin = Read(reader, 640); var target = Read(reader, 640);
                    if (!ItemSlot.TryParse(Read(reader, 1024), out var slot)) throw new InvalidDataException("Invalid escrow item slot.");
                    var payload = Read(reader, MaxPayloadBytes); var pickup = Read(reader, 512); var state = (EscrowState)reader.ReadInt32();
                    var record = new EscrowRecord(world, token, admin, target, slot, payload, pickup, state, Read(reader, 32), Read(reader, 32));
                    if (buffer.Position != bodyLength || Path.GetFileName(path) != record.Token + ".escrow") throw new InvalidDataException("The escrow token or record boundary differs.");
                    Validate(record);
                    return record;
                }
            }
            catch (Exception error) when (error is ArgumentException || error is FormatException || error is EndOfStreamException)
            { throw new InvalidDataException("Invalid escrow record.", error); }
        }

        static void Validate(EscrowRecord record)
        {
            if (record == null) throw new ArgumentNullException(nameof(record));
            if (record.World == 0 || record.Token == null || record.Token.Length != 32
                || !record.Token.All(c => c >= '0' && c <= '9' || c >= 'a' && c <= 'f'))
                throw new ArgumentException("Invalid escrow world or token.");
            if (!(record.AdminId == "host" || record.AdminId == "server" || GameListIdentity.Valid(record.AdminId))
                || !GameListIdentity.Valid(record.TargetId) || record.AdminId == record.TargetId)
                throw new ArgumentException("Invalid escrow participants.");
            if (!ItemSlot.TryParse(record.Slot.Text, out var slot) || !slot.Same(record.Slot)) throw new ArgumentException("Invalid escrow slot.");
            if (record.Payload == null || record.Payload.Length == 0 || record.Payload.Length > MaxPayloadBytes)
                throw new ArgumentException("Invalid escrow payload length.");
            try
            {
                if (Convert.ToBase64String(Convert.FromBase64String(record.Payload)) != record.Payload) throw new ArgumentException("The escrow payload must be canonical base64.");
            }
            catch (FormatException error) { throw new ArgumentException("Invalid escrow payload.", error); }
            if (record.Pickup == null || record.Pickup.Length > 128 || record.Pickup.Any(char.IsControl)) throw new ArgumentException("Invalid escrow pickup id.");
            if ((int)record.State < (int)EscrowState.Prepared || (int)record.State > (int)EscrowState.DeleteCancelled) throw new ArgumentException("Invalid escrow state.");
            if (record.SourceCharacter == null || record.SourceCharacter.Length > 32 || record.SourceCharacter.Length != 0
                && (!long.TryParse(record.SourceCharacter, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var character) || character == 0))
                throw new ArgumentException("Invalid escrow source character.");
            if (record.ReceiverCharacter == null || record.ReceiverCharacter.Length > 32 || record.ReceiverCharacter.Length != 0
                && (!long.TryParse(record.ReceiverCharacter, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var receiver) || receiver == 0))
                throw new ArgumentException("Invalid escrow receiver character.");
            if (record.State >= EscrowState.DeleteRequested && (!record.Slot.HasIdentity || record.SourceCharacter.Length == 0 || record.Pickup.Length != 0 || record.ReceiverCharacter.Length != 0))
                throw new ArgumentException("A deletion requires an exact source image and no delivery.");
            if ((record.State == EscrowState.ClaimRequested || record.State == EscrowState.Claimed) && record.ReceiverCharacter.Length == 0)
                throw new ArgumentException("An escrow claim requires its receiver character.");
        }

        static void Write(BinaryWriter writer, string text)
        {
            var bytes = Utf8.GetBytes(text);
            writer.Write(bytes.Length); writer.Write(bytes);
        }

        static bool Next(EscrowState previous, EscrowState next)
        {
            if (previous == next) return true;
            switch (previous)
            {
                case EscrowState.Prepared: return next == EscrowState.CommitRequested || next == EscrowState.Cancelled;
                case EscrowState.CommitRequested: return next == EscrowState.Committed || next == EscrowState.Cancelled;
                case EscrowState.Committed: return next == EscrowState.Released;
                case EscrowState.Released: return next == EscrowState.ClaimRequested;
                case EscrowState.ClaimRequested: return next == EscrowState.Claimed;
                case EscrowState.DeleteRequested: return next == EscrowState.Deleted || next == EscrowState.DeleteCancelled;
                default: return false;
            }
        }

        static bool CanBindPickup(EscrowRecord previous, EscrowRecord next)
        {
            if (next.Pickup.Length == 0) return false;
            if (previous.State == EscrowState.Prepared && next.State == EscrowState.CommitRequested) return previous.Pickup.Length == 0;
            // Runtime recovery verifies no sealed pickup for this token remains before reconstructing it.
            return next.State == EscrowState.Committed && (previous.State == EscrowState.CommitRequested || previous.State == EscrowState.Committed)
                || previous.State == next.State && (next.State == EscrowState.Released || next.State == EscrowState.ClaimRequested);
        }

        static string Read(BinaryReader reader, int maximum)
        {
            var length = reader.ReadInt32();
            if (length < 0 || length > maximum || length > reader.BaseStream.Length - reader.BaseStream.Position) throw new InvalidDataException("Invalid escrow field length.");
            return Utf8.GetString(reader.ReadBytes(length));
        }
    }
}
