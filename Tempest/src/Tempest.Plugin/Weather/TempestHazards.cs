// Tempest - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: lingering strike ground lightning shocks (and with Strike Starts Fires, burns) a player standing in it.
using System;
using System.Collections.Generic;
using ExtraPotions.Meadworks.Crucible;
using ExtraPotions.Tempest.Config;
using ExtraPotions.Tempest.Integration;
using ExtraPotions.Tempest.Weather;
using UnityEngine;

namespace ExtraPotions.Tempest.Runtime
{
    /// <summary>
    /// The striking client writes the storm serial and the hazard's end on the strike's network object, so every peer that
    /// sees the strike knows it. Each client then checks only its own player, once a second, against the strikes near it
    /// (<see cref="StormHazards"/>) and adds the game's Lightning status effect plus lightning damage through the game's hit
    /// path. With Strike Starts Fires and no rain (EnvMan.IsWet, the check the game's own ignition uses) half of the damage is
    /// fire, so the game's damage path applies its own Burning
    /// effect after resistances; with Storm Hazard Damage 0 Burning is added directly as a status effect. When the storm ends
    /// or a setting turns off, the effects this class added are removed at once. During an Ash Storm's Active phase the local
    /// player burns every <see cref="StormHazards.AshSeconds"/> seconds while out in the open (no roof by the game's own
    /// <c>Cover.IsUnderRoof</c> check, not indoors), with the same setting and exemptions. Main thread only.
    /// </summary>
    internal static class TempestHazards
    {
        static readonly int StrikeHash = TempestStrikes.StrikePrefab.GetStableHashCode();
        static readonly List<ZDO> Nearby = new List<ZDO>();
        static float radius = -1;
        static double last = -1;
        static int shockedSerial;
        static int burnedSerial;
        static int ashBurnedSerial;
        static double ashLast = -1;
        static MeadworksLog? log;

        internal static void Initialize(MeadworksLog logger) => log = logger;

        /// <summary>Marks a strike spawned by this client; the game's own timeout (TimedDestruction) bounds the hazard.</summary>
        internal static void Mark(GameObject strike, int serial)
        {
            var view = strike.GetComponent<ZNetView>();
            if (view == null || !view.IsValid()) return;
            var timer = strike.GetComponent<TimedDestruction>();
            var zdo = view.GetZDO();
            zdo.Set(StormHazards.SerialKey, serial);
            zdo.Set(StormHazards.UntilKey, StormHazards.UntilMilliseconds(TempestWorld.Now, timer != null ? timer.m_timeout : 0));
        }

        internal static void Tick()
        {
            var player = Player.m_localPlayer;
            var settings = Plugin.Settings;
            if (player == null || settings == null || ZDOMan.instance == null || ZNetScene.instance == null) { Reset(); return; }
            var now = TempestWorld.Now;
            var current = TempestWorld.Current;
            var fx = TempestSettings.Effects;
            // Inside a vanilla random event no hazard applies, and the effects this class added end as for a setting turned off.
            var enabled = settings.Get(fx.HazardsAffectPlayers) && TempestModules.Active(TempestModule.Events) && !TempestWorld.LocalInVanillaEvent;
            if (StormHazards.AshBurnEnded(ashBurnedSerial, current, now, enabled))
            {
                // Like the strike hazard: the game's Burning cannot be told apart by source, so any Burning goes with the storm.
                player.GetSEMan().RemoveStatusEffect(SEMan.s_statusEffectBurning, quiet: true);
                ashBurnedSerial = 0;
            }
            Ash(player, settings, current, now, enabled);
            var stormOn = current != null && StormHazards.Live(current.Serial, double.MaxValue, current, now);
            var effects = StormHazards.Effects(settings.Get(fx.StrikeStartsFires), EnvMan.IsWet());
            if (shockedSerial != 0 && (!enabled || !stormOn || current!.Serial != shockedSerial))
            {
                // The shock this class added ends with the storm, not up to its own 3 s later.
                player.GetSEMan().RemoveStatusEffect(SEMan.s_statusEffectLightning, quiet: true);
                shockedSerial = 0;
            }
            if (burnedSerial != 0 && (!enabled || !stormOn || current!.Serial != burnedSerial || (effects & HazardEffects.Burning) == 0))
            {
                player.GetSEMan().RemoveStatusEffect(SEMan.s_statusEffectBurning, quiet: true);
                burnedSerial = 0;
            ashBurnedSerial = 0;
            }
            if (!stormOn || !StormHazards.Affects(enabled, player.IsDead(), player.InGodMode(), player.InGhostMode())) return;
            if (!StormHazards.Due(last, now)) return;
            var at = player.transform.position;
            if (!Standing(at, current!, now)) return;
            last = now;
            try
            {
                var seman = player.GetSEMan();
                seman.AddStatusEffect(SEMan.s_statusEffectLightning, resetTime: true);
                shockedSerial = current!.Serial;
                var burning = (effects & HazardEffects.Burning) != 0;
                if (burning) burnedSerial = current.Serial;
                var damage = StormHazards.Damage(effects, settings.Get(fx.HazardDamage));
                if (damage.Lightning > 0 || damage.Fire > 0)
                {
                    // Fire damage goes through the game's damage path, which adds Burning itself after resistances.
                    var hit = new HitData { m_point = player.GetCenterPoint(), m_dir = Vector3.up };
                    hit.m_damage.m_lightning = damage.Lightning;
                    hit.m_damage.m_fire = damage.Fire;
                    player.Damage(hit);
                }
                if (StormHazards.AddBurning(effects, seman.HaveStatusEffect(SEMan.s_statusEffectBurning), player.IsDead()))
                    seman.AddStatusEffect(SEMan.s_statusEffectBurning, resetTime: true);
            }
            catch (Exception error)
            {
                log?.Warning("Storm hazard failed: " + error.GetType().Name);
            }
        }

        /// <summary>The Ash Storm burn: Storm Hazard Damage as fire through the game's hit path (Burning follows), or Burning alone at 0.</summary>
        static void Ash(Player player, TempestConfig settings, Events.TempestEvent? current, double now, bool enabled)
        {
            var forced = EnvMan.instance != null && !string.IsNullOrEmpty(EnvMan.instance.m_forceEnv);
            var biome = TempestWorld.LocalBiome;
            if (!StormHazards.AshBurns(current, now, biome, forced, enabled, player.IsDead(), player.InGodMode(), player.InGhostMode(), player.InInterior(), false)) return;
            if (!StormHazards.AshDue(ashLast, now)) return;
            ashLast = now;
            if (Cover.IsUnderRoof(player.GetCenterPoint())) return;
            try
            {
                var damage = StormHazards.AshDamage(settings.Get(TempestSettings.Effects.HazardDamage));
                if (damage > 0)
                {
                    var hit = new HitData { m_point = player.GetCenterPoint(), m_dir = Vector3.up };
                    hit.m_damage.m_fire = damage;
                    player.Damage(hit);
                }
                // The game drops a fire hit too small after armor without adding Burning; add it then.
                var seman = player.GetSEMan();
                ashBurnedSerial = current!.Serial;
                if (StormHazards.AshAddBurning(seman.HaveStatusEffect(SEMan.s_statusEffectBurning), player.IsDead()))
                    seman.AddStatusEffect(SEMan.s_statusEffectBurning, resetTime: true);
            }
            catch (Exception error)
            {
                log?.Warning("Ash storm burn failed: " + error.GetType().Name);
            }
        }

        internal static void Reset()
        {
            ashLast = -1;
            last = -1;
            shockedSerial = 0;
            burnedSerial = 0;
            ashBurnedSerial = 0;
            Nearby.Clear();
        }

        internal static void Shutdown()
        {
            Reset();
            radius = -1;
            log = null;
        }

        static bool Standing(Vector3 at, Events.TempestEvent current, double now)
        {
            var area = Radius();
            if (area <= 0) return false;
            Nearby.Clear();
            ZDOMan.instance.FindSectorObjects(ZoneSystem.GetZone(at), new SimulationDistance(1, 0, classic: true), Nearby);
            var inside = false;
            foreach (var zdo in Nearby)
            {
                if (zdo == null || zdo.GetPrefab() != StrikeHash) continue;
                if (!StormHazards.Live(zdo.GetInt(StormHazards.SerialKey), zdo.GetLong(StormHazards.UntilKey) / 1000.0, current, now)) continue;
                var p = zdo.GetPosition();
                if (StormHazards.Inside(at.x - p.x, at.y - p.y, at.z - p.z, area)) { inside = true; break; }
            }
            Nearby.Clear();
            return inside;
        }

        /// <summary>The largest area of the game's lightning object (its AOE_AREA, 2.42 m in the shipped prefab).</summary>
        static float Radius()
        {
            if (radius >= 0) return radius;
            var prefab = ZNetScene.instance.GetPrefab(StrikeHash);
            if (prefab == null) return 0;
            radius = 0;
            foreach (var aoe in prefab.GetComponentsInChildren<Aoe>(true))
                if (aoe.m_useCollider == null) radius = Mathf.Max(radius, aoe.m_radius);
            return radius;
        }
    }
}
