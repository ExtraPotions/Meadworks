// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// Changed by expDARE, 2026: Wildblood namespace and authority-owned raid integration.
using ExtraPotions.Wildblood.Adopted.Util;
using UnityEngine;

namespace ExtraPotions.Wildblood.Adopted.Raids
{
    /// <summary>
    /// The waves (features/raids.md sections 4.3 and 4.4), run by <see cref="RaidPhases"/> on the host's owner. As a wave
    /// begins it is planned from the raid's event for the players at the raid now (<see cref="WaveSizes"/>), its regular
    /// raiders' coins re-split over what is left to come, and it is given its one spot on the ring
    /// (<see cref="WaveRing"/>). Each tick then counts the raiders alive (<see cref="RaidRoster"/>) and, while fewer than
    /// <see cref="RaidTable.MaxAlive"/> are, sends the next few the plan holds - a few a tick, so a wave arrives over a
    /// second or two instead of in one heavy frame - each made (<see cref="RaiderSpawn"/>), handed its coins
    /// (<see cref="RaidPurse"/>) and tagged (<see cref="RaiderTag"/>) in the frame it is made. The last wave's Warlord
    /// comes first, leading it in. Every bit of the wave is in the host's ZDO, so a machine that takes the host over
    /// sends the rest of the wave from the same spot.
    /// </summary>
    public static class RaidWaves
    {
        /// <summary>The waves' own key on the host's ZDO: the current wave's spot on the ground, where its raiders arrive.</summary>
        public const string SpotKey = "ecr_raid_wave_spot";

        /// <summary>Raiders made in one tick at most: a full wave of 20 arrives in under two seconds.</summary>
        private const int MostPerTick = 3;

        private static readonly int Spot = SpotKey.GetStableHashCode();
        private static readonly int Direction = RaidKeys.WaveDirection.GetStableHashCode();

        /// <summary>Wave <paramref name="wave"/> (1 to <see cref="RaidTable.Waves"/>) begins now: plan it and place it.</summary>
        public static void Begin(RaidRunner runner, int wave)
        {
            ZDO? host = runner.Zdo;
            if (host == null || !runner.IsOwner)
            {
                return;
            }
            RaidState state = runner.State;
            RandomEvent? ev = RaidCreatures.Event(state.EventName);
            WavePlan plan = ev != null ? WaveSizes.Plan(ev, wave, runner.Band, Mathf.Max(1, runner.PlayersNear)) : new WavePlan();
            if (wave <= 1)
            {
                RaidRoster.Clear(host); // nobody left over from the host's last raid
            }
            else
            {
                plan.NextRole = WavePlan.Read(host).NextRole; // chest raider and plunderer take turns across the waves
            }
            plan.Write(host);
            float direction = Place(runner, host, wave);
            Log.Info($"raid at {runner.Position:F0} ({state.DisplayName}): wave {wave} of {RaidTable.Waves}, {plan.ToCome} "
                + $"raiders from {direction:F0} degrees" + (ev == null ? $" - the game has no event '{state.EventName}'" : ""));
        }

        /// <summary>Every tick of a wave: the raiders of this raid alive now, and this wave's still to come.</summary>
        public static WaveCount Tick(RaidRunner runner)
        {
            ZDO? host = runner.Zdo;
            if (host == null)
            {
                return new WaveCount(0, 0);
            }
            int alive = RaidRoster.Count(host, runner.State.StartedAt, Time.time);
            WavePlan plan = WavePlan.Read(host);
            int room = Mathf.Min(RaidTable.MaxAlive - alive, MostPerTick);
            if (plan.ToCome > 0 && room > 0)
            {
                alive += Send(runner, host, plan, room);
            }
            return new WaveCount(alive, plan.ToCome);
        }

        // The wave's one spot, on a different side from the last wave's; its direction in degrees.
        private static float Place(RaidRunner runner, ZDO host, int wave)
        {
            float? last = wave > 1 ? host.GetFloat(Direction) : (float?)null;
            Vector3 spot = WaveRing.Pick(runner.Position, last, out float direction);
            host.Set(Spot, spot);
            host.Set(Direction, direction);
            return direction;
        }

        // Up to `room` raiders from the plan. One that cannot be made still leaves the plan, so the wave never waits on it.
        private static int Send(RaidRunner runner, ZDO host, WavePlan plan, int room)
        {
            RandomEvent? ev = RaidCreatures.Event(runner.State.EventName);
            if (ev == null)
            {
                plan.Clear(); // its event is gone from the game: nothing more can come
            }
            Vector3 spot = host.GetVec3(Spot, runner.Position);
            int sent = 0;
            for (int i = 0; ev != null && i < room && plan.ToCome > 0; i++)
            {
                sent += SendOne(runner, host, ev, plan, spot) ? 1 : 0;
            }
            plan.Write(host);
            if (sent > 0)
            {
                RaidRoster.Save(host);
            }
            return sent;
        }

        private static bool SendOne(RaidRunner runner, ZDO host, RandomEvent ev, WavePlan plan, Vector3 spot)
        {
            bool warlord = plan.WarlordDue;
            SpawnSystem.SpawnData? data = RaidCreatures.At(ev, warlord ? plan.TakeWarlord() : plan.TakeRegular());
            ZDO? raider = data != null && data.m_prefab != null ? RaiderSpawn.Arrive(runner, data, spot, warlord) : null;
            if (raider == null)
            {
                return false;
            }
            RaiderRole role = warlord ? RaiderRole.ChestRaider : plan.TakeRole(); // the Warlord comes for the gold
            RaiderTag.Write(raider, runner, role, warlord, Coins(runner.State, plan, warlord));
            RaidRoster.Add(host, raider.m_uid);
            return true;
        }

        // The Warlord leads the last wave in, so it is the raid's last raider only when that wave brings nobody else.
        private static int Coins(RaidState state, WavePlan plan, bool warlord)
        {
            bool lastWave = state.Wave >= RaidTable.Waves;
            if (warlord)
            {
                return RaidPurse.ForWarlord(state, lastWave && plan.Regulars == 0);
            }
            return RaidPurse.ForRaider(state, plan.Regulars + 1 + plan.Later, !lastWave || plan.WarlordDue);
        }
    }
}
