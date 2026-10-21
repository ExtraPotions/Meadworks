// Portions Copyright (C) 2026 Gerald Glasgow. EliteCreaturesPack 0.7.2 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Changed by expDARE, 2026: Bestiary namespace, owned Crucible settings, rebuilt-only bundles and lifecycle adapters.
// SPDX-License-Identifier: GPL-3.0-or-later
using System.Collections.Generic;
using UnityEngine;

namespace ExtraPotions.Bestiary.Runtime.Native.Kraken
{
    /// <summary>
    /// OWNER: the kraken holding a ship. Having caught it, it dives under the hull (the ship is held from that moment, see
    /// <see cref="ShipHold"/>) and the fight goes round two phases until it dies, `Phase Attacks` attacks each (six by
    /// default), one attack at a time, a random `Attack Gap Min` to `Attack Gap Max` seconds apart with three or more
    /// aboard, a bit longer with two (`Gap Factor Pair`) and a little longer alone (`Gap Factor Solo`), and quicker under the
    /// ship (`Tentacle Gap Factor`). Under the ship: the
    /// six tentacles rise round the hull and slam across the deck one after another, each at the player nearest to it;
    /// one of those (never the first) is the grab, which roars a warning and flings whoever it catches into the sea.
    /// Then the head comes up beside the ship, level with most of the crew, on the side with no ladder (or the crew's
    /// side when both have one, off the ladder), and bites whoever is at the rail, spits ink at
    /// whoever is further off and, while the ink recovers, throws its whole head over the rail at whoever is within its
    /// reach on the deck; about every `Ship Hit Interval` seconds its turn is a tentacle from the far side smashing
    /// across the ship itself. Then back under the ship, and round again. With no one aboard for a while, or the ship
    /// gone, it lets go and leaves. A stagger (a parried bite or ink, or enough damage at once) calls off whatever was
    /// about to land and holds the next attack back a moment.
    /// </summary>
    public class KrakenFight
    {
        private const float DiveTime = 1.8f;
        private const float EmergeTime = 1.6f;       // seconds for the tentacles to rise before the first slam
        private const float Surfacing = 2.2f;        // seconds for the head to come up before it attacks
        private const float Retry = 0.5f;            // seconds to look again when there is nobody to attack
        private const float NoCrewFor = 12f;
        private const float FlinchPause = 2f;
        private const float UnderDepth = 7f;         // metres its body sits under the waterline while under the ship
        private const float PlaceSpeed = 8f;

        private readonly KrakenBrain _brain;
        private readonly List<int> _order = new List<int>();
        private int _attacks, _grab;
        private float _nextAttack, _strikeDone, _headFree, _inkFree, _lastSmash, _noCrew;
        private bool _staggered;

        public KrakenFight(KrakenBrain brain) => _brain = brain;

        private KrakenAttacks Attacks => _brain.Attacks;
        private KrakenState State => _brain.State;

        /// <summary>Caught up with the ship: it dives under it, and holds it from now on where it is.</summary>
        public void Grab(Ship ship)
        {
            State.SetShip(ship.m_nview.GetZDO().m_uid);
            State.SetAnchor(ship.transform.position);
            _noCrew = 0f;
            _brain.Enter(KrakenPhase.Dive);
        }

        public void Hold(KrakenPhase phase, float age, float dt)
        {
            Ship? ship = KrakenShips.Find(State.Ship);
            List<Player>? crew = ship != null ? KrakenShips.Crew(ship) : null;
            _noCrew = crew != null && crew.Count > 0 ? 0f : _noCrew + dt;
            if (ship == null || crew == null || _noCrew > NoCrewFor)
            {
                _brain.Enter(KrakenPhase.Leave);
                return;
            }
            PlaceBody(phase, ship, dt);
            Flinch(age);
            if (phase == KrakenPhase.Dive && age >= DiveTime)
            {
                StartTentacles();
            }
            else if (phase == KrakenPhase.Tentacles)
            {
                Tentacles(ship, age, crew);
            }
            else if (phase == KrakenPhase.Head)
            {
                Head(ship, age, crew);
            }
        }

        private void StartTentacles()
        {
            _order.Clear();
            for (int i = 0; i < KrakenBody.Tentacles; i++)
            {
                _order.Insert(Random.Range(0, _order.Count + 1), i);
            }
            (_attacks, _nextAttack, _strikeDone) = (0, EmergeTime, 0f);
            _grab = Random.Range(1, Mathf.Max(2, KrakenSettings.PhaseAttacks));
            _brain.Enter(KrakenPhase.Tentacles);
        }

        // One tentacle after another at the crew; after the last has landed and lifted, the head comes up.
        private void Tentacles(Ship ship, float age, List<Player> crew)
        {
            if (_order.Count == 0)
            {
                StartTentacles();
            }
            else if (_attacks < KrakenSettings.PhaseAttacks && age >= _nextAttack)
            {
                int arm = _order[_attacks % _order.Count];
                StrikeKind kind = _attacks == _grab ? StrikeKind.Grab : StrikeKind.Slam;
                Attacks.Slam(arm, KrakenTargets.SlamTarget(ship, arm, crew), kind);
                float gap = KrakenSettings.SlamGap(crew.Count) + (kind == StrikeKind.Grab ? 1f : 0f);   // the grab's warning is longer
                (_attacks, _nextAttack, _strikeDone) = (_attacks + 1, age + gap, age + TentacleStrike.EndOf(kind));
            }
            else if (_attacks >= KrakenSettings.PhaseAttacks && age >= _strikeDone + 0.3f)
            {
                StartHead(ship, crew);
            }
        }

        private void StartHead(Ship ship, List<Player> crew)
        {
            (int side, float along) = KrakenTargets.HeadPlace(ship, crew);
            State.SetHead(side, along);
            // The first smash is due halfway through the interval, so a head phase of six attacks holds two of them.
            float smashed = -KrakenSettings.ShipHitInterval / 2f;
            (_attacks, _nextAttack, _strikeDone, _headFree, _inkFree, _lastSmash) = (0, Surfacing, 0f, 0f, 0f, smashed);
            _brain.Enter(KrakenPhase.Head);
        }

        // Its attacks one at a time; after the last has finished, back under the ship.
        private void Head(Ship ship, float age, List<Player> crew)
        {
            if (_attacks >= KrakenSettings.PhaseAttacks)
            {
                if (age >= _headFree && age >= _strikeDone)
                {
                    StartTentacles();
                }
                return;
            }
            if (age < _nextAttack)
            {
                return;
            }
            bool attacked = HeadAttack(ship, age, State.Side, crew);
            _attacks += attacked ? 1 : 0;
            _nextAttack = age + (attacked ? KrakenSettings.AttackGap(crew.Count) : Retry);
        }

        // A smash on the ship when one is due; otherwise a bite at the rail, ink further off, or the whole head lunging
        // at someone within its reach; with nobody in reach of the head, a smash all the same.
        private bool HeadAttack(Ship ship, float age, int side, List<Player> crew)
        {
            Player? bitten = KrakenTargets.Biteable(ship, side, State.Along, crew);
            Player? inked = bitten == null && age >= _inkFree ? KrakenTargets.Inkable(ship, side, State.Along, crew) : null;
            bitten = bitten == null && inked == null ? KrakenTargets.Lungeable(ship, side, State.Along, crew) : bitten;
            if (age - _lastSmash >= KrakenSettings.ShipHitInterval || (bitten == null && inked == null))
            {
                return Smash(ship, age, side, crew.Count > 0);
            }
            if (bitten != null)
            {
                Attacks.Bite(KrakenTargets.Chest(ship, bitten));
                _headFree = age + HeadAction.BiteEnd;
                return true;
            }
            Attacks.Ink(KrakenTargets.Chest(ship, inked!));
            (_headFree, _inkFree) = (age + HeadAction.InkEnd, age + KrakenSettings.InkInterval);
            return true;
        }

        private bool Smash(Ship ship, float age, int side, bool anyone)
        {
            if (!anyone)
            {
                return false;
            }
            int arm = KrakenTargets.SmashArm(side);
            Attacks.Slam(arm, KrakenTargets.SmashTarget(ship, arm, side), StrikeKind.Smash);
            (_lastSmash, _strikeDone) = (age, age + TentacleStrike.EndOf(StrikeKind.Smash));
            return true;
        }

        // A fresh stagger calls off what was coming and holds the next attack back a moment.
        private void Flinch(float age)
        {
            bool staggered = _brain.Staggered;
            if (staggered && !_staggered)
            {
                Attacks.Flinch();
                _nextAttack = Mathf.Max(_nextAttack, age + FlinchPause);
                _headFree = Mathf.Max(_headFree, age + FlinchPause);
            }
            _staggered = staggered;
        }

        // Its body under the hull, or up beside it with the head in the head phase.
        private void PlaceBody(KrakenPhase phase, Ship ship, float dt)
        {
            Vector3 at = phase == KrakenPhase.Head
                ? KrakenTargets.HeadSpot(ship, State.Side, State.Along, KrakenTargets.BesideDepth)
                : ship.transform.TransformPoint(new Vector3(0f, KrakenShips.Waterline(ship) - UnderDepth, ShipHull.Of(ship).MiddleZ));
            _brain.Place(at, ship.transform.position - at, PlaceSpeed, dt);
        }
    }
}
