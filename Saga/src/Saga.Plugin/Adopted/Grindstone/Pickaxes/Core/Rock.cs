// Adapted from assets/references/source/ValheimMods-openkeep-3.0.0/GrindstoneSkills/GrindstoneSkills/Pickaxes/Core/Rock.cs at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Copyright (C) 2026 Gerald Glasgow. SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Saga namespace, configuration, CS1 and ownership boundaries.
using UnityEngine;

namespace ExtraPotions.Saga.Runtime.Adopted.Grindstone
{
    /// <summary>
    /// One rock in the world: its rock component (a MineRock5, MineRock or Destructible), what its prefab says
    /// (<see cref="RockInfo"/>, from <see cref="RockCatalog"/>) and where it stands. Built on any machine from a
    /// component; <see cref="Of"/> returns null for anything that is not a rock, so a non-null Rock is always one.
    /// Cheap to build (one cached lookup); the biome is read on first use.
    /// </summary>
    public sealed class Rock
    {
        private Heightmap.Biome biome;
        private bool biomeRead;

        private Rock(Component target, RockInfo info)
        {
            Target = target;
            Info = info;
        }

        /// <summary>The rock's MineRock5, MineRock or Destructible.</summary>
        public Component Target { get; }

        public RockInfo Info { get; }

        public string Kind => Info.Kind;
        public bool IsOre => Info.IsOre;
        public int Tier => Info.Tier;
        public bool HasChunks => Info.HasChunks;
        public bool HasBeacon => Info.HasBeacon;

        /// <summary>
        /// The name as the player reads it: <see cref="RockInfo.Name"/> localized ("Copper deposit"; "Ice" for an ice rock
        /// that has no name of its own). Echo and every callout use it.
        /// </summary>
        public string DisplayName => GameLocalization.instance != null ? GameLocalization.instance.Localize(Info.Name) : Info.Name;

        /// <summary>A multi-chunk rock, the only kind with seams and splash; null for the others.</summary>
        public MineRock5 Chunks5 => Target as MineRock5;

        /// <summary>An older multi-piece rock (MineRock_Copper, Leviathan, meteorites); null for the others.</summary>
        public MineRock Chunks => Target as MineRock;

        /// <summary>A single-piece rock or an intact deposit; null for the others.</summary>
        public Destructible Piece => Target as Destructible;

        public ZNetView View
        {
            get
            {
                switch (Target)
                {
                    case MineRock5 chunks:
                        return chunks.m_nview;
                    case MineRock chunks:
                        return chunks.m_nview;
                    case Destructible piece:
                        return piece.m_nview;
                    default:
                        return null;
                }
            }
        }

        /// <summary>The rock still exists in the world (its ZDO is valid on this machine).</summary>
        public bool IsValid
        {
            get
            {
                ZNetView view = Target != null ? View : null;
                return view != null && view.IsValid();
            }
        }

        /// <summary>This machine owns the rock's ZDO: it applies damage and spawns drops.</summary>
        public bool IsOwner => IsValid && View.IsOwner();

        /// <summary>The rock's ZDOID; none once it is destroyed.</summary>
        public ZDOID Id => IsValid ? View.GetZDO().m_uid : ZDOID.None;

        public Vector3 Position => Target.transform.position;

        /// <summary>The biome at the rock's position: from the loaded terrain, else from the world generator.</summary>
        public Heightmap.Biome Biome
        {
            get
            {
                if (!biomeRead)
                {
                    biome = BiomeAt(Position);
                    biomeRead = true;
                }
                return biome;
            }
        }

        /// <summary>The rock a component belongs to; null when it is no rock.</summary>
        public static Rock Of(Component target)
        {
            RockInfo info = RockCatalog.Info(target);
            return info == null ? null : new Rock(target, info);
        }

        /// <summary>The rock a game object (or a chunk collider's object) belongs to, looking up the parents; null when none.</summary>
        public static Rock Find(GameObject gameObject)
        {
            if (gameObject == null)
                return null;
            Component target = gameObject.GetComponentInParent<MineRock5>();
            if (target == null)
                target = gameObject.GetComponentInParent<MineRock>();
            if (target == null)
                target = gameObject.GetComponentInParent<Destructible>();
            return Of(target);
        }

        public static Heightmap.Biome BiomeAt(Vector3 position)
        {
            Heightmap.Biome found = Heightmap.FindBiome(position);
            if (found == Heightmap.Biome.None && WorldGenerator.instance != null)
                found = WorldGenerator.instance.GetBiome(position);
            return found;
        }
    }
}
