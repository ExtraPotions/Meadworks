// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using System;
using ExtraPotions.Wildblood.Adopted.Traits;
using UnityEngine;

namespace ExtraPotions.Wildblood.Adopted.Mutations
{
    /// <summary>
    /// What makes one ground trail itself rather than the other: which mutation lays it, the ZDO keys its drops travel
    /// under, the colour its patches are drawn in, the status effect a player standing in one wears (its name, words
    /// and icon), how long that lingers after they step out, and whether it slips. Everything else - laying, drawing,
    /// the player's footing - is shared, so Frostbound's ice and Mudbound's mud are one mechanism with two
    /// descriptions.
    /// </summary>
    internal sealed class TrailKind
    {
        /// <summary>Frostbound's ice: pale, glossy blue; the player slides on it and is a little slower.</summary>
        public static readonly TrailKind Frost = new TrailKind
        {
            Index = 0,
            Mutation = Mutation.Frostbound,
            Name = "ice",
            TrailKey = TraitKeys.FrostTrail,
            SeqKey = TraitKeys.FrostTrailSeq,
            Colour = new Color(0.78f, 0.91f, 1f, 0.8f),
            StatusKey = "ecr_slick_ice",
            StatusName = "Slick ice",
            Tooltip = "Frostbound ice underfoot: your feet slide, so starting, turning and stopping all take longer.",
            IconEffects = Array.Empty<string>(),
            IconItems = new[] { "Ice", "Crystal", "FreezeGland" },
            Linger = 0.3f,
            Slips = true,
            Keywords = new[] { "frost", "ice", "cold", "blob", "splat" },
        };

        /// <summary>Mudbound's mud: thick, dark and wet; the player wades through it slower.</summary>
        public static readonly TrailKind Mud = new TrailKind
        {
            Index = 1,
            Mutation = Mutation.Mudbound,
            Name = "mud",
            TrailKey = TraitKeys.MudTrail,
            SeqKey = TraitKeys.MudTrailSeq,
            Colour = new Color(0.2f, 0.14f, 0.07f, 0.93f),
            StatusKey = "ecr_deep_mud",
            StatusName = "Deep mud",
            Tooltip = "Mudbound mud underfoot: thick and deep, it drags at every step and clings a moment after.",
            IconEffects = new[] { "Tared" },
            IconItems = new[] { "Tar", "Guck" },
            Linger = 1f,
            Slips = false,
            Keywords = new[] { "tar", "mud", "blob", "splat" },
        };

        /// <summary>
        /// Flamebound's burning ground (EliteCreaturesReborn 3.22, behavior only): ember-red; a player standing in it is
        /// set on fire - the game's own Burning, from fire damage the patch deals once a second.
        /// </summary>
        public static readonly TrailKind Fire = new TrailKind
        {
            Index = 2,
            Mutation = Mutation.Flamebound,
            Name = "fire",
            TrailKey = TraitKeys.FireTrail,
            SeqKey = TraitKeys.FireTrailSeq,
            Colour = new Color(1f, 0.32f, 0.06f, 0.9f),
            StatusKey = "wildblood_burning_ground",
            StatusName = "Burning ground",
            Tooltip = "Flamebound fire underfoot: the ground burns, and sets you alight while you stand in it.",
            IconEffects = new[] { "Burning" },
            IconItems = new[] { "SurtlingCore", "Resin" },
            Linger = 0.3f,
            Slips = false,
            Burns = true,
            Keywords = new[] { "tar", "fire", "blob", "splat" },
        };

        /// <summary>Every kind, by <see cref="Index"/>.</summary>
        public static readonly TrailKind[] All = { Frost, Mud, Fire };

        /// <summary>The trail a mutation lays; null for a mutation that lays none.</summary>
        public static TrailKind? Of(Mutation mutation)
        {
            foreach (TrailKind kind in All)
            {
                if (kind.Mutation == mutation)
                {
                    return kind;
                }
            }
            return null;
        }

        public int Index { get; private set; }
        public Mutation Mutation { get; private set; }

        /// <summary>A lower-case word for logs and object names.</summary>
        public string Name { get; private set; } = "";

        public string TrailKey { get; private set; } = "";
        public string SeqKey { get; private set; } = "";

        /// <summary>
        /// The tint every patch of this kind is drawn with; its alpha is the patch's opacity at full strength.
        /// </summary>
        public Color Colour { get; private set; }

        /// <summary>The status effect's object name, from which the game takes its identity hash.</summary>
        public string StatusKey { get; private set; } = "";

        public string StatusName { get; private set; } = "";
        public string Tooltip { get; private set; } = "";

        /// <summary>The game's own status effects whose icon to borrow, in order, before the items.</summary>
        public string[] IconEffects { get; private set; } = Array.Empty<string>();

        /// <summary>Item icons to borrow, in order, should no status effect above give one.</summary>
        public string[] IconItems { get; private set; } = Array.Empty<string>();

        /// <summary>
        /// Seconds the status effect (and with it the slow, or the slide) stays after the player steps out.
        /// </summary>
        public float Linger { get; private set; }

        /// <summary>True for ice: a player standing on it loses most of their grip.</summary>
        public bool Slips { get; private set; }

        /// <summary>True for fire: a player standing in it takes the trail's fire damage once a second.</summary>
        public bool Burns { get; private set; }

        /// <summary>
        /// Words a fallback ground decal's effect name is matched against, should the configured one have none.
        /// </summary>
        public string[] Keywords { get; private set; } = Array.Empty<string>();

        private int _statusHash;
        private int _seqHash;

        /// <summary>The status effect's identity hash, worked out once.</summary>
        public int StatusHash => _statusHash != 0 ? _statusHash : _statusHash = StatusKey.GetStableHashCode();

        /// <summary>The drop counter's ZDO hash, worked out once: every machine reads it every frame.</summary>
        public int SeqHash => _seqHash != 0 ? _seqHash : _seqHash = SeqKey.GetStableHashCode();

        /// <summary>
        /// The rule-file setting the decal effect comes from, named in a log line when it fails to resolve.
        /// </summary>
        public string Setting => $"{MutationCatalog.Word(Mutation)} trail effect";
    }
}
