// Bestiary - ExtraPotions Meadworks creatures and bone arsenal.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: step-aside decision (design 11.1) and the CreatureManager workshop file plan (design 11.2, Q1).
// Only CreatureManager's file names are used; no CreatureManager code.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace ExtraPotions.Bestiary.Conversion
{
    public readonly struct StepAsideDecision
    {
        public StepAsideDecision(bool productYields, bool workshopYields)
        {
            ProductYields = productYields;
            WorkshopYields = workshopYields;
        }

        /// <summary>EliteCreaturesPack is installed: both would register the same prefabs, so Bestiary stays off.</summary>
        public bool ProductYields { get; }

        /// <summary>The workshop module stays off (CreatureManager has creature data, or the whole product yields).</summary>
        public bool WorkshopYields { get; }
    }

    /// <summary>Design 11.1: EliteCreaturesPack takes the whole product; CreatureManager takes the workshop unless its domains are empty (Q1).</summary>
    public static class StepAsideRules
    {
        public const string EcpGuid = "com.EliteCreaturesPack";
        public const string CreatureManagerGuid = "sighsorry.CreatureManager";

        /// <summary>Step-aside registry id of the workshop module (one notice per module).</summary>
        public const string WorkshopId = BestiaryCore.ProductId + ".workshop";

        public static StepAsideDecision Decide(IEnumerable<string> installed, bool cmDomainsEmpty)
        {
            var set = new HashSet<string>(installed ?? Enumerable.Empty<string>(), StringComparer.Ordinal);
            var product = set.Contains(EcpGuid);
            var workshop = product || (set.Contains(CreatureManagerGuid) && !cmDomainsEmpty);
            return new StepAsideDecision(product, workshop);
        }
    }

    public enum WorkshopAction
    {
        /// <summary>Copied into Bestiary's workshop data folder; the source stays untouched.</summary>
        Copy,

        /// <summary>Levels and Karma belong to Wildblood's converter.</summary>
        Wildblood,

        /// <summary>Not a workshop file, or a path outside the CreatureManager folder.</summary>
        Ignore,
    }

    public readonly struct WorkshopFile
    {
        public WorkshopFile(string path, WorkshopAction action)
        {
            Path = path;
            Action = action;
        }

        /// <summary>Relative to <c>BepInEx/config/CreatureManager</c>, forward slashes.</summary>
        public string Path { get; }

        public WorkshopAction Action { get; }
    }

    public sealed class WorkshopPlan
    {
        internal WorkshopPlan(IReadOnlyList<WorkshopFile> files, IReadOnlyList<string> sampleTextures)
        {
            Files = files;
            SampleTextures = sampleTextures;
        }

        public IReadOnlyList<WorkshopFile> Files { get; }

        /// <summary>CreatureManager's sample textures (<c>cm_*.png</c>): copied as the user's own files, reported because Bestiary never ships them.</summary>
        public IReadOnlyList<string> SampleTextures { get; }
    }

    /// <summary>The CreatureManager files the workshop imports (preview then apply, C09).</summary>
    public static class WorkshopImport
    {
        public const string SourceFolder = "BepInEx/config/CreatureManager";

        private static readonly Regex Domain = new Regex(@"^(factions\.yml|(creatures|attacks|ai|projectile)(_[^/]*)?\.ya?ml)$", RegexOptions.CultureInvariant);
        private static readonly Regex WildbloodFile = new Regex(@"^(levels|karma)(_[^/]*)?\.yml$", RegexOptions.CultureInvariant);

        public static WorkshopPlan Plan(IEnumerable<string> relativePaths)
        {
            var files = new List<WorkshopFile>();
            var samples = new List<string>();
            foreach (var raw in relativePaths ?? Enumerable.Empty<string>())
            {
                var path = (raw ?? "").Replace('\\', '/');
                var action = Classify(path);
                files.Add(new WorkshopFile(path, action));
                if (action == WorkshopAction.Copy && path.StartsWith("textures/", StringComparison.Ordinal)
                    && path.Substring("textures/".Length).StartsWith("cm_", StringComparison.Ordinal))
                    samples.Add(path);
            }
            return new WorkshopPlan(files, samples);
        }

        /// <summary>True when no creature domain file has a line other than blanks and comments (CM then needs no step-aside).</summary>
        public static bool DomainsEmpty(IReadOnlyDictionary<string, string> domainFiles)
        {
            foreach (var pair in domainFiles)
            {
                if (!Domain.IsMatch(pair.Key.Replace('\\', '/'))) continue;
                foreach (var line in (pair.Value ?? "").Split('\n'))
                {
                    var trimmed = line.Trim();
                    if (trimmed.Length > 0 && trimmed[0] != '#') return false;
                }
            }
            return true;
        }

        private static WorkshopAction Classify(string path)
        {
            if (path.Length == 0 || path[0] == '/' || path.Contains(":") || path.Split('/').Any(part => part == ".." || part == "."))
                return WorkshopAction.Ignore;
            if (Domain.IsMatch(path)) return WorkshopAction.Copy;
            if (WildbloodFile.IsMatch(path)) return WorkshopAction.Wildblood;
            var slash = path.IndexOf('/');
            if (slash > 0 && path.IndexOf('/', slash + 1) < 0)
            {
                var folder = path.Substring(0, slash);
                var name = path.Substring(slash + 1);
                if (folder == "localization" && (name.EndsWith(".yml", StringComparison.Ordinal) || name.EndsWith(".yaml", StringComparison.Ordinal)) && name.Length > 4) return WorkshopAction.Copy;
                if (folder == "textures" && name.EndsWith(".png", StringComparison.OrdinalIgnoreCase) && name.Length > 4) return WorkshopAction.Copy;
            }
            return WorkshopAction.Ignore;
        }
    }
}
