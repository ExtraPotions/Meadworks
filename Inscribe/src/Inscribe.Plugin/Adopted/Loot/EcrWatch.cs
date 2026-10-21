// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
using ExtraPotions.Inscribe.Adopted.Core;

namespace ExtraPotions.Inscribe.Adopted.Loot
{
    /// <summary>
    /// Watches whether ECR's keys still arrive (ecr-integration.md section 8, DECISIONS ECR-11). The keys are the contract
    /// and no version range is enforced, so a renamed key would silently make every creature look unresolved and the
    /// synergy pay nothing. After 20 qualifying deaths in a row on this peer with ECR present, the synergy on and no
    /// <c>ecr_resolved</c>, one warning is logged per session. Also keeps the last 20 for <c>ecraft ecr</c>. Local state
    /// on the peer that rolls (the creature's owner); nothing is synced. A handful of int updates per death.
    /// </summary>
    internal static class EcrWatch
    {
        public const int Window = 20;

        private static readonly bool[] Ring = new bool[Window];
        private static int _next;
        private static int _count;
        private static int _withData;
        private static int _missingRun;
        private static bool _warned;

        /// <summary>A creature with <c>ecr_tier</c> has died here this session (an ECR release that records tiers).</summary>
        public static bool TierSeen { get; private set; }

        /// <summary>One qualifying death on this peer; ignored unless the ECR keys were read in full (ECR present, synergy on).</summary>
        public static void Record(in EcrFacts ecr)
        {
            if (!ecr.ReadFull)
            {
                return;
            }
            Push(ecr.Resolved);
            TierSeen |= ecr.HasTier;
            _missingRun = ecr.Resolved ? 0 : _missingRun + 1;
            if (_missingRun >= Window && !_warned)
            {
                _warned = true;
                Log.Warn("Elite Creatures Reborn is loaded but no creature carries its star data; its keys may have changed - "
                    + "the synergy is paying nothing");
            }
        }

        /// <summary>"last 20 deaths: 17 with ECR data", or why there is nothing yet.</summary>
        public static string Summary()
        {
            if (_count == 0)
            {
                return "no qualifying deaths recorded on this machine yet (counted while the synergy is on)";
            }
            string text = $"last {_count} deaths: {_withData} with ECR data";
            return _warned ? text + " - WARNING: 20 in a row without, ECR's keys may have changed" : text;
        }

        private static void Push(bool withData)
        {
            if (_count == Window)
            {
                _withData -= Ring[_next] ? 1 : 0;
            }
            else
            {
                _count++;
            }
            Ring[_next] = withData;
            _withData += withData ? 1 : 0;
            _next = (_next + 1) % Window;
        }
    }
}
