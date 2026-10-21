// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using UnityEngine;

namespace ExtraPotions.Wildblood.Adopted.Aspects
{
    /// <summary>
    /// The pull on this machine's own player, the only character this machine may move: a player's body is simulated
    /// by that player's own client. A roar catches the local player when they are within `range` of the boss as it
    /// roars; from then until the slam the game's own movement carries them toward the boss. The pull is added where the
    /// game adds a knockback (<see cref="Patches.GraviticPullPatch"/>), to the velocity the game is about to apply, on
    /// foot and swimming alike, in the knockback's own shape: the speed toward the boss is raised to at least `pull
    /// speed`, sideways movement is left alone, and moving toward the boss faster than that is never slowed. Because the
    /// game applies it as its own intent, nothing in its movement undoes it, physics still stops the player at walls
    /// and rocks, and the game's own rule that the feet steer only on the ground makes a jump a brief escape. In water the
    /// game carries its swim velocity from step to step, so each step's push is taken back at the start of the next
    /// (<see cref="Repay"/>): a swimmer stops drifting the moment the pull ends.
    /// It is planar, so a flying boss draws players to the ground beneath it, and it stops a step short of the boss's
    /// body, however large the boss has grown. It pauses for a dodge roll, and it never takes a player the game is
    /// holding: seated, lying, at a ship's helm, riding, standing on a ship's deck, or mid-teleport.
    /// </summary>
    internal static class GraviticPull
    {
        /// <summary>Metres of clear ground the pull leaves between the player and the boss's body.</summary>
        private const float StopGap = 1f;

        private static GraviticBehaviour? _source;
        private static float _until;
        private static float _speed;
        private static Player? _debtor;
        private static Vector3 _debt;

        /// <summary>True while the local player is being pulled; the patch reads only this until then.</summary>
        public static bool Active { get; private set; }

        /// <summary>True while a swimmer still carries last step's push; the repay patch reads only this until then.</summary>
        public static bool Owes { get; private set; }

        /// <summary>A roar on this machine: catch the local player if they are within reach of it and free to move.</summary>
        public static void Catch(GraviticBehaviour source, GraviticCall call)
        {
            Player player = Player.m_localPlayer;
            if (player == null || call.PullSpeed <= 0f || !Free(player) || player.IsDead() || player.InGhostMode()
                || player.IsDebugFlying() || Vector3.Distance(source.Anchor(), player.transform.position) > call.Range)
            {
                return;
            }
            _source = source;
            _until = Time.time + call.PullTime;
            _speed = call.PullSpeed;
            Active = true;
        }

        /// <summary>The boss's cycle is over (slam, death or the boss gone): let go, if this boss is the one pulling.</summary>
        public static void Release(GraviticBehaviour source)
        {
            if (_source == source)
            {
                End();
            }
        }

        /// <summary>
        /// One physics step of the local player's own movement: raise their speed toward the boss to the pull speed,
        /// never past the stopping point in one step. Ends the pull when its time is up or the boss is gone or dead.
        /// </summary>
        public static void Steer(Player player, ref Vector3 velocity)
        {
            if (_source == null || !_source.Pulling || Time.time > _until || player.IsDead())
            {
                End();
                return;
            }
            if (!Free(player) || player.InDodge())
            {
                return; // held by something, or rolling: the roll carries them, the pull waits
            }
            float speed = Wanted(player, _source, out Vector3 toward);
            float along = toward.x * velocity.x + toward.z * velocity.z;
            if (along < speed)
            {
                Vector3 push = toward * (speed - along);
                velocity += push;
                Owe(player, push);
            }
        }

        /// <summary>
        /// The start of a character's movement step: if it is the swimmer the pull pushed last step, take that push back
        /// out of its stored swim velocity, so in water the pull is felt only while it runs and nothing drifts on after.
        /// </summary>
        public static void Repay(Character character)
        {
            if (_debtor != null && character != _debtor)
            {
                return;
            }
            if (_debtor != null)
            {
                _debtor.m_currentVel -= _debt;
            }
            _debtor = null; // paid, or the swimmer is gone: nothing is owed any more
            _debt = Vector3.zero;
            Owes = false;
        }

        // Swimming, the game hands the pull its stored swim velocity, which it carries into the next step (on foot it
        // hands a copy made fresh each step): remember the push, so the next step takes it back before moving on.
        private static void Owe(Player player, Vector3 push)
        {
            if (!player.IsSwimming())
            {
                return;
            }
            _debtor = player;
            _debt += push;
            Owes = true;
        }

        // The speed toward the boss this step: the pull speed, eased near the stopping point so one step never passes
        // it, and 0 once there - which still keeps them from walking back out until the pull ends.
        private static float Wanted(Player player, GraviticBehaviour source, out Vector3 toward)
        {
            Vector3 to = source.Anchor() - player.transform.position;
            to.y = 0f;
            toward = to.normalized;
            float gap = to.magnitude - (source.BodyRadius() + player.GetRadius() + StopGap);
            return gap > 0f ? Mathf.Min(_speed, gap / Time.fixedDeltaTime) : 0f;
        }

        private static void End()
        {
            _source = null;
            Active = false;
        }

        // Nothing the game is holding still: a seat, bed, helm or saddle (attached), a ship's deck, or a teleport.
        private static bool Free(Player player) =>
            !player.IsAttached() && player.GetStandingOnShip() == null && !player.IsTeleporting();
    }
}
