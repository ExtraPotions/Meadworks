// Almanac - ExtraPotions Meadworks settings book.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace ExtraPotions.Almanac.Writing
{
    /// <summary>File operations the safe writer needs; the plugin implements them over the real disk, tests fake them.</summary>
    public interface IFileOps
    {
        bool Exists(string path);
        string ReadAllText(string path);
        void WriteAllText(string path, string text);
        /// <summary>Copies, replacing the destination.</summary>
        void Copy(string source, string destination);
        void Delete(string path);
    }

    public enum SafeWriteStep { Backup, Write, Verify, Rollback }

    public sealed class SafeWriteResult
    {
        internal SafeWriteResult(bool success, bool rolledBack, bool rollbackFailed, string? backupPath, IReadOnlyList<SafeWriteStep> steps, string? error)
        {
            Success = success;
            RolledBack = rolledBack;
            RollbackFailed = rollbackFailed;
            BackupPath = backupPath;
            Steps = steps;
            Error = error;
        }

        public bool Success { get; }
        public bool RolledBack { get; }
        /// <summary>Restoring failed; the player is told the backup path.</summary>
        public bool RollbackFailed { get; }
        public string? BackupPath { get; }
        public IReadOnlyList<SafeWriteStep> Steps { get; }
        public string? Error { get; }
    }

    /// <summary>
    /// Whole-file writes (registered YAML data files, unused key removal, profile apply backups): back up an existing
    /// file, write, read back and compare, and roll back from the backup (or remove a new file) on any failure.
    /// A failed backup writes nothing.
    /// </summary>
    public static class SafeFileWriter
    {
        public static SafeWriteResult Write(IFileOps ops, string path, string text, string backupPath)
        {
            var steps = new List<SafeWriteStep>();
            var existed = ops.Exists(path);
            string? backup = null;
            if (existed)
            {
                steps.Add(SafeWriteStep.Backup);
                try
                {
                    ops.Copy(path, backupPath);
                    backup = backupPath;
                }
                catch (Exception error)
                {
                    return new SafeWriteResult(false, false, false, null, steps, "Backup failed: " + error.Message);
                }
            }

            string? failure = null;
            steps.Add(SafeWriteStep.Write);
            try { ops.WriteAllText(path, text); }
            catch (Exception error) { failure = "Write failed: " + error.Message; }

            if (failure == null)
            {
                steps.Add(SafeWriteStep.Verify);
                try
                {
                    if (!string.Equals(ops.ReadAllText(path), text, StringComparison.Ordinal)) failure = "Verify failed: the file does not match what was written.";
                }
                catch (Exception error) { failure = "Verify failed: " + error.Message; }
            }

            if (failure == null) return new SafeWriteResult(true, false, false, backup, steps, null);

            steps.Add(SafeWriteStep.Rollback);
            try
            {
                if (existed) ops.Copy(backupPath, path);
                else if (ops.Exists(path)) ops.Delete(path);
                return new SafeWriteResult(false, true, false, backup, steps, failure);
            }
            catch (Exception error)
            {
                return new SafeWriteResult(false, false, true, backup, steps, failure + " Rollback failed: " + error.Message);
            }
        }
    }

    /// <summary>Backup names <c>&lt;name&gt;.&lt;yyyyMMdd-HHmmss&gt;&lt;ext&gt;</c> (UTC) and pruning to the newest N per file.</summary>
    public static class BackupNaming
    {
        public const string StampFormat = "yyyyMMdd-HHmmss";

        public static string Name(string filePath, DateTime utc)
        {
            var name = Index.ConfigPaths.FileName(filePath);
            var dot = name.LastIndexOf('.');
            var stem = dot > 0 ? name.Substring(0, dot) : name;
            var extension = dot > 0 ? name.Substring(dot) : "";
            return stem + "." + utc.ToUniversalTime().ToString(StampFormat, CultureInfo.InvariantCulture) + extension;
        }

        /// <summary>Backups of <paramref name="fileName"/> beyond the newest <paramref name="keep"/>, oldest first. Other names are never touched.</summary>
        public static IReadOnlyList<string> ToPrune(string fileName, IEnumerable<string> existing, int keep)
        {
            var dot = fileName.LastIndexOf('.');
            var stem = dot > 0 ? fileName.Substring(0, dot) : fileName;
            var extension = dot > 0 ? fileName.Substring(dot) : "";
            var pattern = new Regex("^" + Regex.Escape(stem) + @"\.(\d{8}-\d{6})" + Regex.Escape(extension) + "$", RegexOptions.CultureInvariant);
            var backups = existing.Select(n => (Name: n, Match: pattern.Match(n))).Where(x => x.Match.Success)
                .OrderByDescending(x => x.Match.Groups[1].Value, StringComparer.Ordinal).Select(x => x.Name).ToList();
            return backups.Skip(Math.Max(0, keep)).Reverse().ToList();
        }
    }
}
