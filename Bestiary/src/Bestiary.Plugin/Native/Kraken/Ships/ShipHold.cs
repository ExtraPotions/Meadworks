// Portions Copyright (C) 2026 Gerald Glasgow. EliteCreaturesPack 0.7.2 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Changed by expDARE, 2026: Bestiary namespace, owned Crucible settings, rebuilt-only bundles and lifecycle adapters.
// SPDX-License-Identifier: GPL-3.0-or-later
using UnityEngine;

namespace ExtraPotions.Bestiary.Runtime.Native.Kraken
{
    /// <summary>
    /// A ship in the kraken's grip, on the machine that sails it (the ship's owner, where the game moves it): its sail
    /// comes down and stays down, and it is held where the kraken caught it, free to bob and roll on the waves but not to
    /// sail, drift or turn. The grip is read from the kraken's own ZDO (its phase and the ship's id), so it holds on
    /// whichever machine owns the ship and ends the moment the kraken lets go, leaves or dies. Blows on the ship rock it
    /// there, and a smash damages it through the game's own damage for ships.
    /// </summary>
    public static class ShipHold
    {
        private const float Pull = 0.5f;       // per second: drifting back to the anchor
        private const float MaxDrift = 1f;     // metres a second at most
        private const float Yaw = 0.85f;       // share of its turning kept each step

        /// <summary>The loaded kraken holding this ship, or null.</summary>
        public static KrakenBrain? Holder(Ship ship)
        {
            ZDO? zdo = ship.m_nview != null && ship.m_nview.IsValid() ? ship.m_nview.GetZDO() : null;
            if (zdo == null)
            {
                return null;
            }
            foreach (KrakenBrain brain in KrakenBrain.Loaded)
            {
                if (brain != null && brain.Holds(zdo.m_uid))
                {
                    return brain;
                }
            }
            return null;
        }

        /// <summary>
        /// OWNER OF THE SHIP, as a hit reaches it: while a kraken holds it, the crew's own blows (blades, arrows, fire) are
        /// spared, so they can fight the kraken from the deck without breaking their ship. The kraken's smashes still land.
        /// </summary>
        public static bool Spares(WearNTear part, HitData hit)
        {
            Ship? ship = part.GetComponent<Ship>();
            return ship != null && hit.GetAttacker() is Player && Holder(ship) != null;
        }

        /// <summary>OWNER OF THE SHIP, before the game moves it: no sail while held.</summary>
        public static void Furl(Ship ship)
        {
            if (ship.m_nview != null && ship.m_nview.IsValid() && ship.m_nview.IsOwner() && Holder(ship) != null)
            {
                ship.m_speed = Ship.Speed.Stop;
            }
        }

        /// <summary>OWNER OF THE SHIP, after the game moved it: back towards the anchor, no turning.</summary>
        public static void Keep(Ship ship)
        {
            if (ship.m_nview == null || !ship.m_nview.IsValid() || !ship.m_nview.IsOwner())
            {
                return;
            }
            KrakenBrain? holder = Holder(ship);
            if (holder == null || ship.m_body == null)
            {
                return;
            }
            Vector3 pull = holder.State.Anchor - ship.transform.position;
            Vector3 drift = Vector3.ClampMagnitude(new Vector3(pull.x, 0f, pull.z) * Pull, MaxDrift);
            Rigidbody body = ship.m_body;
            body.linearVelocity = new Vector3(drift.x, body.linearVelocity.y, drift.z);
            Vector3 turning = body.angularVelocity;
            body.angularVelocity = new Vector3(turning.x, turning.y * Yaw, turning.z);
        }

        /// <summary>A blow landing on the ship at a point: it rocks, on the machine that sails it.</summary>
        public static void Rock(Ship ship, Vector3 at, float strength)
        {
            if (ship.m_nview != null && ship.m_nview.IsValid() && ship.m_nview.IsOwner() && ship.m_body != null)
            {
                ship.m_body.AddForceAtPosition(Vector3.down * (ship.m_body.mass * strength), at, ForceMode.Impulse);
            }
        }

        /// <summary>KRAKEN'S OWNER: a smash's damage to the ship (the ship's owner applies it, as for any hit on a ship).</summary>
        public static void Damage(Ship ship, Character kraken, Vector3 at)
        {
            WearNTear? hull = ship.GetComponent<WearNTear>();
            if (hull == null || KrakenSettings.ShipDamage <= 0f)
            {
                return;
            }
            var hit = new HitData { m_point = at, m_dir = Vector3.down, m_toolTier = 100, m_hitType = HitData.HitType.EnemyHit };
            hit.m_damage.m_blunt = KrakenSettings.ShipDamage;
            hit.SetAttacker(kraken);
            hull.Damage(hit);
        }
    }
}
