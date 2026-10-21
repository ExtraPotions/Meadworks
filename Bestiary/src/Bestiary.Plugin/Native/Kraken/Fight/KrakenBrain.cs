// Portions Copyright (C) 2026 Gerald Glasgow. EliteCreaturesPack 0.7.2 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Changed by expDARE, 2026: Bestiary namespace, owned Crucible settings, rebuilt-only bundles and lifecycle adapters.
// SPDX-License-Identifier: GPL-3.0-or-later
using System.Collections.Generic;
using UnityEngine;

namespace ExtraPotions.Bestiary.Runtime.Native.Kraken
{
    /// <summary>
    /// The kraken's mind, in place of the serpent's AI it was copied from (<see cref="KrakenAIPatch"/>). On every machine
    /// it keeps the boss bar in step with the fight. On the owner it decides: lurking and hunting ships
    /// (<see cref="KrakenHunt"/>), holding one and fighting its crew (<see cref="KrakenFight"/>), and leaving. While it
    /// holds a ship or leaves, its body is fixed (kinematic) and placed by hand: under the hull or beside it.
    /// </summary>
    public class KrakenBrain : MonoBehaviour
    {
        /// <summary>The krakens loaded on this machine.</summary>
        public static readonly List<KrakenBrain> Loaded = new List<KrakenBrain>();

        private Character _character = null!;
        private BaseAI _ai = null!;
        private KrakenHunt _hunt = null!;
        private KrakenFight _fight = null!;
        private KrakenPhase _phase = (KrakenPhase)(-1);
        private float _since;
        private float _staggeredAt = float.NegativeInfinity;

        public KrakenState State { get; private set; } = null!;
        public Character Character => _character;
        public KrakenAttacks Attacks { get; private set; } = null!;
        public KrakenFight Fight => _fight;

        /// <summary>OWNER: whether it has just been staggered (a parry, or enough damage at once).</summary>
        public bool Staggered => Time.time - _staggeredAt < 0.5f || _character.IsStaggering();

        /// <summary>Whether its body is placed by hand now (holding a ship or leaving).</summary>
        public bool Pinned => State.Grips || State.Phase == KrakenPhase.Leave;

        private float Age => Time.time - _since;

        private void Awake()
        {
            _character = GetComponent<Character>();
            _ai = GetComponent<BaseAI>();
            State = new KrakenState(GetComponent<ZNetView>());
            KrakenHandOff.Guard(GetComponent<ZNetView>());
            Attacks = GetComponent<KrakenAttacks>();
            _hunt = new KrakenHunt(this, _ai);
            _fight = new KrakenFight(this);
        }

        private void OnEnable() => Loaded.Add(this);

        private void OnDisable() => Loaded.Remove(this);

        /// <summary>OWNER: it was staggered (<see cref="KrakenStaggerPatch"/>).</summary>
        public void OnStagger() => _staggeredAt = Time.time;

        /// <summary>Whether it holds the ship with this id now.</summary>
        public bool Holds(ZDOID ship) => _character != null && !_character.IsDead() && State.Grips && State.Ship == ship;

        /// <summary>In place of the AI's update, on every machine.</summary>
        public void Think(float dt)
        {
            _ai.m_alerted = State.Fighting;
            if (!State.IsOwner || _character.IsDead())
            {
                return;
            }
            KrakenPhase phase = State.Phase;
            if (phase != _phase)
            {
                (_phase, _since) = (phase, Time.time);
            }
            _character.m_swimSpeed = KrakenSettings.SwimSpeed;
            _character.m_body.isKinematic = Pinned;
            Decide(phase, dt);
        }

        /// <summary>OWNER: on to something new.</summary>
        public void Enter(KrakenPhase phase)
        {
            State.SetPhase(phase);
            (_phase, _since) = (phase, Time.time);
        }

        /// <summary>OWNER: its body straight to a place (a fixed body, moved by hand), facing along <paramref name="facing"/>.</summary>
        public void Place(Vector3 at, Vector3 facing, float speed, float dt)
        {
            Rigidbody body = _character.m_body;
            body.MovePosition(Vector3.MoveTowards(body.position, at, speed * dt));
            facing.y = 0f;
            if (facing.sqrMagnitude > 1e-4f)
            {
                body.MoveRotation(Quaternion.RotateTowards(body.rotation, Quaternion.LookRotation(facing), 90f * dt));
            }
        }

        private void Decide(KrakenPhase phase, float dt)
        {
            switch (phase)
            {
                case KrakenPhase.Lurk: _hunt.Lurk(Age, dt); break;
                case KrakenPhase.Hunt: _hunt.Chase(dt); break;
                case KrakenPhase.Leave: _hunt.Leave(Age, dt); break;
                default: _fight.Hold(phase, Age, dt); break;
            }
        }
    }
}
