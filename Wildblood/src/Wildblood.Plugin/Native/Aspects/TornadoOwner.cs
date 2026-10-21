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
    /// The Nightfall decision, on whichever machine owns the boss right now. It counts seconds of fight - the boss
    /// awake and alerted with at least one player within `range` - and when the drawn delay (`every` to `every max`)
    /// has passed it raises a wave: one tornado for each such player not already hunted by a live one
    /// (<see cref="TornadoTargets"/>), written into the boss's ZDO with the moment it rose and the delay drawn for the
    /// next (<see cref="TornadoStore"/>). The first wave comes a full delay into the fight. A lull only pauses the
    /// count; <see cref="ResetAfter"/> seconds out of the fight end it. A machine that takes the boss over reads when
    /// the last wave rose and the delay it meant to keep, so a hand-over never brings a second wave at once, and carries
    /// the live wave's hunt on (<see cref="TornadoHunt"/>). Nothing is drawn or hurt here.
    /// </summary>
    internal sealed class TornadoOwner
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

        public TornadoOwner(EliteController controller, Character boss, BaseAI ai)
        {
            _controller = controller;
            _boss = boss;
            _ai = ai;
        }

        /// <summary>The live wave as this machine moves it; empty unless it owns the boss.</summary>
        public TornadoHunt Hunt { get; } = new TornadoHunt();

        public void Tick()
        {
            if (_ai == null || !_controller.IsOwner())
            {
                _primed = false; // not the owner (any more): owning it again re-reads the last wave
                Hunt.Release();
                return;
            }
            ZDO zdo = _controller.View.GetZDO();
            if (!_boss.IsDead())
            {
                TornadoSettings settings = TornadoSettings.Read();
                if (Due(InFight(settings.Range), zdo, settings))
                {
                    Raise(zdo, settings);
                }
            }
            Hunt.Tick(zdo, !_boss.IsDead());
        }

        /// <summary>Awake, alerted and with a player in reach; fills the player list the wave is raised on.</summary>
        private bool InFight(float range) =>
            _ai.IsAlerted() && !_ai.IsSleeping()
            && StormTargets.InRange(_boss.transform.position, range, _players).Count > 0;

        private bool Due(bool inFight, ZDO zdo, TornadoSettings settings)
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

        // A fight begins, or this machine has just taken the boss: a full delay, unless a wave rose less than its own
        // drawn delay ago - then the rhythm carries on from it.
        private void Prime(ZDO zdo, TornadoSettings settings)
        {
            _primed = true;
            _fight = 0f;
            _due = settings.NextDelay();
            TornadoWave? last = TornadoStore.Read(zdo);
            float since = last != null ? NetTime.SecondsSince(last.At) : float.MaxValue;
            if (last != null && since < last.Next)
            {
                _due = Mathf.Max(0f, last.Next - since);
            }
        }

        /// <summary>One wave: a tornado near each player in reach not already hunted, formed `form time` from now.</summary>
        private void Raise(ZDO zdo, TornadoSettings settings)
        {
            _fight = 0f;
            _due = settings.NextDelay();
            TornadoWave wave = TornadoTargets.Wave(_players, settings, _due);
            if (wave.Count == 0)
            {
                return; // everyone in reach already has a tornado on them (another Nightfall boss's): this wave is skipped
            }
            wave.At = NetTime.NowMs();
            TornadoStore.Write(zdo, wave);
            Hunt.Start(wave);
            if (Log.Diagnostics)
            {
                Log.Diag($"{_boss.name}: Nightfall raises {wave.Count} tornado(es)");
            }
        }
    }
}
