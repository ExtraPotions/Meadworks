// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace ExtraPotions.Barter.Lists
{
    public static class ListEditRules
    {
        public static string Revision(IReadOnlyDictionary<string, string> files)
        {
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream, Encoding.UTF8, true))
            using (var sha = SHA256.Create())
            {
                foreach (var file in files.OrderBy(p => p.Key, StringComparer.Ordinal))
                { writer.Write(file.Key); writer.Write(file.Value); }
                writer.Flush();
                return Convert.ToBase64String(sha.ComputeHash(stream.ToArray()));
            }
        }

        public static string OwnedPath(string root, string relative)
        {
            if (string.IsNullOrWhiteSpace(relative) || relative.Contains("..") || relative.Contains("\\") || Path.IsPathRooted(relative))
                throw new ArgumentException("Invalid list path.");
            string canonicalRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string path = Path.GetFullPath(Path.Combine(canonicalRoot, relative.Replace('/', Path.DirectorySeparatorChar)));
            if (!path.StartsWith(canonicalRoot, StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("List outside config root.");
            // Junctions/symlinks may redirect an otherwise valid lexical path outside the owned config directory.
            for (string? part = path; part != null && part.Length >= canonicalRoot.TrimEnd(Path.DirectorySeparatorChar).Length; part = Path.GetDirectoryName(part))
                if ((File.Exists(part) || Directory.Exists(part)) && (File.GetAttributes(part) & FileAttributes.ReparsePoint) != 0)
                    throw new IOException("Linked config paths cannot be edited.");
            return path;
        }

        public static bool AtomicReplace(string path, string expected, string replacement)
        {
            if (!File.Exists(path) || File.ReadAllText(path) != expected) return false;
            string temporary = path + ".barter-edit-" + Guid.NewGuid().ToString("N");
            string backup = path + ".barter-backup-" + Guid.NewGuid().ToString("N");
            try
            {
                using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                { byte[] bytes = new UTF8Encoding(false).GetBytes(replacement); stream.Write(bytes, 0, bytes.Length); stream.Flush(true); }
                if (File.ReadAllText(path) != expected) return false;
                File.Replace(temporary, path, backup);
                return true;
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
    }
}
