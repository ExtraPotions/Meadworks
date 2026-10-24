// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ExtraPotions.Barter.Trading;
using Splatform;

namespace ExtraPotions.Barter.Runtime
{
    /// <summary>Keeps cloud authority records in the same native storage backend as the world.</summary>
    internal sealed class WorldAuthorityStorage : IAuthorityStorage
    {
        private readonly FileHelpers.FileSource source;
        internal WorldAuthorityStorage(FileHelpers.FileSource source) => this.source = source;
        private static string NativePath(string path) => path.Replace('\\', '/');
        public bool Exists(string path) => FileHelpers.Exists(NativePath(path), source);
        public byte[] Read(string path, int maximumBytes)
        {
            using var timing = RefillTiming.Measure(RefillTiming.Operation.CloudRead);
            path = NativePath(path);
            if (FileHelpers.GetFileSize(path, source) > (ulong)maximumBytes) throw new InvalidDataException("Authority file bound");
            var reader = new FileReader(path, source);
            try
            {
                if (reader.m_binary.BaseStream.CanSeek)
                {
                    long remaining = reader.m_binary.BaseStream.Length - reader.m_binary.BaseStream.Position;
                    if (remaining < 0 || remaining > maximumBytes) throw new InvalidDataException("Authority file bound");
                    byte[] bytes = reader.m_binary.ReadBytes((int)remaining);
                    // A growing stream still needs the original max+1 overrun check.
                    int next = reader.m_binary.BaseStream.ReadByte();
                    if (next < 0) return bytes;
                    if (bytes.Length == maximumBytes) throw new InvalidDataException("Authority file bound");
                    return ReadRemainder(reader.m_binary, bytes, next, maximumBytes);
                }
                return ReadRemainder(reader.m_binary, Array.Empty<byte>(), -1, maximumBytes);
            }
            finally { reader.Dispose(); }
        }
        private static byte[] ReadRemainder(BinaryReader reader, byte[] prefix, int next, int maximumBytes)
        {
            using var bytes = new MemoryStream(prefix.Length + (next < 0 ? 0 : 1));
            bytes.Write(prefix, 0, prefix.Length);
            if (next >= 0) bytes.WriteByte((byte)next);
            var buffer = new byte[(int)Math.Min(4096L, (long)maximumBytes + 1)];
            while (true)
            {
                int allowed = (int)Math.Min(buffer.Length, (long)maximumBytes - bytes.Length + 1);
                int read = reader.Read(buffer, 0, allowed);
                if (read == 0) return bytes.ToArray();
                if (bytes.Length + read > maximumBytes) throw new InvalidDataException("Authority file bound");
                bytes.Write(buffer, 0, read);
            }
        }
        public void WriteNew(string path, byte[] bytes)
        {
            using var timing = RefillTiming.Measure(RefillTiming.Operation.CloudWrite);
            path = NativePath(path);
            if (Exists(path)) throw new IOException("Authority pending file already exists");
            var writer = new FileWriter(path, CloudStorageFileGrouping.SameFolder, FileHelpers.FileHelperType.Binary, source);
            if (writer.Status != FileWriter.WriterStatus.OpenSucceeded) throw new IOException("Authority storage could not open pending file");
            try { writer.m_binary.Write(bytes); }
            finally { using (RefillTiming.Measure(RefillTiming.Operation.CloudFinish)) writer.Finish(); }
            if (writer.Status != FileWriter.WriterStatus.CloseSucceeded || !Read(path, bytes.Length).SequenceEqual(bytes)) throw new IOException("Authority storage did not commit pending file");
        }
        public void Promote(string pending, string current, string backup)
        {
            using var timing = RefillTiming.Measure(RefillTiming.Operation.Promote);
            pending = NativePath(pending); current = NativePath(current); backup = NativePath(backup);
            byte[] expected = Read(pending, AuthenticatedStateStore.MaximumValueLength * 4 + 4096);
            FileHelpers.ReplaceOldFile(current, pending, backup, CloudStorageFileGrouping.SameFolder, source);
            if (!Exists(current) || !Read(current, expected.Length).SequenceEqual(expected)) throw new IOException("Authority storage replacement failed");
        }
        public IEnumerable<string> Files(string directory, string pattern)
        {
            string prefix = NativePath(directory).TrimEnd('/') + "/";
            string contains = pattern.Replace("*", "");
            return (FileHelpers.GetFiles(source, prefix, searchPattern: contains) ?? throw new IOException("Authority storage listing failed"))
                .Where(path => NativePath(path).StartsWith(prefix, StringComparison.Ordinal) && !NativePath(path).Substring(prefix.Length).Contains("/"));
        }
        public void Delete(string path)
        {
            path = NativePath(path);
            if (Exists(path) && (!FileHelpers.Delete(path, source) || Exists(path))) throw new IOException("Authority storage deletion failed");
        }
    }
}
