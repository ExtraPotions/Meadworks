// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.21.0, GPL-3.0-or-later
// 9da28eb58adbe065be1e4baef779a448acd38e0f
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using ExtraPotions.Wildblood.Adopted.Runtime;
using ExtraPotions.Wildblood.AdoptedLibs.PatchGuard;
using UnityEngine;

namespace ExtraPotions.Wildblood.Adopted.Aspects
{
    /// <summary>
    /// Echoing (the user's design, 2026-10-06): the fight starts as usual; `delay` seconds later (15) a white, nearly clear
    /// ghost of the boss rises where the boss stood then, and from then on does everything the boss did `delay` seconds
    /// before - walks its path, turns, swings, throws, staggers - until the boss dies, and then it is gone. Its blows hurt
    /// as the boss's did. Attached on every machine, it acts only on the boss's owner: every physics step it records the
    /// boss (<see cref="EchoTapes.Record"/>; attacks and animation cues reach the tape through the game's own calls), and
    /// once the tape reaches `delay` seconds back it finds its echo - or makes it (<see cref="EchoSpawner"/>), or takes it
    /// over from a machine that owned the boss before - and drives it (<see cref="EchoDriver"/>). Losing the boss stops
    /// the tape: a new owner records afresh, and the echo stands still meanwhile, or goes with its old owner.
    /// </summary>
    public sealed class EchoBehaviour : MonoBehaviour
    {
        /// <summary>Seconds between attempts to make an echo that could not be made.</summary>
        private const float SpawnRetry = 2f;

        private Character _boss = null!;
        private EliteController _controller = null!;
        private ZSyncAnimation _anim = null!;
        private EchoTape? _tape;
        private Character? _echo;
        private float _nextSpawn;
        private System.Action? _step;

        private void Start() => Guard.Run("EchoBehaviour.Start", Setup);

        private void Setup()
        {
            _boss = GetComponent<Character>();
            _controller = GetComponent<EliteController>();
            _anim = GetComponent<ZSyncAnimation>();
            if (_boss == null || _controller == null || _anim == null)
            {
                enabled = false;
            }
        }

        private void FixedUpdate() => Guard.Run("EchoBehaviour.Step", _step ??= Step);

        private void OnDestroy() => Stop();

        private void Step()
        {
            if (_boss == null || _boss.IsDead() || !_controller.IsOwner())
            {
                Stop();
                return;
            }
            float delay = EchoTape.Delay();
            EchoTape tape = Tape(delay);
            EchoTapes.Record(_boss, _anim, tape);
            float replay = Time.time - delay;
            if (tape.Covers(replay))
            {
                Play(tape, replay);
            }
        }

        /// <summary>The tape, made on the first step owned, and made anew when a longer delay needs more room.</summary>
        private EchoTape Tape(float delay)
        {
            int needed = EchoTape.CapacityFor(delay);
            if (_tape == null || _tape.Capacity < needed)
            {
                Stop();
                _tape = new EchoTape(needed, EchoParams.Count(_anim));
                EchoTapes.Join(_boss, _anim, _tape);
            }
            return _tape;
        }

        private void Play(EchoTape tape, float replay)
        {
            Character? echo = Echo();
            if (echo == null)
            {
                Summon(tape, replay);
                return;
            }
            ZNetView view = echo.GetComponent<ZNetView>();
            if (view == null || !view.IsValid())
            {
                return;
            }
            if (!view.IsOwner())
            {
                view.ClaimOwnership(); // the echo of a boss this machine took over: driven from the next step
                return;
            }
            EchoDriver.Step(echo, tape, replay);
        }

        private Character? Echo()
        {
            if (_echo == null || _echo.IsDead())
            {
                _echo = EchoLink.Find(_controller.View.GetZDO().m_uid);
            }
            return _echo;
        }

        private void Summon(EchoTape tape, float replay)
        {
            if (Time.time < _nextSpawn || !tape.TryRead(replay, out EchoPose pose, out _))
            {
                return;
            }
            _nextSpawn = Time.time + SpawnRetry;
            _echo = EchoSpawner.Make(_controller, pose);
        }

        private void Stop()
        {
            if (_tape != null)
            {
                EchoTapes.Leave(_boss, _anim);
                _tape = null;
            }
        }
    }
}
