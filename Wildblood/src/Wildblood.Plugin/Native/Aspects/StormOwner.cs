// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using System.Collections.Generic;
using ExtraPotions.Wildblood.Adopted.Runtime;
using ExtraPotions.Wildblood.Adopted.Util;
using UnityEngine;

namespace ExtraPotions.Wildblood.Adopted.Aspects
{
    /// <summary>
    /// The Stormbound decision, on whichever machine owns the boss right now. It counts seconds of fight - the boss
    /// awake and alerted with at least one player within `range` - and every `every` of them calls a storm: one circle
    /// under each such player where they stand, written into the boss's ZDO with the moment the lightning falls
    /// (<see cref="StormStore"/>). A lull only pauses the count; <see cref="ResetAfter"/> seconds out of the fight end
    /// it, and the next starts a full interval. A machine that takes the boss over reads when the last storm fell and
    /// keeps the rhythm, so a hand-over never brings a second storm at once. Nothing is drawn or hurt here.
    /// </summary>
    internal sealed class StormOwner
    {
        /// <summary>Seconds out of the fight that end it (players gone, boss calmed); less only pauses.</summary>
        private const float ResetAfter = 10f;

        private readonly EliteController _controller;
        private readonly Character _boss;
        private readonly BaseAI _ai;
        private readonly List<Player> _players = new List<Player>();
        private float _fight;
        private float _due;
        private float _idle;
        private bool _primed;

        public StormOwner(EliteController controller, Character boss, BaseAI ai)
        {
            _controller = controller;
            _boss = boss;
            _ai = ai;
        }

        public void Tick()
        {
            if (_ai == null || _boss.IsDead() || !_controller.IsOwner())
            {
                _primed = false; // not the owner (any more): owning it again re-reads the last storm
                return;
            }
            StormSettings settings = StormSettings.Read();
            ZDO zdo = _controller.View.GetZDO();
            if (Due(InFight(settings.Range), zdo, settings))
            {
                Call(zdo, settings);
            }
        }

        /// <summary>Awake, alerted and with a player in reach; fills the player list the storm is called on.</summary>
        private bool InFight(float range) =>
            _ai.IsAlerted() && !_ai.IsSleeping()
            && StormTargets.InRange(_boss.transform.position, range, _players).Count > 0;

        private bool Due(bool inFight, ZDO zdo, StormSettings settings)
        {
            if (settings.Every <= 0f || zdo == null)
            {
                return false;
            }
            if (!inFight)
            {
                _idle += Time.deltaTime;
                _primed = _primed && _idle < ResetAfter;
                return false;
            }
            _idle = 0f;
            if (!_primed)
            {
                Prime(zdo, settings);
            }
            _fight += Time.deltaTime;
            return _fight >= _due;
        }

        // A fight begins, or this machine has just taken the boss: a full interval, unless a storm fell less than an
        // interval ago - then the rhythm carries on from it.
        private void Prime(ZDO zdo, StormSettings settings)
        {
            _primed = true;
            _fight = 0f;
            _due = settings.Every;
            long strikeAt = StormStore.StrikeAt(zdo);
            float sinceCircles = NetTime.SecondsSince(strikeAt) + settings.Tell;
            if (strikeAt > 0L && sinceCircles < settings.Every)
            {
                _due = Mathf.Max(0f, settings.Every - sinceCircles);
            }
        }

        /// <summary>One storm: a circle under each player in reach, lightning `tell time` from now.</summary>
        private void Call(ZDO zdo, StormSettings settings)
        {
            List<Vector3> centers = new List<Vector3>(_players.Count);
            foreach (Player player in _players)
            {
                centers.Add(StormTargets.Under(player.transform.position));
            }
            long strikeAt = NetTime.NowMs() + (long)(settings.Tell * 1000f);
            StormStore.Write(zdo, strikeAt, settings.Radius, settings.Damage, centers);
            _fight = 0f;
            _due = settings.Every;
            if (Log.Diagnostics)
            {
                Log.Diag($"{_boss.name}: Stormbound calls lightning on {centers.Count} circle(s)");
            }
        }
    }
}
