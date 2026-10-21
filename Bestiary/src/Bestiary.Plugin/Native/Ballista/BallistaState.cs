// Portions Copyright (C) 2026 Gerald Glasgow. EliteCreaturesPack 0.9.0 at ValheimMods 3bb778330f23645ccddb42fbc0c6ffab72c842b6
// (Ballista/BallistaState.cs, Ballista/BallistaTimeline.cs).
// Changed by expDARE, 2026: Bestiary namespace; the timeline keeps only the reload's moments (no hands or string to draw
// on the game's turret model).
// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using UnityEngine;

namespace ExtraPotions.Bestiary.Runtime.Native.Ballista
{
    /// <summary>The string and what lies in the groove: slack, drawn and caught by the latch, or drawn with a missile laid.</summary>
    public enum Spring
    {
        Slack = 0,
        Drawn = 1,
        Loaded = 2,
    }

    /// <summary>What the holder is doing at the ballista: nothing, pulling the string back, or laying a missile.</summary>
    public enum Act
    {
        None = 0,
        Pulling = 1,
        Loading = 2,
    }

    /// <summary>
    /// The keys a bone ballista keeps in its ZDO. All are written by its owner, which is the holder while someone holds it
    /// (<see cref="BallistaControl"/> hands the ZDO over on a granted request), and read by every peer that draws it.
    /// Times are the network clock's ticks, so every peer reads the same moment.
    /// </summary>
    public static class BallistaKeys
    {
        public static readonly int User = "ecp_bal_user".GetStableHashCode();
        public static readonly int Yaw = "ecp_bal_yaw".GetStableHashCode();
        public static readonly int Pitch = "ecp_bal_pitch".GetStableHashCode();
        public static readonly int Spring = "ecp_bal_spring".GetStableHashCode();
        public static readonly int Act = "ecp_bal_act".GetStableHashCode();
        public static readonly int ActAt = "ecp_bal_act_at".GetStableHashCode();
        public static readonly int Shots = "ecp_bal_shots".GetStableHashCode();
        public static readonly int ShotAt = "ecp_bal_shot_at".GetStableHashCode();

        /// <summary>The network clock now, in ticks (0 outside a world).</summary>
        public static long Now() => ZNet.instance != null ? ZNet.instance.GetTime().Ticks : 0L;

        /// <summary>Seconds from `ticks` to now; very long for a moment never set.</summary>
        public static float Since(long ticks) => ticks <= 0L ? 9999f : Mathf.Min(9999f, (Now() - ticks) / (float)TimeSpan.TicksPerSecond);
    }

    /// <summary>
    /// The reload's moments: pulling the string to the latch takes 1.4 s, laying a missile 1.2 s (it shows in the groove
    /// from 0.95 s), and a shot settles 0.5 s before the next pull starts.
    /// </summary>
    public static class BallistaTimeline
    {
        public const float PullEnd = 1.4f, Lay = 0.95f, LoadEnd = 1.2f, Settle = 0.5f;

        /// <summary>Whether a missile lies in the groove.</summary>
        public static bool MissileLaid(in BallistaState s) => s.Settled == Spring.Loaded || (s.Act == Act.Loading && s.ActTime >= Lay);

        /// <summary>Whether the string is drawn back (the game's turret model shows its armed body).</summary>
        public static bool Drawn(in BallistaState s) => s.Settled != Spring.Slack || s.Act == Act.Loading;
    }

    /// <summary>
    /// One read of a ballista's ZDO: who holds it, where it points (degrees from where it was placed: yaw to the right,
    /// pitch up), its string, the holder's action and how long it has run, the shots fired and how long since the last.
    /// An action that has run its time counts as done (<see cref="Settled"/>), so a holder letting go mid-action, or
    /// leaving the game, leaves a ballista every peer draws the same.
    /// </summary>
    public readonly struct BallistaState
    {
        public readonly long User;
        public readonly float Yaw, Pitch, ActTime, ShotAge;
        public readonly Spring Spring;
        public readonly Act Act;
        public readonly int Shots;

        private BallistaState(ZDO zdo)
        {
            User = zdo.GetLong(BallistaKeys.User, 0L);
            Yaw = zdo.GetFloat(BallistaKeys.Yaw, 0f);
            Pitch = zdo.GetFloat(BallistaKeys.Pitch, 0f);
            Spring = (Spring)zdo.GetInt(BallistaKeys.Spring, 0);
            Act = (Act)zdo.GetInt(BallistaKeys.Act, 0);
            ActTime = BallistaKeys.Since(zdo.GetLong(BallistaKeys.ActAt, 0L));
            Shots = zdo.GetInt(BallistaKeys.Shots, 0);
            ShotAge = BallistaKeys.Since(zdo.GetLong(BallistaKeys.ShotAt, 0L));
        }

        public static BallistaState Read(ZDO zdo) => new BallistaState(zdo);

        /// <summary>The string as it is once the current action has run its time.</summary>
        public Spring Settled => Act switch
        {
            Act.Pulling when ActTime >= BallistaTimeline.PullEnd => Spring.Drawn,
            Act.Loading when ActTime >= BallistaTimeline.LoadEnd => Spring.Loaded,
            _ => Spring,
        };

        /// <summary>Whether an action is still running.</summary>
        public bool Busy => (Act == Act.Pulling && ActTime < BallistaTimeline.PullEnd) || (Act == Act.Loading && ActTime < BallistaTimeline.LoadEnd);
    }
}
