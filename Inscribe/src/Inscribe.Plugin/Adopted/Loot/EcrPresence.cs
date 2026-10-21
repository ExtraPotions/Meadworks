// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
using BepInEx;
using BepInEx.Bootstrap;
using ExtraPotions.Inscribe.Adopted.Config;
using ExtraPotions.Inscribe.Adopted.Core;
using HarmonyLib;

namespace ExtraPotions.Inscribe.Adopted.Loot
{
    /// <summary>
    /// Whether Elite Creatures Reborn is installed on this peer (ecr-integration.md section 2, DECISIONS ECR-2): by its
    /// permanent plugin GUID in BepInEx's chainloader, checked once and cached for the process, so with ECR absent the
    /// whole hook costs one bool test per death. No <c>BepInDependency</c>, no manifest dependency, no reflection and no
    /// ECR type: everything else is string-keyed ZDO reads (<see cref="EcrKeys"/>). Local per peer: ECR itself must be on
    /// every peer, so in a supported install every peer agrees; a peer without it rolls as if it were absent.
    /// </summary>
    internal static class EcrPresence
    {
        public const string Guid = "gglasgow.elitecreaturesreborn";

        private static bool _checked;
        private static bool _present;
        private static string _version = "";

        /// <summary>ECR is loaded on this peer. The first call detects; every later one reads the cached answer.</summary>
        public static bool Present
        {
            get
            {
                if (!_checked)
                {
                    Detect();
                }
                return _present;
            }
        }

        /// <summary>ECR's version from its chainloader entry, for the log and <c>ecraft ecr</c>; "" when absent.</summary>
        public static string Version => Present ? _version : "";

        /// <summary>
        /// Called at the first <c>ZNetScene.Awake</c> (every plugin is loaded by then, so load order between the two mods
        /// does not matter); a later call does nothing.
        /// </summary>
        internal static void Detect()
        {
            if (_checked)
            {
                return;
            }
            _checked = true;
            if (Chainloader.PluginInfos.TryGetValue(Guid, out PluginInfo info) && info != null)
            {
                _present = true;
                _version = info.Metadata?.Version?.ToString() ?? "?";
                Log.Info($"Elite Creatures Reborn {_version} found; synergy {(ModSettings.EcrSynergy.Value ? "on" : "off")}");
                return;
            }
            Log.Debug("Elite Creatures Reborn not installed; its drop synergy stays off");
        }
    }

    /// <summary>Every peer: detects ECR once the chainloader is done, at the first scene with a network.</summary>
    [HarmonyPatch(typeof(ZNetScene), nameof(ZNetScene.Awake))]
    internal static class EcrDetectPatch
    {
        private static void Postfix() => EcrPresence.Detect();
    }
}
