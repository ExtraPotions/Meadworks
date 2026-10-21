// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using System.Collections.Generic;
using ExtraPotions.Wildblood.Adopted.Rules;
using ExtraPotions.Wildblood.Adopted.Runtime;
using ExtraPotions.Wildblood.Adopted.Scaling;
using ExtraPotions.Wildblood.Adopted.Tally;
using ExtraPotions.Wildblood.Adopted.Traits;
using UnityEngine;

namespace ExtraPotions.Wildblood.Adopted.Aspects
{
    /// <summary>
    /// Where Fixated's mark goes, decided on the boss's owner and written to its ZDO (<see cref="FixatedMark"/>). Only
    /// players in the fight count: alive and within <see cref="Leash"/> metres of the boss. Alone in the fight, that
    /// player is always marked. Otherwise the first mark goes to whoever first hurts the boss or is first targeted by it;
    /// every `every` seconds it moves to whoever hurt the boss most in those seconds alone, so players can take turns
    /// (nobody hurt it: it stays); and it moves at once when the marked player dies, logs out or leaves the fight - to
    /// whoever hurt it most so far this period, else its target, else the nearest. Each change starts a new period. The
    /// damage is read off the boss's damage-board tally, which the owner already keeps per player in the ZDO: a period's
    /// damage is the tally now less the tally at its start. A new owner starts a fresh period from the tally it finds.
    /// </summary>
    internal sealed class FixatedOwner
    {
        /// <summary>How far from the boss a player still counts as in the fight, in metres.</summary>
        public const float Leash = 60f;

        private const float CheckEvery = 0.5f;

        private readonly EliteController _boss;
        private readonly Character _character;
        private readonly BaseAI? _ai;
        private Dictionary<long, float>? _periodStart;
        private float _periodEnd;
        private float _nextCheck;

        public FixatedOwner(EliteController boss, Character character)
        {
            _boss = boss;
            _character = character;
            _ai = character.GetComponent<BaseAI>();
        }

        /// <summary>Every frame on every machine; acts only on the boss's owner, twice a second.</summary>
        public void Tick()
        {
            if (!_boss.IsOwner() || _character.IsDead())
            {
                _periodStart = null; // given away: a later hand-back starts a fresh period
                return;
            }
            if (_periodStart == null)
            {
                StartPeriod();
            }
            if (Time.time >= _nextCheck)
            {
                _nextCheck = Time.time + CheckEvery;
                Check();
            }
        }

        private void Check()
        {
            ZDO zdo = _boss.View.GetZDO();
            List<Player> players = InFight();
            ZDOID mark = FixatedMark.Get(zdo);
            Player? marked = players.Find(p => p.GetZDOID() == mark);
            Player? next = marked == null || players.Count == 1 ? Replacement(players, mark) : Rotation(players, marked);
            ZDOID nextId = next != null ? next.GetZDOID() : ZDOID.None;
            if (nextId != mark)
            {
                FixatedMark.Set(zdo, nextId);
                StartPeriod();
            }
        }

        // Alone, the one player. A lost mark moves at once; an open one waits for the first to hurt it or be targeted.
        private Player? Replacement(List<Player> players, ZDOID mark)
        {
            if (players.Count <= 1)
            {
                return players.Count == 1 ? players[0] : null;
            }
            Player? next = TopDamage(players) ?? Target(players);
            return next == null && mark != ZDOID.None ? Nearest(players) : next;
        }

        // At a period's end the mark goes to whoever hurt it most in it, the marked player included.
        private Player Rotation(List<Player> players, Player marked)
        {
            if (Time.time < _periodEnd)
            {
                return marked;
            }
            Player? top = TopDamage(players);
            StartPeriod();
            return top ?? marked;
        }

        private List<Player> InFight()
        {
            List<Player> players = new List<Player>();
            Vector3 at = _character.transform.position;
            foreach (Player player in Player.GetAllPlayers())
            {
                if (player != null && !player.IsDead() && Vector3.Distance(player.transform.position, at) <= Leash)
                {
                    players.Add(player);
                }
            }
            return players;
        }

        private Player? TopDamage(List<Player> players)
        {
            Dictionary<long, float> now = Totals();
            Player? top = null;
            float most = 0f;
            foreach (Player player in players)
            {
                long id = player.GetPlayerID();
                now.TryGetValue(id, out float total);
                float before = 0f;
                _periodStart?.TryGetValue(id, out before);
                if (total - before > most)
                {
                    most = total - before;
                    top = player;
                }
            }
            return top;
        }

        private Player? Target(List<Player> players)
        {
            Character? target = _ai != null ? _ai.GetTargetCreature() : null;
            return target != null ? players.Find(p => p == target) : null;
        }

        private Player Nearest(List<Player> players)
        {
            Vector3 at = _character.transform.position;
            Player nearest = players[0];
            foreach (Player player in players)
            {
                if (Vector3.Distance(player.transform.position, at) < Vector3.Distance(nearest.transform.position, at))
                {
                    nearest = player;
                }
            }
            return nearest;
        }

        private void StartPeriod()
        {
            _periodStart = Totals();
            _periodEnd = Time.time + Mathf.Max(1f, AspectMath.Power(Aspect.Fixated, Fields.Every));
        }

        private Dictionary<long, float> Totals()
        {
            Dictionary<long, float> totals = new Dictionary<long, float>();
            foreach (DamageTally.Entry entry in DamageTally.Load(_boss.View.GetZDO()))
            {
                totals[entry.Id] = entry.Damage;
            }
            return totals;
        }
    }
}
