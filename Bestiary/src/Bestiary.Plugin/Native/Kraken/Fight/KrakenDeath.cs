// Portions Copyright (C) 2026 Gerald Glasgow. EliteCreaturesPack 0.7.2 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Changed by expDARE, 2026: Bestiary namespace, owned Crucible settings, rebuilt-only bundles and lifecycle adapters.
// SPDX-License-Identifier: GPL-3.0-or-later
using UnityEngine;

namespace ExtraPotions.Bestiary.Runtime.Native.Kraken
{
    /// <summary>
    /// OWNER, as the kraken dies. The game makes a creature's corpse where its body is and drops its loot round it; the
    /// kraken's body may be deep under a ship, so first it is moved to where its head is seen (up beside the ship, facing
    /// it, or where it swam), a little under the surface, and its loot is aimed at the held ship's deck (or just over the
    /// water where it died, where the game's items float). Items other mods drop in its death go to the same spot
    /// (<see cref="KrakenDropSpot"/>).
    /// </summary>
    public static class KrakenDeath
    {
        private const float CorpseDepth = 1.5f;
        private const float OverDeck = 0.6f;

        public static void Prepare(KrakenBrain brain)
        {
            Ship? ship = Held(brain);
            Vector3 at = ship != null
                ? KrakenTargets.HeadSpot(ship, brain.State.Side, brain.State.Along, CorpseDepth)
                : Surface(brain.transform.position, -CorpseDepth);
            Vector3 facing = ship != null ? ship.transform.position - at : brain.transform.forward;
            facing.y = 0f;
            Quaternion turn = facing.sqrMagnitude > 1e-4f ? Quaternion.LookRotation(facing) : brain.transform.rotation;
            brain.transform.SetPositionAndRotation(at, turn);
            brain.Character.m_body.position = at;
        }

        public static void AimLoot(KrakenBrain brain, CharacterDrop drop)
        {
            Vector3 target = LootSpot(brain);
            drop.m_spawnOffset = drop.transform.InverseTransformVector(target - brain.Character.GetCenterPoint());
        }

        /// <summary>Where its loot lands: over the middle of the held ship's deck, or just over the water where it died.</summary>
        public static Vector3 LootSpot(KrakenBrain brain)
        {
            Ship? ship = Held(brain);
            ShipHull? hull = ship != null ? ShipHull.Of(ship) : null;
            return ship != null && hull != null
                ? ship.transform.TransformPoint(new Vector3(0f, hull.Rail + OverDeck, hull.MiddleZ))
                : Surface(brain.transform.position, OverDeck);
        }

        private static Ship? Held(KrakenBrain brain) => brain.State.Grips ? KrakenShips.Find(brain.State.Ship) : null;

        private static Vector3 Surface(Vector3 at, float up) => new Vector3(at.x, KrakenShips.Water(at) + up, at.z);
    }
}
