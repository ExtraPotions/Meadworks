// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using ExtraPotions.Wildblood.Adopted.Rules;
using ExtraPotions.Wildblood.Adopted.Runtime;
using ExtraPotions.Wildblood.Adopted.Traits;
using ExtraPotions.Wildblood.AdoptedLibs.PatchGuard;
using UnityEngine;

namespace ExtraPotions.Wildblood.Adopted.Mutations
{
    /// <summary>
    /// Relentless: once it picks a target it does not give up while that target is within `chase distance`. The game's
    /// monster AI still does all the hunting; this holds on to the one target it picked (its quarry,
    /// <see cref="RelentlessQuarry"/>) and switches off, one by one, each way the game lets a hunter lose interest
    /// (<see cref="RelentlessChase"/>), so a player escapes only by getting beyond the chase distance or by killing it.
    /// Sneaking is handled beside it (<see cref="RelentlessSenses"/>), and the patches that reach in here are the
    /// Relentless* patches. Attached on every machine but active only where the creature's AI runs, its owner: a
    /// machine that loses ownership lets go, and the next owner picks the quarry back up from the ZDO.
    /// </summary>
    public sealed class RelentlessBehaviour : MonoBehaviour
    {
        private EliteController _controller = null!;
        private MonsterAI _ai = null!;
        private RelentlessQuarry _quarry = null!;
        private Pathfinding.AgentType _homePath;
        private Pathfinding.AgentType _chasePath;
        private bool _registered;

        /// <summary>True on the owner while it holds a quarry within chase distance, as of its last target update.</summary>
        private bool _hunting;
        internal bool Active => isActiveAndEnabled && _controller != null && _controller.Ready;
        public bool Hunting { get => Active && _hunting; private set => _hunting = value; }

        /// <summary>The quarry it is hunting, or null while it hunts nothing (and always on a machine that does not own it).</summary>
        public Character? Quarry => Hunting ? _quarry.Current : null;

        private void Start() => Guard.Run("RelentlessBehaviour.Start", Setup);

        private void Setup()
        {
            _controller = GetComponent<EliteController>();
            _ai = GetComponent<MonsterAI>();
            Character body = GetComponent<Character>();
            if (_controller == null || _ai == null || body == null)
            {
                enabled = false; // no hunting AI (an animal that only ever flees): there is no target to hold on to
                return;
            }
            float chase = _controller.Rules.PowerOf(Mutation.Relentless, Fields.ChaseDistance);
            _quarry = new RelentlessQuarry(_controller.View, _ai, chase);
            _homePath = _ai.m_pathAgentType;
            _chasePath = RelentlessChase.SwimmingPath(body, _homePath);
            RelentlessHunters.Add(_ai, this);
            _registered = true;
        }

        private void OnDisable()
        {
            Hunting = false;
            _quarry?.Release();
            if (_ai != null && _ai.m_pathAgentType == _chasePath) _ai.m_pathAgentType = _homePath;
        }

        private void OnDestroy()
        {
            OnDisable();
            if (_registered)
            {
                RelentlessHunters.Remove(_ai); // the reference, not a Unity null check: the AI may be torn down first
            }
        }

        private void Update() => Guard.Run("RelentlessBehaviour.Update", static self => self.Step(), this);

        // Owner-only state: a machine that does not own the creature holds nothing, so when it becomes the owner it
        // reads the quarry fresh from the ZDO rather than trusting a memory from before someone else drove it.
        private void Step()
        {
            if (!_controller.IsOwner())
            {
                Hunting = false;
                _quarry.Release();
                _ai.m_pathAgentType = _homePath;
            }
        }

        /// <summary>
        /// The owner's hold on its quarry, run right after the game's own target update. It adopts the target the game
        /// picked when it has none, keeps one it has, and tells the game whether to treat the quarry as heard: true
        /// whenever it is chasing its quarry, so the game keeps moving it toward where the quarry is, seen or not.
        /// </summary>
        public bool Pursue(bool able, bool heard, bool seen)
        {
            Character? quarry = able ? _quarry.Hold(_ai.m_targetCreature) : null;
            Hunting = quarry != null;
            _ai.m_pathAgentType = Hunting ? _chasePath : _homePath; // into the water after it, only while hunting
            if (quarry == null)
            {
                return heard;
            }
            RelentlessChase.HoldOn(_ai, quarry);
            return RelentlessChase.Track(_ai, quarry, heard, seen);
        }

        /// <summary>The game is choosing a target: while it holds a quarry it can go after right now, that quarry.</summary>
        public Character? Resume()
        {
            Character? quarry = _quarry.Hold(null);
            return quarry != null && RelentlessChase.Targetable(_ai, quarry) ? quarry : null;
        }

        /// <summary>
        /// The game found it no attack it may use right now (most often: none that works while swimming) and would have
        /// it wander. While it is after its quarry it keeps closing in instead. False leaves the game's wander in place.
        /// </summary>
        public bool ChaseUnarmed(float dt)
        {
            Character? quarry = Quarry;
            if (quarry == null || _ai.m_targetCreature != quarry)
            {
                return false;
            }
            _ai.MoveTo(dt, quarry.transform.position, 0f, _ai.IsAlerted());
            return true;
        }
    }
}
