// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.10.0 at d3b57606cb24775d61b7bda244b22eb89e7e7717.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries; the Honing Rune;
// Awakening folded into Ascension and Tempering into Honing (2026-10-19), their prefabs kept as legacy items.
using System;
using System.Linq;
using ExtraPotions.Inscribe.Adopted.Core;

namespace ExtraPotions.Inscribe.Adopted.Rules
{
    /// <summary>
    /// The code-side list of rune prefabs (prefabs.md): the five runes, the Dvergr Chisel and the eleven gems
    /// (sockets.md), whose prefab is <c>ECF_</c> + PascalCase id. Prefabs come from code only and exist on every peer
    /// whatever the YAML says; the YAML binds definitions to them (and can disable one, never add one).
    /// </summary>
    public static class StoneCatalog
    {
        public const string PrefabPrefix = "ECF_";

        /// <summary>The five runes (user decision 2026-10-19): Ascension (one rarity step, Common to Epic), Recasting
        /// (Uncommon rerolled), Honing (one inscription a tier up, its value rolled again), Cleansing (back to Common) and
        /// the Serpent (sealed). The Shaping and Consecrated Runes left on 2026-10-07, Awakening and Tempering on
        /// 2026-10-19 (<see cref="LegacyIds"/>). The Rune Table holds these only.</summary>
        public static readonly string[] BuiltInIds =
        {
            "ascension", "recasting", "honing", "cleansing", "serpent",
        };

        /// <summary>Runes folded into another (Awakening into Ascension, Tempering into Honing). Their prefabs stay
        /// registered so saved stacks load, and every stack converts to the successor (<c>Items.LegacyRuneConversion</c>);
        /// they have no definition, never drop and cannot be used.</summary>
        public static readonly string[] LegacyIds = global::ExtraPotions.Inscribe.Runes.LegacyRunes.Ids.ToArray();

        /// <summary>The Dvergr Chisel: one to three sockets on an item that has none.</summary>
        public const string ChiselId = "dvergr_chisel";

        /// <summary>The gems (sockets.md section 3); what each gives per base is <c>Sockets.GemCatalog</c>.</summary>
        public static readonly string[] GemIds =
        {
            "gem_surtr", "gem_ymir", "gem_thor", "gem_nidhogg", "gem_hel", "gem_tyr", "gem_freyja", "gem_odin",
            "gem_skadi", "gem_heimdall", "gem_sleipnir",
        };

        /// <summary>Every built-in id: the runes, then the chisel and the gems.</summary>
        public static readonly string[] AllIds = BuiltInIds.Concat(new[] { ChiselId }).Concat(GemIds).ToArray();

        /// <summary>Every id with a prefab: <see cref="AllIds"/> and the legacy runes.</summary>
        public static readonly string[] PrefabIds = AllIds.Concat(LegacyIds).ToArray();

        /// <summary>The rune a legacy id became (<c>awakening</c> to <c>ascension</c>), or null.</summary>
        public static string? SuccessorOf(string? id) => global::ExtraPotions.Inscribe.Runes.LegacyRunes.SuccessorOf(id);

        public static bool IsBuiltIn(string id) => Array.IndexOf(AllIds, id) >= 0;

        /// <summary>One of the five runes (not the chisel or a gem).</summary>
        public static bool IsRune(string id) => Array.IndexOf(BuiltInIds, id) >= 0;

        public static bool IsGem(string id) => Array.IndexOf(GemIds, id) >= 0;

        /// <summary>The chisel or a gem: the stones of the socket feature.</summary>
        public static bool IsSocketStone(string id) => id == ChiselId || IsGem(id);

        /// <summary><c>ascension</c> → <c>ECF_Ascension</c>.</summary>
        public static string PrefabFor(string builtInId) => PrefabPrefix + Ids.Pascal(builtInId);
    }
}
