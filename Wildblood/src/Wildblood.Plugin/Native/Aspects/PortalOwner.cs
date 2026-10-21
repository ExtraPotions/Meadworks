// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using ExtraPotions.Wildblood.Adopted.Runtime;
using ExtraPotions.Wildblood.Adopted.Util;
using UnityEngine;

namespace ExtraPotions.Wildblood.Adopted.Aspects
{
    /// <summary>
    /// The Portalbound decision, on whichever machine owns the boss - the only machine that runs its attacks. When the
    /// boss starts a portal-carried attack (<see cref="PortalAttacks"/>) at a creature, it looks for a spot for the far
    /// portal around that target (<see cref="PortalSpots"/>) and writes it into the boss's ZDO with the moment it opened
    /// (<see cref="PortalStore"/>); with no spot, the attack is thrown as the game made it. When the game lets the
    /// projectiles go (the Elder's vines, Bonemass's slime), each one leaves from the far portal's face for the target's
    /// middle, where the target is at that moment, instead of from the boss: straight at it, or along the arc that lands
    /// on it for one that falls (<see cref="PortalAim"/>); the game still adds its own spread. The moment of the first one
    /// is written too, which opens the portal at the hand everywhere. When the attack ends both portals close a little
    /// later; cut short before the throw (a stagger, a death), they close at once. Every write carries the latest closing
    /// time, so the portals close on time even if this machine stops owning the boss mid-attack. Nothing is drawn here.
    /// </summary>
    internal sealed class PortalOwner
    {
        /// <summary>The longest a wind-up may hold the far portal open before the throw comes; past it, it closes.</summary>
        private const float MaxWindup = 5f;

        /// <summary>Seconds both portals stay open after the last projectile, so the throw is seen to end.</summary>
        private const float Linger = 1.2f;

        /// <summary>Spare seconds on the closing time written at the release, for a throw that runs a little long.</summary>
        private const float Slack = 0.5f;

        /// <summary>How far in front of the far portal's face the projectiles appear.</summary>
        private const float Mouth = 0.6f;

        /// <summary>The longest stream of projectiles the closing time allows for, whatever the attack's own numbers say.</summary>
        private const float MaxStream = 10f;

        private readonly EliteController _controller;
        private readonly Humanoid _boss;
        private readonly string _prefab;
        private Attack? _attack;
        private Character? _target;
        private Vector3 _spot;
        private Vector3 _aim;
        private float _speed;
        private float _gravity;
        private bool _released;

        public PortalOwner(EliteController controller, Humanoid boss, string prefab)
        {
            _controller = controller;
            _boss = boss;
            _prefab = prefab;
        }

        /// <summary>From the attack-start patch, on the owner: a portal-carried attack winds up and its far portal opens.</summary>
        public void Started(Attack attack)
        {
            if (!_controller.IsOwner() || !PortalAttacks.Carries(_boss, _prefab, attack))
            {
                return;
            }
            if (_attack != null)
            {
                Finish(); // the previous throw is over: its portals close before the next ones open
            }
            Character? target = Target();
            if (target == null || !PortalSpots.TryFind(target.GetCenterPoint(), PortalSettings.Read(), out Vector3 spot))
            {
                if (Log.Diagnostics)
                {
                    Log.Diag($"{_boss.name}: Portalbound found no clear spot; the throw comes from its hand");
                }
                return;
            }
            Open(attack, target, spot);
        }

        private Character? Target()
        {
            BaseAI? ai = _boss.GetBaseAI();
            Character? target = ai != null ? ai.GetTargetCreature() : null;
            return target != null && !target.IsDead() ? target : null;
        }

        private void Open(Attack attack, Character target, Vector3 spot)
        {
            _attack = attack;
            _target = target;
            _spot = spot;
            _aim = target.GetCenterPoint();
            _speed = PortalAim.SpeedOf(attack);
            _gravity = PortalAim.GravityOf(attack);
            _released = false;
            long now = NetTime.NowMs();
            PortalStore.Open(_controller.View.GetZDO(), now, spot, _aim, now + Ms(MaxWindup + Stream(attack) + Linger));
            if (Log.Diagnostics)
            {
                Log.Diag($"{_boss.name}: Portalbound opens a portal at {spot} for {target.name}");
            }
        }

        /// <summary>
        /// From the spawn-point patch, on the owner, as the game places each projectile of this attack: it leaves from the
        /// far portal's face for the target's middle. The first one marks the release.
        /// </summary>
        public void Redirect(Attack attack, ref Vector3 spawnPoint, ref Vector3 aimDir)
        {
            if (attack != _attack || !_controller.IsOwner())
            {
                return;
            }
            if (_target != null && !_target.IsDead())
            {
                _aim = _target.GetCenterPoint(); // a fallen or vanished target keeps its last place
            }
            Vector3 toward = _aim - _spot;
            if (toward.sqrMagnitude < 0.01f)
            {
                return; // the target stands in the portal itself: nothing sensible to aim at, the game's aim stands
            }
            spawnPoint = _spot + toward.normalized * Mouth;
            aimDir = PortalAim.Toward(spawnPoint, _aim, _speed, _gravity);
            if (!_released)
            {
                Release(attack);
            }
        }

        private void Release(Attack attack)
        {
            _released = true;
            long now = NetTime.NowMs();
            PortalStore.Fire(_controller.View.GetZDO(), now, _aim, now + Ms(Stream(attack) + Linger + Slack));
        }

        /// <summary>Each frame: the attack's end (or the boss's) closes the portals; a lost boss is let go.</summary>
        public void Tick()
        {
            if (_attack == null)
            {
                return;
            }
            if (!_controller.IsOwner())
            {
                Forget(); // the closing time already written closes the portals on every machine
                return;
            }
            if (_boss.IsDead() || _attack.IsDone() || _boss.m_currentAttack != _attack)
            {
                Finish();
            }
        }

        // Thrown: the portals stay a moment, then close. Cut short before the throw, or the boss dead: closed at once.
        private void Finish()
        {
            bool thrown = _released && !_boss.IsDead();
            if (_controller.View != null && _controller.View.IsValid())
            {
                PortalStore.Shut(_controller.View.GetZDO(), NetTime.NowMs() + (thrown ? Ms(Linger) : 0L));
            }
            Forget();
        }

        private void Forget()
        {
            _attack = null;
            _target = null;
            _released = false;
        }

        /// <summary>How long the game streams this attack's projectiles after the release.</summary>
        private static float Stream(Attack attack) =>
            Mathf.Clamp(Mathf.Max(0, attack.m_projectileBursts - 1) * attack.m_burstInterval, 0f, MaxStream);

        private static long Ms(float seconds) => (long)(seconds * 1000f);
    }
}
