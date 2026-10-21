// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// Changed by expDARE, 2026: Wildblood namespace and authority-owned raid integration.
using System;
using ExtraPotions.Wildblood.AdoptedLibs.PatchGuard;
using UnityEngine;

namespace ExtraPotions.Wildblood.Adopted.Raids
{
    /// <summary>
    /// A raider's mind beside the game's own (features/raids.md section 4, "What raiders go for", and section 5). Put on
    /// every tagged raider by <see cref="RaiderAttachPatch"/> on every machine that loads it, it acts only while this
    /// machine owns the raider, so ownership moving between clients costs nothing: the next owner's copy picks up from the
    /// tag in the ZDO. Every 1.5 seconds, never every frame, it reads its raid at the host (<see cref="RaidVerdicts"/>):
    /// while the raid runs and the game's own hunt has given it no creature to fight, it points the game's static target
    /// at its building target or at the piece in the way (<see cref="RaiderGoal"/>, <see cref="BreakIn"/>); a stopped
    /// raid kills it where it stands (<see cref="RaiderKill"/>); a lost or ended raid, a host that has gone, or one this
    /// machine has never been sent for half a minute, sends it off (<see cref="RaiderLeave"/>). A tamed raider is the
    /// players' now and is left alone.
    /// </summary>
    internal sealed class RaiderSteering : MonoBehaviour
    {
        /// <summary>Seconds between judgements of what to go for: slow enough to cost nothing, quick enough to look alive.</summary>
        private const float TickSeconds = 1.5f;

        /// <summary>Seconds between a walking-off raider's checks of whether it may vanish.</summary>
        private const float LeaveTickSeconds = 0.5f;

        /// <summary>Seconds a raider waits on a host this machine has never been sent before it decides its raid is over.</summary>
        private const float UnknownSeconds = 30f;

        private ZNetView _nview = null!;
        private float _next;
        private bool _inCharge;
        private bool _homed;
        private bool _hostSeen;
        private float _unknownSince = -1f;
        private bool _faultReported;

        public MonsterAI Ai { get; private set; } = null!;
        public Character Body { get; private set; } = null!;
        public ZNetView View => _nview;

        /// <summary>The raider's ZDO; null once it is gone.</summary>
        public ZDO? Zdo => _nview != null && _nview.IsValid() ? _nview.GetZDO() : null;

        /// <summary>The raid's host and the raid's id, from the tag: written once at the spawn, never changed.</summary>
        public ZDOID Host { get; private set; }

        public long Raid { get; private set; }

        internal RaiderGoal Goal { get; } = new RaiderGoal();
        internal BreakIn Way { get; } = new BreakIn();
        internal RaiderLeave Leave { get; } = new RaiderLeave();

        private void Awake() => Guard.Run("RaiderSteering.Awake", Setup);

        private void Setup()
        {
            _nview = GetComponent<ZNetView>();
            Ai = GetComponent<MonsterAI>();
            Body = GetComponent<Character>();
            ZDO? zdo = Zdo;
            if (zdo == null || Ai == null || Body == null)
            {
                enabled = false;
                return;
            }
            Host = RaiderTag.Host(zdo);
            Raid = RaiderTag.Raid(zdo);
            _next = Time.time + UnityEngine.Random.Range(0f, TickSeconds); // a wave's raiders spread over the interval
            RaiderRoster.Add(this);
        }

        private void OnDestroy() => RaiderRoster.Remove(this);

        private void Update()
        {
            float now = Time.time;
            if (now < _next)
            {
                return;
            }
            _next = now + (Leave.Active ? LeaveTickSeconds : TickSeconds);
            try
            {
                Tick(now);
            }
            catch (Exception e)
            {
                ReportOnce(e);
            }
        }

        private void Tick(float now)
        {
            if (!InCharge() || Body.IsDead())
            {
                return;
            }
            if (Body.IsTamed())
            {
                Leave.Stop(this); // tamed on its way out: it stays with its new friends
                return;
            }
            if (Leave.Active)
            {
                Leave.Check(this, now);
                return;
            }
            Judge(RaidVerdicts.Read(Host, Raid), now);
        }

        private void Judge(RaidVerdict verdict, float now)
        {
            if (verdict == RaidVerdict.Unknown)
            {
                Wait(now);
                return;
            }
            _hostSeen = true;
            _unknownSince = -1f;
            if (verdict == RaidVerdict.Runs)
            {
                Steer();
            }
            else if (verdict == RaidVerdict.Stopped)
            {
                RaiderKill.Kill(this);
            }
            else
            {
                SendOff();
            }
        }

        // A client keeps every ZDO it was sent until it is destroyed, so a host it saw and has lost is gone (the Raiders
        // Chest broken, a test marker removed after its raid). One it was never sent may only lie beyond the area the
        // server sends it, around a player the raider chased away: the raider fights on as any creature for a while.
        private void Wait(float now)
        {
            if (_hostSeen)
            {
                SendOff();
                return;
            }
            if (_unknownSince < 0f)
            {
                _unknownSince = now;
            }
            else if (now - _unknownSince >= UnknownSeconds)
            {
                SendOff();
            }
        }

        // Players first: while the game's own hunt has a creature for it to fight, it fights it. Only then the base.
        private void Steer()
        {
            if (!_homed)
            {
                Home();
            }
            if (PassiveWorld())
            {
                return; // the world's passive monsters leave pieces alone, raiders too
            }
            Character? prey = Ai.m_targetCreature;
            StaticTarget? aim = prey != null ? Cornered(prey) : AtBase();
            if (aim != null && Ai.m_targetStatic != aim)
            {
                Ai.m_targetStatic = aim;
            }
        }

        // The game's own break-in for a creature it cannot reach - its "no blow for 15 seconds and no path" - which this
        // raider's AI no longer makes for itself (TakeCharge), aimed at the piece between. The AI strikes a static target
        // before a creature and drops it as soon as it hunts again, so the player stays its quarry meanwhile.
        private StaticTarget? Cornered(Character prey) =>
            Ai.m_unableToAttackTargetTimer > 0f && !Ai.HavePath(prey.transform.position) ? Way.Toward(Ai, Body, prey) : null;

        // Nothing of the base loaded here: no aim, and it wanders toward the host, its spawn point now.
        private StaticTarget? AtBase()
        {
            StaticTarget? goal = Goal.Current(this);
            return goal != null ? Way.Aim(Ai, Body, goal) : null;
        }

        /// <summary>The raid's order (<see cref="RaiderOrders"/>), obeyed on the raider's owner only: a stop kills it where
        /// it stands, a lost raid sends it off.</summary>
        public void Obey(bool kill)
        {
            if (!InCharge() || Body.IsDead() || Body.IsTamed())
            {
                return;
            }
            if (kill)
            {
                RaiderKill.Kill(this);
            }
            else
            {
                SendOff();
            }
        }

        private void SendOff()
        {
            if (Leave.Active)
            {
                return;
            }
            ZDO? host = ZDOMan.instance != null ? ZDOMan.instance.GetZDO(Host) : null;
            Transform at = transform;
            Leave.Begin(this, host != null ? host.GetPosition() : at.position - at.forward * 5f);
            _next = Time.time + LeaveTickSeconds;
        }

        // An idle creature wanders around its spawn point; a raider's is the host, so with nothing of the base loaded on
        // this machine yet it drifts toward the base instead of back to where its wave came in. In memory, owner only.
        private void Home()
        {
            ZDO? host = ZDOMan.instance != null ? ZDOMan.instance.GetZDO(Host) : null;
            if (host != null)
            {
                Ai.m_spawnPoint = host.GetPosition();
                _homed = true;
            }
        }

        private bool InCharge()
        {
            bool owns = _nview != null && _nview.IsValid() && _nview.IsOwner();
            if (owns == _inCharge)
            {
                return owns;
            }
            _inCharge = owns;
            if (owns)
            {
                TakeCharge();
            }
            else
            {
                Leave.Stop(this); // the next owner reads the raid itself and sends it off from there
            }
            return owns;
        }

        // Ownership has just arrived. The game's own pick of a priority piece (a workbench, a portal) would overwrite the
        // building target every few seconds, so this owner steers the static target alone, the break-in toward a player
        // it cannot reach included (Cornered); players it sees or hears it still hunts as any creature does. The game's
        // hunt for the nearest player within 200 m would leave it never without a player and never at the base, and the
        // game walks an idle event creature off whenever no game event runs: a raider is neither. Its target is read back
        // from the tag, so a hand-over keeps it.
        private void TakeCharge()
        {
            Ai.m_attackPlayerObjects = false;
            Ai.SetHuntPlayer(false);
            if (Ai.IsEventCreature())
            {
                Ai.SetEventCreature(false);
            }
            _homed = false;
            Goal.Adopt(Zdo);
        }

        private static bool PassiveWorld() =>
            ZoneSystem.instance != null && ZoneSystem.instance.GetGlobalKey(GlobalKeys.PassiveMobs);

        private void ReportOnce(Exception e)
        {
            if (!_faultReported)
            {
                _faultReported = true;
                Guard.Report(e, $"raider at {transform.position:F0}");
            }
        }
    }
}
