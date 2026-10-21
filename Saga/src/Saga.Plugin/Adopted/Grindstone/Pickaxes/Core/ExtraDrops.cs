// Adapted from assets/references/source/ValheimMods-openkeep-3.0.0/GrindstoneSkills/GrindstoneSkills/Pickaxes/Core/ExtraDrops.cs at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Copyright (C) 2026 Gerald Glasgow. SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Saga namespace, configuration, CS1 and ownership boundaries.
using System.Collections.Generic;
using UnityEngine;

namespace ExtraPotions.Saga.Runtime.Adopted.Grindstone
{
    /// <summary>
    /// Spawns a break's extra rolls on the rock's owner, the way the game spawns the chunk's own drops. Each roll is one
    /// DropTable.GetDropList() of the chunk's table: m_dropMin..m_dropMax picks by weight, m_dropChance, stack sizes and
    /// the world's resource rate, exactly as for the game's roll. Each item is Object.Instantiate of its prefab (a ZDO
    /// owned by this machine, so every client sees it), then ItemDrop.OnCreateNew with the break's cheated flag.
    /// <list type="bullet">
    /// <item>Chunks (MineRock5, MineRock): scattered in a 0.3 m sphere around the drop spot, as DamageArea and RPC_Hit do.</item>
    /// <item>Single pieces: stacked upwards by the piece's m_spawnYStep in a 0.5 m circle with a random turn, as
    /// DropOnDestroyed does, starting m_dropMax steps up so they land above most of the game's own stack.</item>
    /// </list>
    /// </summary>
    public static class ExtraDrops
    {
        private const float ChunkScatter = 0.3f;
        private const float PieceScatter = 0.5f;

        /// <summary>Spawns <see cref="RockBreak.ExtraRolls"/> rolls of the break's drop table; returns how many items dropped.</summary>
        public static int Spawn(RockBreak broken)
        {
            if (broken == null || broken.Drops == null || broken.ExtraRolls <= 0)
                return 0;
            List<GameObject> items = new List<GameObject>();
            for (int roll = 0; roll < broken.ExtraRolls; roll++)
                items.AddRange(broken.Drops.GetDropList());
            int stackStart = Mathf.Max(0, broken.Drops.m_dropMax);
            for (int i = 0; i < items.Count; i++)
            {
                if (items[i] != null)
                    Drop(items[i], broken, stackStart + i);
            }
            return items.Count;
        }

        private static void Drop(GameObject prefab, RockBreak broken, int index)
        {
            Vector3 position;
            Quaternion rotation = Quaternion.identity;
            if (broken.Chunk >= 0)
                position = broken.Position + Random.insideUnitSphere * ChunkScatter;
            else
            {
                Vector2 circle = Random.insideUnitCircle * PieceScatter;
                position = broken.Position + new Vector3(circle.x, broken.StackStep * index, circle.y);
                rotation = Quaternion.Euler(0f, Random.Range(0, 360), 0f);
            }
            ItemDrop.OnCreateNew(Object.Instantiate(prefab, position, rotation), broken.Cheated);
        }
    }
}
