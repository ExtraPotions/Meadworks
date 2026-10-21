// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.10.0 at d3b57606cb24775d61b7bda244b22eb89e7e7717.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries; a pre-0.2.0
// main economy file is updated once from the old defaults (EconomyDefaultsMigration).
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using BepInEx;
using ExtraPotions.Inscribe.Adopted.Core;

namespace ExtraPotions.Inscribe.Adopted.Rules
{
    /// <summary>
    /// A family's files on this machine, in the BepInEx config folder: the main file first, then every other
    /// <c>&lt;prefix&gt;*.yml</c> in case-insensitive name order. Writes the main file from the built-in defaults on
    /// first run (no file of the family exists), and in place of a format-1 main file (renamed to <c>.v1.bak</c>,
    /// <see cref="RuleFormat"/>); never writes anything else. Extra files not in the current format are skipped with a
    /// warning. Tracks names, sizes and write times so the reload can tell when anything changed; between reads the
    /// folder is listed again only when its own write time moved (a file added, removed or renamed), otherwise the
    /// poll costs one stat per known file (<see cref="Changed"/>).
    /// </summary>
    internal sealed class RuleFiles
    {
        /// <summary>A folder write time this recent may still move within its own clock tick: trusted from the next poll.</summary>
        private static readonly TimeSpan Settle = TimeSpan.FromSeconds(3);

        private readonly FamilySpec _spec;
        private string _signature = "";
        private List<string> _paths = new List<string>();
        private DateTime _folderWrite;

        public RuleFiles(FamilySpec spec)
        {
            _spec = spec;
        }

        public static string Folder => Paths.ConfigPath;

        /// <summary>Writes the main file from the defaults when the family has no file at all.</summary>
        public void EnsureMainFile()
        {
            if (List().Count > 0)
            {
                return;
            }
            string path = Path.Combine(Folder, _spec.MainFile);
            try
            {
                File.WriteAllText(path, FamilyBuilder.DefaultText(_spec), new UTF8Encoding(false));
                Log.Info($"wrote the default {_spec.MainFile}");
            }
            catch (Exception e)
            {
                Log.Error($"could not write {path}: {e.Message}");
            }
        }

        /// <summary>
        /// Reads every file of the family, in order. A file that cannot be read is skipped with an error; a format-1
        /// main file is replaced by the default, a format-1 extra file skipped (<see cref="RuleFormat"/>).
        /// </summary>
        public List<SourceText> Read()
        {
            List<SourceText> texts = new List<SourceText>();
            foreach (string path in List())
            {
                string name = Path.GetFileName(path);
                string? text = ReadText(path);
                text = text == null || RuleFormat.IsReadable(text, _spec.Format) ? text : OldFormat(path, name, text);
                if (text != null && _spec == FamilySpec.Economy && string.Equals(name, _spec.MainFile, StringComparison.OrdinalIgnoreCase))
                {
                    text = UpdateOldDefaults(path, name, text);
                }
                if (text != null)
                {
                    texts.Add(new SourceText(name, text));
                }
            }
            Remember();
            return texts;
        }

        // After a read (which may have replaced an old main file): the folder's time first, so a change made while
        // listing shows at the next poll.
        private void Remember()
        {
            _folderWrite = FolderWriteTime();
            _paths = List();
            _signature = Signature(_paths);
        }

        private static string? ReadText(string path)
        {
            try
            {
                return File.ReadAllText(path);
            }
            catch (Exception e)
            {
                Log.Error($"could not read {path}: {e.Message}");
                return null;
            }
        }

        // This machine's own main economy file only; the server's texts are never written here.
        private static string UpdateOldDefaults(string path, string name, string text)
        {
            var result = global::ExtraPotions.Inscribe.Conversion.EconomyDefaultsMigration.UpdateFile(path, text);
            if (result.Error != null)
            {
                Log.Warn($"{name}: values at the pre-0.2.0 defaults could not be updated: {result.Error}");
            }
            else if (result.Rewritten)
            {
                Log.Info($"{name}: {result.Notes.Count} values still at the pre-0.2.0 defaults were updated (the Dvergr Chisel, gems, Cleansing and the Serpent Rune now take Epic and Legendary items); your own values were kept; original saved as {Path.GetFileName(result.Backup)}");
            }
            return result.Text;
        }

        // The main file of an older version is replaced by the default; anything else not in the current format is skipped.
        private string? OldFormat(string path, string name, string text)
        {
            bool main = string.Equals(name, _spec.MainFile, StringComparison.OrdinalIgnoreCase);
            if (main && RuleFormat.Of(text) < _spec.Format)
            {
                return RuleFormat.ReplaceOldMain(path, text, FamilyBuilder.DefaultText(_spec));
            }
            Log.Warn($"{name} is {RuleFormat.Describe(text, _spec.Format)}: skipped (this version reads format {_spec.Format}; add 'format: {_spec.Format}' once it is updated)");
            return null;
        }

        /// <summary>
        /// True when a file was added, removed or rewritten since the last <see cref="Read"/>. The folder is listed again
        /// only when its write time moved (entries added, removed or renamed; editors that save by replacing the file
        /// land here too); an in-place save shows in the known files' own sizes and write times.
        /// </summary>
        public bool Changed()
        {
            DateTime folder = FolderWriteTime();
            if (folder == DateTime.MinValue || folder != _folderWrite)
            {
                _folderWrite = folder;
                _paths = List();
            }
            return Signature(_paths) != _signature;
        }

        // Unreadable reads as "unknown" (a re-list every poll); a time inside the settle window as well.
        private static DateTime FolderWriteTime()
        {
            try
            {
                DateTime write = Directory.GetLastWriteTimeUtc(Folder);
                return DateTime.UtcNow - write < Settle ? DateTime.MinValue : write;
            }
            catch (Exception)
            {
                return DateTime.MinValue;
            }
        }

        private List<string> List()
        {
            List<string> paths = new List<string>();
            try
            {
                foreach (string path in Directory.GetFiles(Folder, _spec.Prefix + "*.yml"))
                {
                    if (path.EndsWith(".yml", StringComparison.OrdinalIgnoreCase))
                    {
                        paths.Add(path);
                    }
                }
            }
            catch (Exception e)
            {
                Log.Error($"could not list {Folder}: {e.Message}");
            }
            paths.Sort(CompareFiles);
            return paths;
        }

        private int CompareFiles(string a, string b)
        {
            bool mainA = string.Equals(Path.GetFileName(a), _spec.MainFile, StringComparison.OrdinalIgnoreCase);
            bool mainB = string.Equals(Path.GetFileName(b), _spec.MainFile, StringComparison.OrdinalIgnoreCase);
            if (mainA != mainB)
            {
                return mainA ? -1 : 1;
            }
            return StringComparer.OrdinalIgnoreCase.Compare(Path.GetFileName(a), Path.GetFileName(b));
        }

        private static string Signature(List<string> paths)
        {
            StringBuilder sb = new StringBuilder();
            foreach (string path in paths)
            {
                try
                {
                    FileInfo info = new FileInfo(path);
                    sb.Append(info.Name).Append('|').Append(info.Length).Append('|').Append(info.LastWriteTimeUtc.Ticks).Append(';');
                }
                catch (Exception)
                {
                    sb.Append(path).Append("|?;");
                }
            }
            return sb.ToString();
        }
    }
}
