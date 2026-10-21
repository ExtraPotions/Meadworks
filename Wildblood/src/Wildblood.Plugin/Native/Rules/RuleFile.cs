// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using System;
using System.IO;
using BepInEx;
using ExtraPotions.Wildblood.Adopted.Util;

namespace ExtraPotions.Wildblood.Adopted.Rules
{
    /// <summary>
    /// This machine's copy of the rule file on disk: it writes the fully documented default on first run, reads and
    /// parses it, and reports when the file has changed so a reload can pick it up. A bad edit is reported line by line
    /// and the last good rules are kept - the parse never throws out of here. The text is remembered verbatim so a
    /// server can push exactly what it holds, comments and all.
    /// </summary>
    internal static class RuleFile
    {
        public static string LocalText { get; private set; } = RuleText.Default;
        public static RuleSet Local { get; private set; } = RuleDefaults.BuildDefault();

        private static string Path => System.IO.Path.Combine(Paths.ConfigPath, RuleText.FileName);
        private static long _stampTicks;
        private static long _stampSize;

        public static void Initialise()
        {
            EnsureExists();
            Load();
        }

        private static void EnsureExists()
        {
            try
            {
                if (!File.Exists(Path))
                {
                    File.WriteAllText(Path, RuleText.Default);
                    Log.Info($"wrote a fresh rule file to {Path}");
                }
            }
            catch (Exception e)
            {
                Log.Error($"could not write the rule file: {e.Message}");
            }
        }

        /// <summary>Reads and parses the file; keeps the previous rules if the new text is bad. Returns true if adopted.</summary>
        public static bool Load()
        {
            string text;
            try
            {
                text = File.ReadAllText(Path);
            }
            catch (Exception e)
            {
                Log.Error($"could not read the rule file: {e.Message}");
                return false;
            }
            Stamp();
            return Adopt(text);
        }

        private static bool Adopt(string text)
        {
            RuleParser.Result parsed = RuleParser.Parse(text);
            foreach (string warning in parsed.Warnings)
            {
                Log.Warn($"rule file: {warning}");
            }
            if (parsed.Rules == null || parsed.Errors.Count > 0)
            {
                Report(parsed.Errors);
                return false;
            }
            LocalText = text;
            Local = parsed.Rules;
            return true;
        }

        private static void Report(System.Collections.Generic.List<string> errors)
        {
            Log.Error("rule file has errors; keeping the last good rules:");
            foreach (string error in errors)
            {
                Log.Error($"  {error}");
            }
        }

        public static bool ChangedOnDisk()
        {
            try
            {
                FileInfo info = new FileInfo(Path);
                return info.Exists && (info.LastWriteTimeUtc.Ticks != _stampTicks || info.Length != _stampSize);
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static void Stamp()
        {
            try
            {
                FileInfo info = new FileInfo(Path);
                _stampTicks = info.LastWriteTimeUtc.Ticks;
                _stampSize = info.Length;
            }
            catch (Exception)
            {
                _stampTicks = 0;
                _stampSize = 0;
            }
        }
    }
}
