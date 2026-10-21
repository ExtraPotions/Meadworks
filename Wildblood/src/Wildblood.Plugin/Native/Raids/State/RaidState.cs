// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// Changed by expDARE, 2026: Wildblood namespace and authority-owned raid integration.
namespace ExtraPotions.Wildblood.Adopted.Raids
{
    /// <summary>
    /// The raid as it stands, read and written straight in the host's ZDO (features/raids.md section 5: the raid lives in
    /// the chest's ZDO). Nothing is kept beside it, so a machine that takes the host over carries the raid on from
    /// exactly here, and every client draws the HUD line from the same values. Only the host's owner writes; anyone may
    /// read. Times are whole milliseconds of the server clock (<see cref="Util.NetTime"/>). The ZDO itself skips a write
    /// that changes nothing, so setting a value to what it already is costs no resend.
    /// </summary>
    public readonly struct RaidState
    {
        private static readonly int PhaseKey = RaidKeys.Phase.GetStableHashCode();
        private static readonly int WaveKey = RaidKeys.Wave.GetStableHashCode();
        private static readonly int HeatKey = RaidKeys.Heat.GetStableHashCode();
        private static readonly int BandKey = RaidKeys.Band.GetStableHashCode();
        private static readonly int TierKey = RaidKeys.Tier.GetStableHashCode();
        private static readonly int StakeKey = RaidKeys.Stake.GetStableHashCode();
        private static readonly int CoinsBackKey = RaidKeys.CoinsBack.GetStableHashCode();
        private static readonly int CoinsUnspentKey = RaidKeys.CoinsUnspent.GetStableHashCode();
        private static readonly int DropsKey = RaidKeys.Drops.GetStableHashCode();
        private static readonly int StartedKey = RaidKeys.StartedAt.GetStableHashCode();
        private static readonly int DeadlineKey = RaidKeys.Deadline.GetStableHashCode();
        private static readonly int PhaseUntilKey = RaidKeys.PhaseUntil.GetStableHashCode();
        private static readonly int EventKey = RaidKeys.Event.GetStableHashCode();
        private static readonly int NameKey = RaidKeys.Name.GetStableHashCode();
        private static readonly int AliveKey = RaidKeys.Alive.GetStableHashCode();
        private static readonly int LeftKey = RaidKeys.Left.GetStableHashCode();
        private static readonly int SeenKey = RaidKeys.SeenAt.GetStableHashCode();
        private static readonly int RunnerKey = RaidKeys.Runner.GetStableHashCode();
        private static readonly int AloneKey = RaidKeys.Alone.GetStableHashCode();
        private static readonly int ServerKey = RaidKeys.Server.GetStableHashCode();
        private static readonly int CooldownKey = RaidKeys.CooldownUntil.GetStableHashCode();
        private static readonly int EndedKey = RaidKeys.Ended.GetStableHashCode();
        private static readonly int EndedAtKey = RaidKeys.EndedAt.GetStableHashCode();

        public RaidState(ZDO zdo) => Zdo = zdo;

        /// <summary>The host's ZDO the values live in.</summary>
        public ZDO Zdo { get; }

        public RaidPhase Phase { get => (RaidPhase)Zdo.GetInt(PhaseKey); set => Zdo.Set(PhaseKey, (int)value); }

        /// <summary>True from the sounding until the end: countdown, a wave or a break.</summary>
        public bool Running => IsRunning(Phase);

        public int Wave { get => Zdo.GetInt(WaveKey); set => Zdo.Set(WaveKey, value); }

        public float Heat { get => Zdo.GetFloat(HeatKey); set => Zdo.Set(HeatKey, value); }

        /// <summary>The band's index; <see cref="RaidTable.Band(int)"/> turns it into the row.</summary>
        public int Band { get => Zdo.GetInt(BandKey); set => Zdo.Set(BandKey, value); }

        public int Tier { get => Zdo.GetInt(TierKey); set => Zdo.Set(TierKey, value); }

        public int Stake { get => Zdo.GetInt(StakeKey); set => Zdo.Set(StakeKey, value); }

        public int CoinsBack { get => Zdo.GetInt(CoinsBackKey); set => Zdo.Set(CoinsBackKey, value); }

        public int CoinsUnspent { get => Zdo.GetInt(CoinsUnspentKey); set => Zdo.Set(CoinsUnspentKey, value); }

        /// <summary>The raiders' loot multiplier now; 1 when none was written.</summary>
        public float Drops { get => Zdo.GetFloat(DropsKey, 1f); set => Zdo.Set(DropsKey, value); }

        /// <summary>When the raid was sounded; also its id in the raiders' tags.</summary>
        public long StartedAt { get => Zdo.GetLong(StartedKey); set => Zdo.Set(StartedKey, value); }

        public long Deadline { get => Zdo.GetLong(DeadlineKey); set => Zdo.Set(DeadlineKey, value); }

        public long PhaseUntil { get => Zdo.GetLong(PhaseUntilKey); set => Zdo.Set(PhaseUntilKey, value); }

        public string EventName { get => Zdo.GetString(EventKey); set => Zdo.Set(EventKey, value); }

        public string DisplayName { get => Zdo.GetString(NameKey); set => Zdo.Set(NameKey, value); }

        public int Alive { get => Zdo.GetInt(AliveKey); set => Zdo.Set(AliveKey, value); }

        public int Left { get => Zdo.GetInt(LeftKey); set => Zdo.Set(LeftKey, value); }

        public long SeenAt { get => Zdo.GetLong(SeenKey); set => Zdo.Set(SeenKey, value); }

        public long Runner { get => Zdo.GetLong(RunnerKey); set => Zdo.Set(RunnerKey, value); }

        public bool Alone { get => Zdo.GetBool(AloneKey); set => Zdo.Set(AloneKey, value); }

        public long Server { get => Zdo.GetLong(ServerKey); set => Zdo.Set(ServerKey, value); }

        public long CooldownUntil { get => Zdo.GetLong(CooldownKey); set => Zdo.Set(CooldownKey, value); }

        public RaidEnd Ended { get => (RaidEnd)Zdo.GetInt(EndedKey); set => Zdo.Set(EndedKey, (int)value); }

        public long EndedAt { get => Zdo.GetLong(EndedAtKey); set => Zdo.Set(EndedAtKey, value); }

        /// <summary>True for a phase in which a raid runs.</summary>
        public static bool IsRunning(RaidPhase phase) =>
            phase == RaidPhase.Countdown || phase == RaidPhase.Wave || phase == RaidPhase.Break;

        /// <summary>True when the ZDO carries a running raid; a cheap test for the HUD and the spacing check.</summary>
        public static bool IsRunning(ZDO? zdo) => zdo != null && IsRunning((RaidPhase)zdo.GetInt(PhaseKey));

        /// <summary>
        /// The owner, as the raid is sounded: the countdown starts, the heat, band and coins are fixed and the clocks set.
        /// Everything a previous raid of this host left is overwritten.
        /// </summary>
        public void Begin(RaidHeat heat, RaidChoice choice, int stake, long now)
        {
            BeginPurse(heat, stake);
            EventName = choice.EventName;
            DisplayName = choice.DisplayName;
            Wave = 0;
            Alive = 0;
            Left = 0;
            Ended = RaidEnd.None;
            EndedAt = 0L;
            StartedAt = now;
            Deadline = now + Ms(RaidTable.TimeLimitSeconds);
            PhaseUntil = now + Ms(RaidTable.CountdownSeconds);
            SeenAt = now;
            CooldownUntil = now + Ms((float)(Util.NetTime.DaySeconds() * RaidTable.CooldownDays));
            Phase = RaidPhase.Countdown;
        }

        private void BeginPurse(RaidHeat heat, int stake)
        {
            Heat = heat.Heat;
            Band = heat.Band.Index;
            Tier = heat.Tier;
            Stake = stake;
            CoinsBack = heat.Band.CoinsFor(stake);
            CoinsUnspent = CoinsBack;
            Drops = heat.Drops;
        }

        /// <summary>Seconds as whole milliseconds of the server clock.</summary>
        public static long Ms(float seconds) => (long)(seconds * 1000f);
    }
}
