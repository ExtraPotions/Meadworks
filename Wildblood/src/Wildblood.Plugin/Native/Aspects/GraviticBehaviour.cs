// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using ExtraPotions.Wildblood.Adopted.Runtime;
using ExtraPotions.Wildblood.AdoptedLibs.PatchGuard;
using UnityEngine;

namespace ExtraPotions.Wildblood.Adopted.Aspects
{
    /// <summary>
    /// Gravitic: every `every` seconds of fighting the boss roars, pulls every player within `range` toward it for
    /// `pull time` seconds at `pull speed`, then slams, costing each player within `slam radius` of its body `slam
    /// damage` percent of their maximum health. The owner keeps only the rhythm (<see cref="GraviticClock"/>) and sends
    /// one message, the roar, through the boss's own network view, so it reaches exactly the machines that hold the boss.
    /// Each of those runs the whole cycle for itself from that message: it plays the roar (<see cref="GraviticLook"/>),
    /// pulls its own player if they were in reach (<see cref="GraviticPull"/>), and when the pull time is up draws the
    /// slam and judges its own player (<see cref="GraviticSlam"/>) - a player's body lives on that player's client, so
    /// that is where it is moved and struck. The boss dying ends a cycle on the spot, with no slam. A hand-over needs
    /// nothing: the roar already sent plays out where it landed, and the new owner reads the last roar's time from the
    /// ZDO. A machine that meets the boss mid-cycle never heard that roar, so its player sits that one out.
    /// </summary>
    public sealed class GraviticBehaviour : MonoBehaviour
    {
        /// <summary>The roar, to every machine holding the boss; it carries the cycle's numbers.</summary>
        public const string Rpc = "ecr_gravitic";

        private Character _character = null!;
        private EliteController _controller = null!;
        private BaseAI? _ai;
        private readonly GraviticClock _clock = new GraviticClock();
        private GraviticCall _call;
        private float _slamAt;
        private bool _armed;

        /// <summary>True on this machine between a roar and its slam, while the boss lives.</summary>
        public bool Pulling => isActiveAndEnabled && _controller != null && _controller.Ready && _armed && !Fallen();

        private void Start() => Guard.Run("GraviticBehaviour.Start", Setup);

        private void Setup()
        {
            _character = GetComponent<Character>();
            _controller = GetComponent<EliteController>();
            if (_character == null || _controller == null || _controller.View == null || !_controller.View.IsValid())
            {
                enabled = false;
                return;
            }
            _ai = _character.GetBaseAI();
            if (!NativeRpcOwnership.View(_controller.View, NativeRpcOwner.CreatureGravitic, () => _controller.View.Register<ZPackage>(Rpc, OnRoar), Rpc))
            {
                enabled = false;
                return;
            }
        }

        private void OnDisable()
        {
            _armed = false;
            _clock.Reset();
            GraviticPull.Release(this);
        }

        private void OnDestroy() => OnDisable();

        private void Update() => Guard.Run("GraviticBehaviour.Update", static self => self.Step(), this);

        private void Step()
        {
            if (!NativeRpcOwnership.OwnsView(_controller.View, NativeRpcOwner.CreatureGravitic))
            {
                _armed = false;
                GraviticPull.Release(this);
                _clock.Reset();
                return;
            }
            if (_armed)
            {
                Follow(); // every machine: the cycle it heard, up to its slam
            }
            if (Fallen() || !_controller.IsOwner())
            {
                _clock.Reset(); // owner-only from here: the rhythm belongs to whoever owns the boss
                return;
            }
            GraviticCall call = GraviticCall.FromRules();
            float every = GraviticCall.Every(call);
            if (_clock.Tick(Fighting(call.Range), every, _controller.View.GetZDO(), Time.deltaTime))
            {
                _clock.Spent(_controller.View.GetZDO(), every);
                ZPackage pkg = new ZPackage();
                call.Write(pkg);
                _controller.View.InvokeRPC(ZNetView.Everybody, Rpc, pkg); // delivered here at once, and to every peer
            }
        }

        /// <summary>Alert, with a player within reach of the pull: the boss is in a fight worth roaring at.</summary>
        private bool Fighting(float range) => _ai != null && _ai.IsAlerted() && Player.IsPlayerInRange(Anchor(), range);

        private void OnRoar(long sender, ZPackage pkg) =>
            Guard.Run("GraviticBehaviour.Roar", () => Begin(GraviticCall.Read(pkg)));

        // A roar heard here. One cycle at a time: a second roar while one plays out (a hand-over's echo) is ignored.
        private void Begin(GraviticCall call)
        {
            if (!isActiveAndEnabled || _controller == null || !_controller.Ready || Fallen() || _armed)
            {
                return;
            }
            _call = call;
            _armed = true;
            _slamAt = Time.time + call.PullTime;
            GraviticLook.Roar(_character, Anchor(), BodyRadius(), call.Range);
            GraviticPull.Catch(this, call);
        }

        // The cycle on this machine: cut short by the boss's death, else the slam once the pull time is up.
        private void Follow()
        {
            if (!Fallen() && Time.time < _slamAt)
            {
                return;
            }
            _armed = false;
            GraviticPull.Release(this);
            if (Fallen())
            {
                return;
            }
            Vector3 anchor = Anchor();
            float reach = BodyRadius() + _call.SlamRadius;
            GraviticLook.Slam(anchor, reach);
            GraviticSlam.Strike(anchor, reach, _call.SlamDamage);
        }

        /// <summary>
        /// Dead or dying. Only the boss's owner marks it dead, and a boss with a death animation plays it out first, so on
        /// every other machine the replicated health reaching zero is the first sign - and the one that stops a cycle there.
        /// </summary>
        private bool Fallen() => _character == null || _character.IsDead() || _character.GetHealth() <= 0f;

        /// <summary>
        /// The point the pull draws toward and the slam lands on: the boss's feet, or for a boss on the wing the ground
        /// straight beneath it, so players are drawn under a flier rather than up into the air.
        /// </summary>
        public Vector3 Anchor()
        {
            Vector3 at = transform.position;
            if (_character != null && _character.IsFlying() && ZoneSystem.instance != null)
            {
                at.y = ZoneSystem.instance.GetSolidHeight(at, out float ground, 0) ? ground
                    : ZoneSystem.instance.GetGroundHeight(at);
            }
            return at;
        }

        /// <summary>The boss's body radius as the game measures it, growth included.</summary>
        public float BodyRadius() => _character != null ? _character.GetRadius() : 0f;
    }
}
