// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;

namespace ExtraPotions.Barter.Trading
{
    public sealed class JournalEntry
    {
        public string Token = "", Actor = "", BeforeInventory = "", AfterInventory = "";
        public long World;
        public byte Phase;
        public bool Consumed;
        public byte[] Request = Array.Empty<byte>();
    }

    // Inventory images are reconciliation evidence, never permission to grant an output again.
    public sealed class TradeJournal
    {
        private readonly string directory;
        public TradeJournal(string directory) { this.directory = Path.GetFullPath(directory); }
        private string PathFor(string token)
        {
            if (!Guid.TryParseExact(token, "N", out _)) throw new InvalidDataException("Invalid receipt token");
            return Path.Combine(directory, token + ".receipt");
        }

        public void Save(JournalEntry entry)
        {
            if (entry == null || entry.World == 0 || entry.Actor.Length == 0 || entry.Actor.Length > 128 ||
                entry.Phase > 3 || entry.Request.Length > 98304 || entry.BeforeInventory.Length > 2097152 || entry.AfterInventory.Length > 2097152) throw new InvalidDataException("Invalid journal entry");
            string target = PathFor(entry.Token);
            Directory.CreateDirectory(directory);
            if (!File.Exists(target))
            {
                int count = 0;
                foreach (JournalEntry existing in Read())
                    if (existing.World == entry.World && ++count >= 256) throw new InvalidDataException("Journal receipt bound");
            }
            byte[] payload;
            using (var memory = new MemoryStream())
            using (var writer = new BinaryWriter(memory))
            {
                writer.Write(3); writer.Write(entry.Token); writer.Write(entry.World); writer.Write(entry.Actor); writer.Write(entry.Phase);
                writer.Write(entry.Request.Length); writer.Write(entry.Request); writer.Write(entry.BeforeInventory);
                writer.Write(entry.AfterInventory);
                writer.Write(entry.Consumed);
                payload = memory.ToArray();
            }
            string temporary = target + ".pending";
            using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                using (var sha = SHA256.Create())
                {
                    byte[] checksum = sha.ComputeHash(payload); stream.Write(checksum, 0, checksum.Length);
                }
                stream.Write(payload, 0, payload.Length); stream.Flush(true);
            }
            if (File.Exists(target)) File.Replace(temporary, target, null);
            else File.Move(temporary, target);
        }

        public IReadOnlyList<JournalEntry> Read()
        {
            var rows = new List<JournalEntry>();
            if (!Directory.Exists(directory)) return rows;
            // A torn replacement is evidence, never permission to ignore a possibly prepared inventory write.
            foreach (string pending in Directory.EnumerateFiles(directory, "*.pending")) throw new InvalidDataException("Incomplete journal write");
            var counts = new Dictionary<long, int>();
            var totals = new Dictionary<long, long>();
            foreach (string path in Directory.EnumerateFiles(directory, "*.receipt"))
            {
                long length = new FileInfo(path).Length;
                if (length < 40 || length > 4400000) throw new InvalidDataException("Journal size");
                byte[] bytes = File.ReadAllBytes(path);
                if (bytes.Length < 40 || bytes.Length > 4400000) throw new InvalidDataException("Journal size");
                byte[] payload = new byte[bytes.Length - 32]; Buffer.BlockCopy(bytes, 32, payload, 0, payload.Length);
                using (var sha = SHA256.Create())
                {
                    byte[] hash = sha.ComputeHash(payload);
                    for (int i = 0; i < 32; i++) if (hash[i] != bytes[i]) throw new InvalidDataException("Journal checksum");
                }
                using (var stream = new MemoryStream(payload))
                using (var reader = new BinaryReader(stream))
                {
                    int version = reader.ReadInt32();
                    if (version < 1 || version > 3) throw new InvalidDataException("Journal version");
                    var row = new JournalEntry { Token = reader.ReadString(), World = reader.ReadInt64(), Actor = reader.ReadString(), Phase = reader.ReadByte() };
                    int size = reader.ReadInt32(); if (size < 0 || size > 98304) throw new InvalidDataException("Journal request");
                    row.Request = reader.ReadBytes(size); row.BeforeInventory = reader.ReadString();
                    if (version >= 2) row.AfterInventory = reader.ReadString();
                    row.Consumed = version >= 3 ? reader.ReadBoolean() : row.Phase == 2;
                    if (row.Request.Length != size || stream.Position != stream.Length || row.Actor.Length == 0 || row.Actor.Length > 128 ||
                        row.World == 0 || row.Phase > 3 || row.BeforeInventory.Length > 2097152 || row.AfterInventory.Length > 2097152 || !string.Equals(path, PathFor(row.Token), StringComparison.OrdinalIgnoreCase))
                        throw new InvalidDataException("Journal identity");
                    counts.TryGetValue(row.World, out int count); totals.TryGetValue(row.World, out long total);
                    if (++count > 256 || (total += length) > 67108864) throw new InvalidDataException("Journal world bound");
                    counts[row.World] = count; totals[row.World] = total;
                    rows.Add(row);
                }
            }
            return rows;
        }
        public void Remove(string token, bool characterSaved = false)
        {
            if (!characterSaved)
                foreach (var entry in Read())
                    if (entry.Token == token && (entry.Phase == 1 || entry.Phase == 2))
                        throw new InvalidOperationException("Character-save proof required before discarding inventory evidence");
            File.Delete(PathFor(token));
        }
    }
}
