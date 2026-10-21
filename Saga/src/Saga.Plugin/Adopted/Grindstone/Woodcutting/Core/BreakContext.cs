// Adapted from assets/references/source/ValheimMods-openkeep-3.0.0/GrindstoneSkills/GrindstoneSkills/Woodcutting/Core/BreakContext.cs at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Copyright (C) 2026 Gerald Glasgow. SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Saga namespace, configuration, CS1 and ownership boundaries.
using UnityEngine;

namespace ExtraPotions.Saga.Runtime.Adopted.Grindstone
{
    /// <summary>
    /// A log breaking, on the log's owner (<see cref="LogBreaking"/>): a whole log splitting into halves, or a half
    /// breaking into wood. Everything a feature needs is read before the game destroys the log's ZDO.
    /// </summary>
    public sealed class BreakContext
    {
        public TreeLog Log { get; set; }

        /// <summary>The log's prefab name, for example "beech_log_half".</summary>
        public string LogPrefab { get; set; }

        public Vector3 Position { get; set; }

        /// <summary>The log's scale: the scale of the tree it came from.</summary>
        public Vector3 Scale { get; set; }

        /// <summary>The breaking hit as the game passes it to TreeLog.Destroy (a copy taken before resistances).</summary>
        public HitData Hit { get; set; }

        /// <summary>The woodcutter of the breaking hit; null when it was no woodcutting hit (fire, a creature, the log landing).</summary>
        public Woodcutter Breaker { get; set; }

        /// <summary>The woodcutter who felled the tree, stored on the log; null for a log felled without Saga.</summary>
        public Woodcutter Feller { get; set; }

        /// <summary>Whose level a yield bonus follows: the breaker when there is one, otherwise the feller.</summary>
        public Woodcutter Woodcutter => Breaker ?? Feller;

        /// <summary>The log was split cleanly (<see cref="Keys.CleanSplit"/>); its halves inherit the mark.</summary>
        public bool Clean { get; set; }

        /// <summary>
        /// How big the log's tree was within its kind's size range, 0..1 (<see cref="Keys.WoodSize"/>, written by
        /// <see cref="OldGrowth"/> when the tree fell); -1 when unknown. Halves inherit it.
        /// </summary>
        public float Size { get; set; } = -1f;

        /// <summary>This break drops wood (a half); false for a whole log that only splits into halves.</summary>
        public bool DropsWood { get; set; }

        /// <summary>The yield bonus applied to this break's drops: 0 for the game's own amount, 0.5 for half as much again.</summary>
        public float YieldBonus { get; set; }
    }
}
