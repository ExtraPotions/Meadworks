// Thing - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: the remote configuration guard of design D1 to D6 and Q3 (Almanac Q3 moved server-side editing here).
// Every request names a path relative to BepInEx/config with forward slashes; the server resolves it only after this check.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using ExtraPotions.Thing.Config;

namespace ExtraPotions.Thing.RemoteConfig
{
    public enum FileVerdict { Allowed, RefusedPath, RefusedLink, RefusedAdminList, RefusedExtension, RefusedScope, RefusedTooLarge }

    /// <summary>One request: the relative path, the file's size on the server and whether it is a symbolic link or junction.</summary>
    public readonly struct FileRequest
    {
        public FileRequest(string path, long sizeBytes, bool isLink)
        {
            Path = path ?? "";
            SizeBytes = sizeBytes;
            IsLink = isLink;
        }

        public string Path { get; }
        public long SizeBytes { get; }
        public bool IsLink { get; }
    }

    /// <summary>
    /// The server's current policy: scope and allow list (design 4.5), the registered Meadworks YAML files (relative paths)
    /// and whether file names compare case-sensitively (a Linux server, C10).
    /// </summary>
    public sealed class FilePolicy
    {
        public FilePolicy(RemoteConfigScope scope, string allowList, IEnumerable<string> registeredYaml, bool caseSensitive)
        {
            Scope = scope;
            CaseSensitive = caseSensitive;
            var comparer = caseSensitive ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase;
            AllowList = new HashSet<string>(ThingSettings.NormalizeList(allowList ?? "").Split(',').Where(n => n.Length > 0), comparer);
            RegisteredYaml = new HashSet<string>(registeredYaml ?? Enumerable.Empty<string>(), comparer);
        }

        public RemoteConfigScope Scope { get; }
        public bool CaseSensitive { get; }
        public ISet<string> AllowList { get; }
        public ISet<string> RegisteredYaml { get; }
    }

    public static class RemoteFiles
    {
        /// <summary>Largest file the remote editor opens (design D6): 1 MiB.</summary>
        public const long MaxBytes = 1024 * 1024;

        public const string MeadworksPrefix = "com.meadworks.";
        public const string ThingConfig = "com.meadworks.thing.cfg";
        public const string ThingFolder = "Meadworks/thing/";

        public static FileVerdict Check(FileRequest request, FilePolicy policy)
        {
            if (policy == null) throw new ArgumentNullException(nameof(policy));
            var path = request.Path;
            if (!SafePath(path)) return FileVerdict.RefusedPath;
            if (request.IsLink) return FileVerdict.RefusedLink;
            if (string.Equals(path, "adminlist.txt", StringComparison.OrdinalIgnoreCase)) return FileVerdict.RefusedAdminList;
            var topLevel = path.IndexOf('/') < 0;
            var cfg = path.EndsWith(".cfg", StringComparison.OrdinalIgnoreCase);
            var yaml = path.EndsWith(".yml", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".yaml", StringComparison.OrdinalIgnoreCase);
            if (yaml ? !policy.RegisteredYaml.Contains(path) : !cfg) return FileVerdict.RefusedExtension;
            var compare = policy.CaseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
            var meadworks = yaml || (topLevel && path.StartsWith(MeadworksPrefix, compare));
            bool inScope;
            switch (policy.Scope)
            {
                case RemoteConfigScope.AllowList: inScope = meadworks || (topLevel && policy.AllowList.Contains(path)); break;
                case RemoteConfigScope.AllConfigFiles: inScope = meadworks || topLevel; break;
                default: inScope = meadworks; break;
            }
            if (!inScope) return FileVerdict.RefusedScope;
            if (request.SizeBytes < 0 || request.SizeBytes > MaxBytes) return FileVerdict.RefusedTooLarge;
            return FileVerdict.Allowed;
        }

        /// <summary>
        /// A relative path below BepInEx/config: forward slashes only, no empty, <c>.</c> or <c>..</c> segment, no drive,
        /// colon, backslash, control character, or segment with surrounding spaces or a trailing dot.
        /// </summary>
        public static bool SafePath(string? path)
        {
            if (string.IsNullOrEmpty(path) || path![0] == '/') return false;
            if (path.Any(c => c == '\\' || c == ':' || char.IsControl(c))) return false;
            foreach (var segment in path.Split('/'))
            {
                if (segment.Length == 0 || segment == "." || segment == "..") return false;
                if (segment.Trim() != segment || segment.EndsWith(".", StringComparison.Ordinal)) return false;
            }
            return true;
        }

        /// <summary>Raw text editing is only for registered Meadworks YAML with product validation (D5, Almanac Q4).</summary>
        public static bool RawTextEditable(string path, IEnumerable<string> registeredYaml, bool caseSensitive) =>
            new FilePolicy(RemoteConfigScope.MeadworksOnly, "", registeredYaml, caseSensitive).RegisteredYaml.Contains(path ?? "");

        /// <summary>Thing's own server files (its cfg and everything in its data folder) are never deletable from the game.</summary>
        public static bool Protected(string path) =>
            string.Equals(path, ThingConfig, StringComparison.OrdinalIgnoreCase) || (path ?? "").StartsWith(ThingFolder, StringComparison.OrdinalIgnoreCase);
    }

    public static class FileVersion
    {
        /// <summary>Lowercase hex SHA256 of the file bytes: the version an editor read.</summary>
        public static string Hash(byte[] bytes)
        {
            using (var sha = SHA256.Create())
            {
                var digest = sha.ComputeHash(bytes ?? Array.Empty<byte>());
                var b = new StringBuilder(64);
                foreach (var x in digest) b.Append(x.ToString("x2", CultureInfo.InvariantCulture));
                return b.ToString();
            }
        }

        /// <summary>The write goes ahead only when the file is still the version the editor read (D4).</summary>
        public static bool Matches(string expectedHash, byte[] current) => string.Equals(expectedHash, Hash(current), StringComparison.OrdinalIgnoreCase);
    }

    public static class Backups
    {
        const string StampFormat = "yyyyMMdd-HHmmss";
        const string Suffix = ".bak";

        /// <summary>The backup's relative path: the file's own path plus a UTC stamp and <c>.bak</c>.</summary>
        public static string Name(string path, DateTime utc) => path + "." + utc.ToString(StampFormat, CultureInfo.InvariantCulture) + Suffix;

        /// <summary>Backups of <paramref name="path"/> beyond <paramref name="keep"/>, oldest first; other names are never returned.</summary>
        public static IReadOnlyList<string> ToDelete(string path, IEnumerable<string> existing, int keep)
        {
            var prefix = path + ".";
            var mine = new List<(DateTime Stamp, string Name)>();
            foreach (var name in existing ?? Enumerable.Empty<string>())
            {
                if (!name.StartsWith(prefix, StringComparison.Ordinal) || !name.EndsWith(Suffix, StringComparison.Ordinal)) continue;
                var stamp = name.Substring(prefix.Length, name.Length - prefix.Length - Suffix.Length);
                if (DateTime.TryParseExact(stamp, StampFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var at)) mine.Add((at, name));
            }
            var ordered = mine.OrderBy(m => m.Stamp).ThenBy(m => m.Name, StringComparer.Ordinal).Select(m => m.Name).ToList();
            return ordered.Take(Math.Max(0, ordered.Count - Math.Max(0, keep))).ToList();
        }

        /// <summary>Back up before the first write per file and session (D4).</summary>
        public static bool FirstWriteThisSession(string path, ISet<string> writtenThisSession) => !writtenThisSession.Contains(path);
    }
}
