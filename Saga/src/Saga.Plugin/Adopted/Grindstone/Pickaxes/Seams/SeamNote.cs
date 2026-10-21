// Adapted from assets/references/source/ValheimMods-openkeep-3.0.0/GrindstoneSkills/GrindstoneSkills/Pickaxes/Seams/SeamNote.cs at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Copyright (C) 2026 Gerald Glasgow. SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Saga namespace, configuration, CS1 and ownership boundaries.
using System.Collections.Generic;

namespace ExtraPotions.Saga.Runtime.Adopted.Grindstone
{
    /// <summary>
    /// What one swing did to one rock, on the miner's own client (<see cref="SeamSwing"/>): the chunks it touched, the
    /// damage it dealt each of them, and the chain link of its clean strike, if it struck the seam.
    /// </summary>
    internal sealed class SeamNote
    {
        private readonly Dictionary<int, float> damage = new Dictionary<int, float>(4);

        public SeamNote(Rock rock, ZDOID id)
        {
            Rock = rock;
            Id = id;
        }

        public Rock Rock { get; }

        public ZDOID Id { get; }

        /// <summary>Every chunk the swing touched, as area indices.</summary>
        public List<int> Areas { get; } = new List<int>(4);

        /// <summary>The chain link of the swing's clean strike on this rock (1 = the first of a chain); 0 when none.</summary>
        public int Link { get; private set; }

        public void Strike(int link) => Link = link;

        /// <summary>
        /// Adds a hit on chunk <paramref name="area"/>, its damage counted the way the rock's owner will count it: after
        /// the rock's resistances (MineRock5.DamageArea), on a copy, so the hit itself goes out untouched.
        /// </summary>
        public void Hurt(int area, HitData hit)
        {
            HitData counted = hit.Clone();
            counted.ApplyResistance(Rock.Chunks5.m_damageModifiers, out _);
            damage.TryGetValue(area, out float sum);
            damage[area] = sum + counted.GetTotalDamage();
        }

        /// <summary>The swing's damage to chunk <paramref name="area"/>; 0 when it did not touch it.</summary>
        public float DamageTo(int area) => damage.TryGetValue(area, out float sum) ? sum : 0f;
    }
}
