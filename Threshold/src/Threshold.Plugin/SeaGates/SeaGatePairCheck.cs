// Threshold - ExtraPotions Meadworks portals: sea gates.
// Portions from Wayfare 0.3.2 (Copyright (C) 2026 Gerald Glasgow), GPL-3.0-or-later, at
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1: Wayfare/Wayfare/src/SeaGates/SeaGatePairCheck.cs.
// Changed by expDARE, 2026: Threshold namespace; Threshold's settings, log, catalog words, access rules and portal keys (SeaGateBridge); Threshold ZDO keys, RPC names and pillar prefab name; patches installed by Threshold's own Harmony instance; the Kit picker instead of the large map; no crew pointers, map icons, placement preview or copied portal swirl.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks) for the changes. SPDX-License-Identifier: GPL-3.0-or-later
using System.Globalization;

namespace ExtraPotions.Threshold.Runtime.SeaGates
{
    /// <summary>The verdict for a pillar at a position: the nearest unpaired pillar it would pair with (if any) and
    /// whether it may. <see cref="ReasonToken"/> is a SeaGateWords token (PairOk or why not); for a measured problem
    /// <see cref="Value"/> is what was found and <see cref="Limit"/> what the rule wants (metres), which the token's
    /// {0} and {1} show, so a player knows how far off it is.</summary>
    public readonly struct PairCheck
    {
        public readonly SeaGatePillar Candidate;
        public readonly bool Ok;
        public readonly int Sides;
        public readonly string ReasonToken;
        public readonly float Value;
        public readonly float Limit;

        public PairCheck(SeaGatePillar candidate, bool ok, int sides, string reasonToken, float value = 0f, float limit = 0f)
        {
            Candidate = candidate;
            Ok = ok;
            Sides = sides;
            ReasonToken = reasonToken;
            Value = value;
            Limit = limit;
        }

        /// <summary>The reason as a player reads it, numbers filled in.</summary>
        public string Describe()
        {
            if (global::Localization.instance == null || string.IsNullOrEmpty(ReasonToken))
                return ReasonToken ?? "";
            string text = global::Localization.instance.Localize(ReasonToken);
            if (!text.Contains("{0}"))
                return text;
            return string.Format(text, Metres(Value), Metres(Limit));
        }

        private static string Metres(float value) => value.ToString("0.#", CultureInfo.InvariantCulture);
    }
}
