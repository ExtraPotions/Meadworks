// Threshold - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: full step-aside (design 9.1, roster decision 3). Every source routes portal Use and portal trips
// itself; two routers on one portal are not safe, so Threshold steps aside as a whole while any is installed.
using System;
using System.Collections.Generic;
using System.Linq;

namespace ExtraPotions.Threshold.Integration
{
    /// <summary>A source mod by BepInEx plugin GUID.</summary>
    public sealed class SourcePlugin
    {
        internal SourcePlugin(string guid, string name)
        {
            Guid = guid;
            Name = name;
        }

        public string Guid { get; }
        public string Name { get; }
    }

    public sealed class StepAsideDecision
    {
        internal StepAsideDecision(IReadOnlyList<SourcePlugin> installed)
        {
            Installed = installed;
            SourceNames = string.Join(" and ", installed.Select(s => s.Name).Distinct());
        }

        public bool SteppedAside => Installed.Count > 0;

        public IReadOnlyList<SourcePlugin> Installed { get; }

        /// <summary>Installed source names for the notice's <c>{0}</c>, each once.</summary>
        public string SourceNames { get; }
    }

    public static class ProductStepAside
    {
        public const string NoticeKey = "step_aside";

        public static readonly SourcePlugin PortalSelector = new SourcePlugin("lazly.valheim.portalselector", "PortalSelector");

        /// <summary>PotalMap's CHANGELOG records a rename of its GUID and a revert; both spellings are listed.</summary>
        public static readonly SourcePlugin PotalMap = new SourcePlugin("com.korcaptain.potalmap", "PotalMap");
        public static readonly SourcePlugin PotalMapRenamed = new SourcePlugin("com.korcaptain.portalmap", "PotalMap");

        public static readonly SourcePlugin Wayfare = new SourcePlugin("com.Wayfare", "Wayfare");

        public static IReadOnlyList<SourcePlugin> Sources { get; } = Array.AsReadOnly(new[] { PortalSelector, PotalMap, PotalMapRenamed, Wayfare });

        /// <summary>GUIDs match exactly, as BepInEx keys its plugin table.</summary>
        public static StepAsideDecision Decide(IEnumerable<string> installedGuids)
        {
            if (installedGuids == null) throw new ArgumentNullException(nameof(installedGuids));
            var installed = new HashSet<string>(installedGuids.Where(g => g != null), StringComparer.Ordinal);
            return new StepAsideDecision(Sources.Where(s => installed.Contains(s.Guid)).ToList());
        }
    }
}
