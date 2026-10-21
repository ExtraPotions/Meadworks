// Portions Copyright (C) 2026 Gerald Glasgow. EliteCreaturesPack 0.7.2 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Changed by expDARE, 2026: Bestiary namespace, owned Crucible settings, rebuilt-only bundles and lifecycle adapters.
// SPDX-License-Identifier: GPL-3.0-or-later
using UnityEngine;

namespace ExtraPotions.Bestiary.Runtime.Native.Kraken
{
    /// <summary>
    /// OWNER: the kraken away from a ship. Lurking, it cruises slowly under the surface in deep water until a ship with a
    /// crew comes within `Hunt Range`, then hunts it: it swims straight for it at `Swim Speed` (the game's own swimming),
    /// never into water shallower than `Min Depth`. A ship that stays further than `Hunt Range` for a few seconds has
    /// outrun it, and one that stays in the shallows has escaped it: either way it leaves, as it does after lurking a
    /// minute and a half with nothing to hunt. Reaching the ship's hull, it grabs it (<see cref="KrakenFight.Grab"/>).
    /// Leaving, it sinks away and is gone for good a few seconds later.
    /// </summary>
    public class KrakenHunt
    {
        private const float GiveUp = 90f;        // seconds lurking with nothing to hunt before it leaves
        private const float LostFor = 8f;        // seconds a hunted ship may stay out of range
        private const float StuckFor = 20f;      // seconds it waits at the edge of shallow water
        private const float Catch = 3f;          // metres from the hull at which it has the ship
        private const float Ahead = 6f;          // metres ahead it looks for shallow water
        private const float Wander = 10f;        // seconds between lurking turns
        private const float LurkSpeed = 0.4f;    // share of its swim speed while lurking
        private const float SinkSpeed = 2f;
        private const float Gone = 5f;           // seconds from starting to leave to gone

        private readonly KrakenBrain _brain;
        private readonly BaseAI _ai;
        private Vector3 _wander = Vector3.forward;
        private float _lost, _stuck, _turnAt;

        public KrakenHunt(KrakenBrain brain, BaseAI ai)
        {
            _brain = brain;
            _ai = ai;
        }

        private Vector3 Position => _brain.transform.position;

        public void Lurk(float age, float dt)
        {
            Ship? ship = KrakenShips.Nearest(Position, KrakenSettings.HuntRange);
            if (ship != null && ship.m_nview != null && ship.m_nview.IsValid())
            {
                _brain.State.SetShip(ship.m_nview.GetZDO().m_uid);
                (_lost, _stuck) = (0f, 0f);
                _brain.Enter(KrakenPhase.Hunt);
                return;
            }
            if (age > GiveUp)
            {
                _brain.Enter(KrakenPhase.Leave);
                return;
            }
            _brain.Character.m_swimSpeed = KrakenSettings.SwimSpeed * LurkSpeed;
            Roam(age);
        }

        public void Chase(float dt)
        {
            Ship? ship = KrakenShips.Find(_brain.State.Ship);
            if (ship == null || KrakenShips.Crew(ship).Count == 0)
            {
                _brain.Enter(KrakenPhase.Lurk);
                return;
            }
            Vector3 offset = ship.transform.position - Position;
            _lost = KrakenShips.Level(offset) > KrakenSettings.HuntRange ? _lost + dt : 0f;
            if (_lost > LostFor)
            {
                _brain.Enter(KrakenPhase.Leave);
            }
            else if (ShipHull.Of(ship).Holds(ship.transform.InverseTransformPoint(Position), Catch))
            {
                _brain.Fight.Grab(ship);
            }
            else
            {
                Swim(new Vector3(offset.x, 0f, offset.z).normalized, dt);
            }
        }

        /// <summary>Sinking away; gone for good once it has been at it a few seconds.</summary>
        public void Leave(float age, float dt)
        {
            _brain.Place(Position + Vector3.down * 10f, _brain.transform.forward, SinkSpeed, dt);
            if (age > Gone)
            {
                KrakenBoss.Forget(_brain.Character);
                ZNetScene.instance.Destroy(_brain.gameObject);
            }
        }

        private void Swim(Vector3 direction, float dt)
        {
            if (Deep(Position + direction * Ahead))
            {
                _stuck = 0f;
                _ai.MoveTowards(direction, run: true);
                return;
            }
            _ai.StopMoving();
            _stuck += dt;
            if (_stuck > StuckFor)
            {
                _brain.Enter(KrakenPhase.Leave);
            }
        }

        // Cruising under the surface, turning now and then, keeping to deep water.
        private void Roam(float age)
        {
            if (age >= _turnAt)
            {
                _turnAt = age + Wander;
                _wander = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) * Vector3.forward;
            }
            if (Deep(Position + _wander * Ahead))
            {
                _ai.MoveTowards(_wander, run: false);
                return;
            }
            _ai.StopMoving();
            _turnAt = age;
        }

        private static bool Deep(Vector3 at) => KrakenShips.Depth(at) >= KrakenSettings.MinDepth;
    }
}
