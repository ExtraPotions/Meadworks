// Barter - ExtraPotions Meadworks trader overhaul.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;

namespace ExtraPotions.Barter
{
    public enum LegacyFileOutcome
    {
        /// <summary>No file under the old name: nothing to carry over.</summary>
        None,
        /// <summary>The old file was copied to the new name and renamed to a .bak.</summary>
        Migrated,
        /// <summary>The new file already exists; it is kept and the old file is left untouched.</summary>
        KeptNew,
        /// <summary>The copy failed; the old file is untouched and no new file was written.</summary>
        Failed,
    }

    public sealed class LegacyFileResult
    {
        internal LegacyFileResult(LegacyFileOutcome outcome, string? backup = null, string? problem = null)
        {
            Outcome = outcome;
            Backup = backup;
            Problem = problem;
        }

        public LegacyFileOutcome Outcome { get; }

        /// <summary>Full path of the renamed old file after a migration, or null when it could not be renamed.</summary>
        public string? Backup { get; }

        /// <summary>Why the copy failed, or why the old file kept its name after a migration.</summary>
        public string? Problem { get; }
    }

    /// <summary>
    /// Carries a file from the old plugin GUID's name to the new one (com.extrapotions.barter.cfg to com.meadworks.barter.cfg).
    /// Runs before the settings bind. It never overwrites an existing new file and never deletes the old one: after a
    /// verified copy the old file is renamed to <c>&lt;old name&gt;.bak</c> (or a numbered .bak when that name is taken).
    /// </summary>
    public static class LegacyFileMigration
    {
        public static LegacyFileResult Run(string directory, string legacyName, string currentName)
        {
            if (string.IsNullOrEmpty(directory)) throw new ArgumentException("A config directory is required.", nameof(directory));
            if (string.IsNullOrEmpty(legacyName) || string.IsNullOrEmpty(currentName) || string.Equals(legacyName, currentName, StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Two different file names are required.");
            string legacy = Path.Combine(directory, legacyName), current = Path.Combine(directory, currentName);
            if (!File.Exists(legacy)) return new LegacyFileResult(LegacyFileOutcome.None);
            if (File.Exists(current)) return new LegacyFileResult(LegacyFileOutcome.KeptNew);
            string staging = current + ".migrate-" + Guid.NewGuid().ToString("N");
            try
            {
                byte[] bytes = File.ReadAllBytes(legacy);
                File.WriteAllBytes(staging, bytes);
                if (!bytes.SequenceEqual(File.ReadAllBytes(staging))) throw new IOException("The copied settings failed readback.");
                if (File.Exists(current)) return new LegacyFileResult(LegacyFileOutcome.KeptNew);
                File.Move(staging, current);
            }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException)
            {
                // A new file that appeared during the copy (another process) wins; it is never replaced.
                return File.Exists(current) ? new LegacyFileResult(LegacyFileOutcome.KeptNew)
                    : new LegacyFileResult(LegacyFileOutcome.Failed, problem: error.Message);
            }
            finally
            {
                try { if (File.Exists(staging)) File.Delete(staging); } catch (IOException) { } catch (UnauthorizedAccessException) { }
            }
            try
            {
                string backup = FreeBackupName(legacy);
                File.Move(legacy, backup);
                return new LegacyFileResult(LegacyFileOutcome.Migrated, backup);
            }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException)
            {
                return new LegacyFileResult(LegacyFileOutcome.Migrated, problem: "The old file kept its name: " + error.Message);
            }
        }

        private static string FreeBackupName(string path)
        {
            string first = path + ".bak";
            if (!File.Exists(first)) return first;
            for (int i = 1; i < 1000; i++)
            {
                string candidate = path + "." + i + ".bak";
                if (!File.Exists(candidate)) return candidate;
            }
            throw new IOException("No free backup name for " + Path.GetFileName(path) + ".");
        }
    }
}
