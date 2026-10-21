// Adapted from assets/references/source/ValheimMods-openkeep-3.0.0/GrindstoneSkills/GrindstoneSkills/Woodcutting/Core/FellContext.cs at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Copyright (C) 2026 Gerald Glasgow. SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Saga namespace, configuration, CS1 and ownership boundaries.
using UnityEngine;

namespace ExtraPotions.Saga.Runtime.Adopted.Grindstone
{
    /// <summary>
    /// A tree that just fell, on the tree's owner, handed to every felling feature (<see cref="Felling"/>). The tree
    /// object is still in place (it is destroyed right after); the log and the stub were spawned a moment ago and are
    /// owned by this machine.
    /// </summary>
    public sealed class FellContext
    {
        public TreeBase Tree { get; set; }

        /// <summary>The tree's prefab name, for example "Beech1".</summary>
        public string TreePrefab { get; set; }

        /// <summary>The kind of tree: its log's prefab name ("beech_log"), so variants of one tree count once.</summary>
        public string Species { get; set; }

        public Vector3 Position { get; set; }

        /// <summary>The tree's scale (vegetation spawns at random sizes); the log and its halves get the same.</summary>
        public Vector3 Scale { get; set; }

        /// <summary>The direction of the felling hit, as the game passes it to SpawnLog.</summary>
        public Vector3 HitDir { get; set; }

        /// <summary>The felling hit, after the game applied the tree's resistances.</summary>
        public HitData Hit { get; set; }

        /// <summary>Who felled it; null when the felling hit was no woodcutting hit (fire, a log without a woodcutter).</summary>
        public Woodcutter Woodcutter { get; set; }

        /// <summary>The log that fell; null if the tree has no log prefab.</summary>
        public TreeLog Log { get; set; }

        /// <summary>The stump left behind; null for a tree without one.</summary>
        public Destructible Stub { get; set; }

        public Heightmap.Biome Biome { get; set; }
    }
}
