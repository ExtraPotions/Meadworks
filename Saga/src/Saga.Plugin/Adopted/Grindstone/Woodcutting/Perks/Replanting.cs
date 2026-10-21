// Adapted from assets/references/source/ValheimMods-openkeep-3.0.0/GrindstoneSkills/GrindstoneSkills/Woodcutting/Perks/Replanting.cs at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Copyright (C) 2026 Gerald Glasgow. SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Saga namespace, configuration, CS1 and ownership boundaries.
using UnityEngine;

namespace ExtraPotions.Saga.Runtime.Adopted.Grindstone
{
    /// <summary>
    /// Replanting: a sapling of the same kind takes root, free, where a felled tree stood. On the tree's owner, after
    /// Clean fell (<see cref="Felling"/>), the feller's level share of Replanting At 100 is the chance.
    /// <list type="bullet">
    /// <item>The sapling is the game's own (<see cref="ReplantSaplings"/>); a tree the game has no sapling for (swamp,
    /// snow, Mistlands and Ashlands trees) is never replanted.</item>
    /// <item>It goes on the ground (the terrain height, ZoneSystem.GetGroundHeight) at the tree's position, with a random
    /// yaw, and only where it could grow (<see cref="ReplantSpace"/>): the game destroys a tree sapling that cannot
    /// grow when its time comes. Then the stump comes out the way Clean fell takes it (<see cref="CleanFell.RemoveStump"/>),
    /// if Clean fell left it.</item>
    /// <item>Instantiating the prefab here makes a new ZDO owned by this machine (ZNetView.Awake: CreateNewZDO, with the
    /// prefab's m_persistent), which ZDOMan sends to every peer and the world saves: every client sees the sapling and
    /// it survives a restart. Plant.Awake stamps its plant time, so it grows like one planted with the cultivator (tree
    /// saplings take 3000 to 8000 seconds). Like a wild tree it has no creator (Piece.SetCreator); for a Destructible
    /// sapling that only matters to monster targeting, and tree saplings are targets either way (m_targetNonPlayerBuilt).</item>
    /// <item>It is marked (<see cref="Keys.Replanted"/>) so that the felled log lying on it cannot kill it (<see cref="ReplantWait"/>).</item>
    /// </list>
    /// Saplings are on the piece_nonsolid layer, which collides with nothing, so the falling log never strikes the new
    /// sapling (no ImpactEffect contact) and is not deflected by it.
    /// </summary>
    public static class Replanting
    {
        private static readonly int ReplantedHash = Keys.Replanted.GetStableHashCode();

        /// <summary>Called by <see cref="Felling"/> on the tree's owner, after Clean fell.</summary>
        public static void OnFelled(FellContext fell)
        {
            if (fell.Woodcutter == null || Random.value >= WoodSkill.Share(FellPerkSettings.ReplantingAt100.Value, fell.Woodcutter.Level))
                return;
            Plant sapling = ReplantSaplings.For(fell.TreePrefab, fell.Species);
            if (sapling == null)
                return;
            Vector3 spot = Ground(fell.Position);
            if (!ReplantSpace.CanGrow(sapling, spot, fell))
                return;
            CleanFell.RemoveStump(fell);
            Sow(sapling, spot);
        }

        /// <summary>Whether Replanting planted this sapling; read on whichever machine owns it now.</summary>
        public static bool IsReplanted(Plant plant)
        {
            ZNetView nview = plant != null ? plant.m_nview : null;
            return nview != null && nview.IsValid() && nview.GetZDO().GetBool(ReplantedHash);
        }

        /// <summary>
        /// The point on the terrain under a position. A wild tree stands at the terrain height plus its vegetation entry's
        /// m_groundOffset (ZoneSystem.PlaceVegetation), so its position need not be on the ground.
        /// </summary>
        private static Vector3 Ground(Vector3 position)
        {
            if (ZoneSystem.instance != null)
                position.y = ZoneSystem.instance.GetGroundHeight(position);
            return position;
        }

        private static void Sow(Plant sapling, Vector3 spot)
        {
            Quaternion yaw = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            GameObject planted = Object.Instantiate(sapling.gameObject, spot, yaw);
            ZNetView nview = planted.GetComponent<ZNetView>();
            if (nview != null && nview.IsValid())
                nview.GetZDO().Set(ReplantedHash, true);
        }
    }
}
