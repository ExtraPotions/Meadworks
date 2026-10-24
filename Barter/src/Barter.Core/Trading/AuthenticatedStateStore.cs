// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace ExtraPotions.Barter.Trading
{
    /// <summary>Operations shared by local files and Valheim's cloud save backend.</summary>
    public interface IAuthorityStorage
    {
        bool Exists(string path);
        byte[] Read(string path, int maximumBytes);
        void WriteNew(string path, byte[] bytes);
        void Promote(string pending, string current, string backup);
        IEnumerable<string> Files(string directory, string pattern);
        void Delete(string path);
    }

    /// <summary>Server state bound to its world, object and field. Network mirrors are never an input.</summary>
    public sealed class AuthenticatedStateStore
    {
        public const int MaximumValueLength = 1048576;
        private const int MaximumRecordBytes = MaximumValueLength * 4 + 4096;
        private readonly string directory;
        private readonly IAuthorityStorage files;
        private readonly bool requireExistingKey;
        public AuthenticatedStateStore(string directory, IAuthorityStorage? storage = null, bool requireExistingKey = false)
        {
            this.directory = storage == null ? Path.GetFullPath(directory) : directory;
            files = storage ?? new LocalStorage(); this.requireExistingKey = requireExistingKey;
        }
        private string WorldDirectory(long world)
        {
            if (world == 0) throw new InvalidDataException("Authority world is unavailable");
            return Path.Combine(directory, world.ToString(CultureInfo.InvariantCulture));
        }
        private static void Identity(string objectId, string field)
        {
            if (string.IsNullOrEmpty(objectId) || objectId.Length > 128 || string.IsNullOrEmpty(field) || field.Length > 256)
                throw new InvalidDataException("Authority state identity bound");
        }
        private string RecordPath(long world, string objectId, string field)
        {
            Identity(objectId, field);
            using (var sha = SHA256.Create())
                return Path.Combine(WorldDirectory(world), BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(objectId + "\0" + field))).Replace("-", "") + ".state");
        }
        private byte[] Key(long world, bool create)
        {
            string folder = WorldDirectory(world), path = Path.Combine(folder, "authority.key");
            // Keys never rotate. An interrupted key copy must agree with an existing valid key.
            if (files.Exists(path + ".pending"))
            {
                byte[]? known = null;
                foreach (string existing in new[] { path, path + ".backup" })
                {
                    if (!files.Exists(existing)) continue;
                    try { byte[] value = files.Read(existing, 32); if (value.Length == 32) { known = value; break; } }
                    catch (InvalidDataException) { }
                }
                if (known != null)
                {
                    byte[]? pending = null;
                    try { pending = files.Read(path + ".pending", 32); } catch (InvalidDataException) { }
                    if (pending == null || !pending.SequenceEqual(known)) Quarantine(path + ".pending");
                }
            }
            byte[]? key = Recover(path, p => { byte[] value = files.Read(p, 32); if (value.Length != 32) throw new InvalidDataException("Authority key bound"); return value; });
            if (key != null) return key;
            if (files.Files(folder, "*.state*").Any()) throw new InvalidDataException("Authority key missing for existing state");
            string reset = Path.Combine(folder, "allow-new-authority-key");
            bool explicitReset = files.Exists(reset) && Encoding.UTF8.GetString(files.Read(reset, 64)).Trim() == world.ToString(CultureInfo.InvariantCulture);
            if (requireExistingKey && !explicitReset)
                throw new InvalidDataException("Existing world is missing Barter authority state. Restore Barter.Authority or create allow-new-authority-key containing the world UID to authorize its initial balance.");
            if (!create) return Array.Empty<byte>();
            key = new byte[32]; using (var random = RandomNumberGenerator.Create()) random.GetBytes(key);
            AtomicWrite(path, key); AtomicWrite(path + ".backup", key);
            if (explicitReset) files.Delete(reset);
            return key;
        }
        private sealed class Record { internal string Object = "", Field = "", Value = ""; }
        private Record Decode(string path, long world, byte[] key, string? objectId = null, string? field = null)
        {
            byte[] record = files.Read(path, MaximumRecordBytes);
            if (record.Length < 48 || record.Length > MaximumRecordBytes) throw new InvalidDataException("Authority record bound");
            byte[] expected;
            using (var hmac = new HMACSHA256(key)) expected = hmac.ComputeHash(record, 32, record.Length - 32);
            int different = 0; for (int i = 0; i < 32; i++) different |= expected[i] ^ record[i];
            if (different != 0) throw new InvalidDataException("Authority state authentication failed");
            using (var stream = new MemoryStream(record, 32, record.Length - 32))
            using (var reader = new BinaryReader(stream, Encoding.UTF8))
            {
                if (reader.ReadInt32() != 1 || reader.ReadInt64() != world) throw new InvalidDataException("Authority world mismatch");
                var decoded = new Record { Object = reader.ReadString(), Field = reader.ReadString(), Value = reader.ReadString() };
                Identity(decoded.Object, decoded.Field);
                if (objectId != null)
                {
                    // Direct reads already selected the canonical path for this exact identity.
                    if (decoded.Object != objectId || decoded.Field != field) throw new InvalidDataException("Authority state identity mismatch");
                }
                else
                {
                    string expectedPath = RecordPath(world, decoded.Object, decoded.Field).Replace('\\', '/');
                    string actualPath = path.Replace('\\', '/');
                    if (actualPath != expectedPath && actualPath != expectedPath + ".pending" && actualPath != expectedPath + ".backup")
                        throw new InvalidDataException("Authority record filename mismatch");
                }
                if (decoded.Value.Length > MaximumValueLength || stream.Position != stream.Length) throw new InvalidDataException("Authority state value bound");
                return decoded;
            }
        }
        public string Read(long world, string objectId, string field) => Read(world, objectId, field, RecordPath(world, objectId, field));
        private string Read(long world, string objectId, string field, string path)
        {
            byte[] key = Key(world, false);
            Record? record = Recover(path, p =>
            {
                if (key.Length != 32) throw new InvalidDataException("Authority key missing");
                return Decode(p, world, key, objectId, field);
            });
            return record?.Value ?? "";
        }
        // A complete pending commit wins. Torn writes are quarantined before recovering the last authenticated image.
        private T? Recover<T>(string path, Func<string, T> validate) where T : class
        {
            string pending = path + ".pending", backup = path + ".backup"; bool invalid = false;
            if (files.Exists(pending))
            {
                T? candidate = null;
                try { candidate = validate(pending); }
                catch (Exception error) when (error is InvalidDataException || error is EndOfStreamException || error is FormatException) { invalid = true; }
                if (candidate != null) { files.Promote(pending, path, backup); return candidate; }
                Quarantine(pending);
            }
            if (files.Exists(path))
            {
                try { return validate(path); }
                catch (Exception error) when (error is InvalidDataException || error is EndOfStreamException || error is FormatException) { invalid = true; }
            }
            if (files.Exists(backup))
            {
                T restored = validate(backup); byte[] bytes = files.Read(backup, MaximumRecordBytes);
                if (files.Exists(path)) Quarantine(path);
                files.WriteNew(pending, bytes); files.Promote(pending, path, path + ".replaced"); return restored;
            }
            if (invalid || files.Exists(path)) throw new InvalidDataException("No valid authority state remains; restore the world backup");
            return null;
        }
        private void Quarantine(string path) => files.Promote(path, path + ".quarantine-" + Guid.NewGuid().ToString("N"), path + ".unused");
        public void Write(long world, string objectId, string field, string value)
        {
            if (value == null || value.Length > MaximumValueLength) throw new InvalidDataException("Authority state value bound");
            string path = RecordPath(world, objectId, field); Read(world, objectId, field);
            byte[] key = Key(world, true), payload;
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream, Encoding.UTF8))
            { writer.Write(1); writer.Write(world); writer.Write(objectId); writer.Write(field); writer.Write(value); payload = stream.ToArray(); }
            using (var stream = new MemoryStream())
            {
                using (var hmac = new HMACSHA256(key)) { byte[] signature = hmac.ComputeHash(payload); stream.Write(signature, 0, signature.Length); }
                stream.Write(payload, 0, payload.Length); AtomicWrite(path, stream.ToArray());
            }
        }
        public string[] Fields(long world, string objectId, string prefix)
        {
            Identity(objectId, string.IsNullOrEmpty(prefix) ? "*" : prefix); byte[] key = Key(world, false);
            var fields = new HashSet<string>(StringComparer.Ordinal);
            foreach (string path in RecordFiles(WorldDirectory(world)))
            {
                Record? record = Recover(path, p => Decode(p, world, key));
                if (record == null) continue;
                if (record.Object == objectId && record.Field.StartsWith(prefix ?? "", StringComparison.Ordinal))
                { Read(world, objectId, record.Field, path); fields.Add(record.Field); }
            }
            return fields.ToArray();
        }
        private IEnumerable<string> RecordFiles(string folder) => files.Files(folder, "*.state*")
            .Where(path => path.EndsWith(".state", StringComparison.Ordinal) || path.EndsWith(".state.pending", StringComparison.Ordinal) || path.EndsWith(".state.backup", StringComparison.Ordinal))
            .Select(path => path.EndsWith(".pending", StringComparison.Ordinal) ? path.Substring(0, path.Length - 8) :
                path.EndsWith(".backup", StringComparison.Ordinal) ? path.Substring(0, path.Length - 7) : path)
            .Distinct(StringComparer.Ordinal).ToArray();
        public void Remove(long world, string objectId, string field)
        {
            string path = RecordPath(world, objectId, field); Read(world, objectId, field, path);
            // Recovery copies go first so an interrupted prune cannot resurrect a removed receipt.
            foreach (string suffix in new[] { ".pending", ".backup", "" }) files.Delete(path + suffix);
        }
        /// <summary>Copies authenticated legacy state, committing the key last so interrupted imports stay closed and retry safely.</summary>
        public void ImportLocal(long world, string legacyDirectory)
        {
            string target = WorldDirectory(world), source = Path.Combine(legacyDirectory, world.ToString(CultureInfo.InvariantCulture));
            if (files.Exists(Path.Combine(target, "authority.key")) || !Directory.Exists(source)) return;
            ImportFrom(world, new AuthenticatedStateStore(legacyDirectory));
        }
        public void ImportFrom(long world, AuthenticatedStateStore old, bool replaceExisting = false)
        {
            string target = WorldDirectory(world), source = old.WorldDirectory(world);
            byte[] key = old.Key(world, false);
            if (key.Length != 32) return;
            string keyPath = Path.Combine(target, "authority.key");
            if (files.Exists(keyPath) && !Key(world, false).SequenceEqual(key)) throw new InvalidDataException("Authority world migration key conflict");
            var records = new Dictionary<string, byte[]>(StringComparer.Ordinal);
            foreach (string path in old.RecordFiles(source))
            {
                Record? record = old.Recover(path, p => old.Decode(p, world, key));
                if (record == null) continue;
                old.Read(world, record.Object, record.Field);
                string canonical = old.RecordPath(world, record.Object, record.Field);
                records[Path.GetFileName(canonical)] = old.files.Read(canonical, MaximumRecordBytes);
            }
            foreach (var record in records)
            {
                string destination = Path.Combine(target, record.Key);
                if (replaceExisting && files.Exists(destination) && !files.Read(destination, MaximumRecordBytes).SequenceEqual(record.Value))
                {
                    Recover(destination, p => Decode(p, world, key));
                    AtomicWrite(destination, record.Value);
                }
                else ImportFile(destination, record.Value);
            }
            ImportFile(Path.Combine(target, "authority.key.backup"), key); ImportFile(Path.Combine(target, "authority.key"), key);
        }
        private void ImportFile(string path, byte[] bytes)
        {
            if (files.Exists(path))
            {
                if (!files.Read(path, MaximumRecordBytes).SequenceEqual(bytes)) throw new InvalidDataException("Authority import conflicts with existing world state");
                return;
            }
            if (files.Exists(path + ".pending"))
            {
                bool complete = false;
                try { complete = files.Read(path + ".pending", MaximumRecordBytes).SequenceEqual(bytes); } catch (InvalidDataException) { }
                if (complete) { files.Promote(path + ".pending", path, path + ".backup"); return; }
                Quarantine(path + ".pending");
            }
            AtomicWrite(path, bytes);
        }
        private void AtomicWrite(string path, byte[] bytes)
        { string temporary = path + ".pending"; files.WriteNew(temporary, bytes); files.Promote(temporary, path, path + ".backup"); }
        private sealed class LocalStorage : IAuthorityStorage
        {
            public bool Exists(string path) => File.Exists(path);
            public byte[] Read(string path, int maximumBytes)
            {
                using var timing = RefillTiming.Measure(RefillTiming.Operation.LocalRead);
                if (new FileInfo(path).Length > maximumBytes) throw new InvalidDataException("Authority file bound");
                byte[] bytes = File.ReadAllBytes(path); if (bytes.Length > maximumBytes) throw new InvalidDataException("Authority file bound"); return bytes;
            }
            public void WriteNew(string path, byte[] bytes)
            {
                using var timing = RefillTiming.Measure(RefillTiming.Operation.LocalWrite);
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    stream.Write(bytes, 0, bytes.Length);
                    using (RefillTiming.Measure(RefillTiming.Operation.Flush)) stream.Flush(true);
                }
            }
            public void Promote(string pending, string current, string backup)
            {
                using var timing = RefillTiming.Measure(RefillTiming.Operation.Promote);
                if (File.Exists(current)) File.Replace(pending, current, backup); else File.Move(pending, current);
            }
            public IEnumerable<string> Files(string directory, string pattern) => Directory.Exists(directory) ? Directory.EnumerateFiles(directory, pattern) : Array.Empty<string>();
            public void Delete(string path) => File.Delete(path);
        }
    }
}
