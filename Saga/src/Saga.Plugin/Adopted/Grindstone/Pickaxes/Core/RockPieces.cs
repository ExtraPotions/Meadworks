// Adapted from assets/references/source/ValheimMods-openkeep-3.0.0/GrindstoneSkills/GrindstoneSkills/Pickaxes/Core/RockPieces.cs at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Copyright (C) 2026 Gerald Glasgow. SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Saga namespace, configuration, CS1 and ownership boundaries.
using UnityEngine;

namespace ExtraPotions.Saga.Runtime.Adopted.Grindstone
{
    /// <summary>
    /// Rocks that are a single Destructible, read from the prefab (every machine alike). Two sorts, told apart by what
    /// they leave behind:
    /// <list type="bullet">
    /// <item><b>Single pieces</b> (tin, obsidian, small boulders): a DropOnDestroyed drops their items when they break.</item>
    /// <item><b>Intact deposits</b> (rock4_copper, silvervein, rock3_silver, mudpile, big boulders): 1 health, no drops of
    /// their own; their m_spawnWhenDestroyed is a MineRock5 (the "_frac" form) that Destructible.Destroy instantiates at
    /// the same position and rotation and damages with the same hit.</item>
    /// </list>
    /// </summary>
    public static class RockPieces
    {
        /// <summary>The items a single piece drops when it breaks; null for an intact deposit or a piece without drops.</summary>
        public static DropTable Drops(Destructible piece)
        {
            DropOnDestroyed drops = piece != null ? piece.GetComponent<DropOnDestroyed>() : null;
            DropTable table = drops != null ? drops.m_dropWhenDestroyed : null;
            return table != null && table.m_drops != null && table.m_drops.Count > 0 ? table : null;
        }

        /// <summary>The rock an intact deposit turns into (its m_spawnWhenDestroyed's MineRock5 or MineRock); null otherwise.</summary>
        public static Component SpawnedRock(Destructible piece)
        {
            GameObject spawned = piece != null ? piece.m_spawnWhenDestroyed : null;
            if (spawned == null)
                return null;
            MineRock5 chunks = spawned.GetComponent<MineRock5>();
            if (chunks != null)
                return chunks;
            return spawned.GetComponent<MineRock>();
        }

        /// <summary>The <see cref="RockInfo"/> of a Destructible whose damage modifiers already make it a rock.</summary>
        public static RockInfo Classify(Destructible piece, string prefab)
        {
            Component spawned = SpawnedRock(piece);
            DropTable own = Drops(piece);
            DropTable drops = own ?? SpawnedTable(spawned);
            return new RockInfo
            {
                Prefab = prefab,
                Kind = RockCatalog.KindOf(spawned != null ? spawned.gameObject.name : prefab),
                Name = NameOf(piece, spawned, drops, prefab),
                IsOre = RockCatalog.HasOre(drops),
                Tier = piece.m_minToolTier,
                Health = piece.m_health,
                HasChunks = false,
                Fractures = spawned != null && own == null,
                HasBeacon = piece.GetComponentInChildren<Beacon>(true) != null,
            };
        }

        private static DropTable SpawnedTable(Component spawned)
        {
            switch (spawned)
            {
                case MineRock5 chunks:
                    return chunks.m_dropItems;
                case MineRock chunks:
                    return chunks.m_dropItems;
                default:
                    return null;
            }
        }

        /// <summary>
        /// A Destructible has no name of its own: its HoverText, else the rock it turns into, else its ore's name
        /// (<see cref="RockCatalog.FallbackName"/>, from <paramref name="drops"/>, its own or its fractured form's table).
        /// </summary>
        private static string NameOf(Destructible piece, Component spawned, DropTable drops, string prefab)
        {
            HoverText hover = piece.GetComponent<HoverText>();
            if (hover != null && !string.IsNullOrEmpty(hover.m_text))
                return hover.m_text;
            string spawnedName = spawned is MineRock5 chunks ? chunks.m_name : spawned is MineRock rock ? rock.m_name : null;
            return string.IsNullOrEmpty(spawnedName) ? RockCatalog.FallbackName(drops, prefab) : spawnedName;
        }
    }
}
