// Threshold - ExtraPotions Meadworks portals: sea gates.
// Portions from Wayfare 0.3.2 (Copyright (C) 2026 Gerald Glasgow), GPL-3.0-or-later, at
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1: Wayfare/Wayfare/src/SeaGates/SeaGateWords.cs.
// Changed by expDARE, 2026: Threshold namespace; Threshold's settings, log, catalog words, access rules and portal keys (SeaGateBridge); Threshold ZDO keys, RPC names and pillar prefab name; patches installed by Threshold's own Harmony instance; the Kit picker instead of the large map; no crew pointers, map icons, placement preview or copied portal swirl.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks) for the changes. SPDX-License-Identifier: GPL-3.0-or-later

namespace ExtraPotions.Threshold.Runtime.SeaGates
{
    /// <summary>The sea gate words. A module that needs one more adds it in its own file with
    /// <see cref="Language.Add"/>, so parallel work never edits this file at the same time.</summary>
    public static class SeaGateWords
    {
        public static readonly string PillarName = Language.Add("wf_sg_pillar", "Sea Gate Pillar");
        public static readonly string PillarDescription = Language.Add("wf_sg_pillar_desc",
            "Build two on the shore or in shallow water, 10 to 15 m apart with water between them: a portal for ships opens between them.");
        public static readonly string DefaultName = Language.Add("wf_sg_default_name", "Sea gate");

        public static readonly string Unpaired = Language.Add("wf_sg_unpaired", "Not paired: build a second pillar 10 to 15 m away across the water");
        public static readonly string HoverRename = Language.Add("wf_sg_hover_rename", "Name");
        public static readonly string HoverSailIn = Language.Add("wf_sg_hover_sailin", "Sail in with someone at the helm to pick any sea gate");

        public static readonly string RenameTopic = Language.Add("wf_sg_rename", "Name this sea gate");

        public static readonly string PairOk = Language.Add("wf_sg_pair_ok", "Pairs with this pillar");
        public static readonly string PairTooFar = Language.Add("wf_sg_pair_far", "Too far from the other pillar: {0} m, at most {1} m");
        public static readonly string PairTooClose = Language.Add("wf_sg_pair_close", "Too close to the other pillar: {0} m, at least {1} m");
        public static readonly string PairHeight = Language.Add("wf_sg_pair_height", "{0} m higher or lower than the other pillar, at most {1} m");
        public static readonly string PairNoWater = Language.Add("wf_sg_pair_nowater", "No water between the pillars");
        public static readonly string PairShallow = Language.Add("wf_sg_pair_shallow", "Too shallow at the red marks: {0} m deep, a ship needs {1} m");
        public static readonly string PairTooDeep = Language.Add("wf_sg_pair_deep", "Too deep for a pillar: {0} m under water, at most {1} m");
        public static readonly string Paired = Language.Add("wf_sg_paired", "The sea gate is open");
        public static readonly string FixHint = Language.Add("wf_sg_fix_hint", "Fix it and the gate opens by itself");

        public static readonly string DeniedGone = Language.Add("wf_sg_denied_gone", "The destination sea gate is gone");
        public static readonly string DeniedCargo = Language.Add("wf_sg_denied_cargo", "Ore and other restricted cargo cannot pass through a sea gate");
        public static readonly string DeniedNotReady = Language.Add("wf_sg_denied_notready", "The sea gate was not ready: sail through again");
        public static readonly string DeniedNoHelmsman = Language.Add("wf_sg_denied_nohelm", "A ship passes through a sea gate only with someone at the helm");

        public static readonly string Fallback = Language.Add("wf_sg_fallback", "The ship did not arrive in time: you were set ashore by the gate");

        public static void Touch() => _ = PillarName;
    }
}
