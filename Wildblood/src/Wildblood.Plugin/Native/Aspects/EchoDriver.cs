// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.21.0, GPL-3.0-or-later
// 9da28eb58adbe065be1e4baef779a448acd38e0f
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using UnityEngine;

namespace ExtraPotions.Wildblood.Adopted.Aspects
{
    /// <summary>
    /// One physics step of an echo, on its owner (the boss's owner): the boss as it was `delay` seconds ago, read off the
    /// tape (<see cref="EchoTape"/>), put onto the echo - its place (a velocity that lands it there this step, so the game
    /// sends a moving body that other machines follow smoothly; a jump of several metres, a boss that teleported, is a
    /// jump), its facing, where it looks, its animator (walking, turning, flying) and where it aims - and then everything
    /// the boss did by that moment, done again (<see cref="EchoReplay"/>). The game's own movement step is skipped for an
    /// echo (<see cref="Patches.MotionPatch"/>), so nothing else moves it.
    /// </summary>
    internal static class EchoDriver
    {
        /// <summary>A step longer than this is a jump, not a walk.</summary>
        private const float JumpDistance = 8f;

        public static void Step(Character echo, EchoTape tape, float replay)
        {
            if (!tape.TryRead(replay, out EchoPose pose, out float[] values))
            {
                return;
            }
            Move(echo, pose);
            Look(echo, pose.Look);
            if (echo.m_zanim != null)
            {
                EchoParams.Write(echo.m_zanim, values);
            }
            EchoLink.Aim(echo, pose);
            while (tape.TryTake(replay, out EchoEvent echoEvent))
            {
                EchoReplay.Play(echo, echoEvent);
            }
        }

        private static void Move(Character echo, EchoPose pose)
        {
            Rigidbody body = echo.m_body;
            if (body == null)
            {
                return;
            }
            body.useGravity = false;
            body.angularVelocity = Vector3.zero;
            body.MoveRotation(pose.Rotation);
            Vector3 to = pose.Position - body.position;
            if (to.sqrMagnitude > JumpDistance * JumpDistance)
            {
                body.position = pose.Position;
                echo.transform.position = pose.Position;
                body.linearVelocity = Vector3.zero;
                return;
            }
            body.linearVelocity = to / Mathf.Max(Time.fixedDeltaTime, 0.005f);
        }

        private static void Look(Character echo, Vector3 look)
        {
            if (look.sqrMagnitude < 0.0001f)
            {
                return;
            }
            echo.SetLookDir(look);
            echo.UpdateEyeRotation();
        }
    }
}
