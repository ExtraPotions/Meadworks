// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.11.0 at a0e33b9e74be2141e6ef38909d1d77de482cd7ac.
// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.10.0 at d3b57606cb24775d61b7bda244b22eb89e7e7717.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries; the hone verb
// (the temper verb left with the Tempering Rune, 2026-10-19).
using System;

namespace ExtraPotions.Inscribe.Adopted.Rules
{
    // Every enum here maps to the YAML's snake_case ids through Core.EnumIds (PascalCase ↔ snake_case).

    public enum AffixValueType { Percent, Flat, Flag }

    public enum AffixUnit { None, Ms, Deg, M, Min }

    public enum AffixCategory { Offense, Defense, Utility }

    /// <summary>An inscription's <c>affix</c>: prefixes are an item's core power, suffixes everything else.</summary>
    public enum AffixKind { Prefix, Suffix }

    /// <summary>How an inscription fits an item class: not at all, <c>allowed</c> (top tiers closed) or <c>best</c>.</summary>
    public enum ClassFit { None, Allowed, Best }

    public enum HookDifficulty { None, Easy, Medium, Hard }

    /// <summary>What a rune does: Ascension promotes, Cleansing strips, the Serpent Rune corrupts, Recasting rerolls,
    /// Honing hones one inscription; the Dvergr Chisel cuts a socket, a gem fills one.</summary>
    public enum StoneVerb { Promote, Strip, Corrupt, Reroll, Socket, Gem, Hone }

    /// <summary>The Serpent Rune's outcomes; every one seals the item.</summary>
    public enum CorruptOutcome { SealOnly, AddInscription, ChaoticReroll }

    /// <summary>Damage types a <c>damage_type</c> or <c>element</c> param names; groups are unions.</summary>
    [Flags]
    public enum DamageMask
    {
        None = 0,
        Blunt = 1,
        Slash = 2,
        Pierce = 4,
        Fire = 8,
        Frost = 16,
        Lightning = 32,
        Poison = 64,
        Spirit = 128,
        Physical = Blunt | Slash | Pierce,
        Elemental = Fire | Frost | Lightning | Poison,
        All = Physical | Elemental | Spirit,
    }
}
