// Tempest - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: lightning strike runtime (design 1.2, 4.2, W2, W3, W10, Q2; WildWeather behaviour only, its DLL never opened).
using System;
using System.Collections.Generic;
using ExtraPotions.Meadworks.Crucible;
using ExtraPotions.Tempest.Config;
using ExtraPotions.Tempest.Events;
using ExtraPotions.Tempest.Integration;
using ExtraPotions.Tempest.Weather;
using HarmonyLib;
using UnityEngine;

namespace ExtraPotions.Tempest.Runtime
{
    /// <summary>
    /// TP05b. The authority decides every strike: once a second during a thunderstorm's Active phase it plans the strikes
    /// due around each online player in an affected biome (<see cref="StrikeOrders.Plan"/>), drops points inside a base,
    /// ward or piece clearance found in the world's own objects (W3), and sends each order with its damage and effects to
    /// that player's client. The client checks the order (<see cref="StrikeOrders.Accept"/>), shows the game's lightning
    /// flash as the telegraph and one second later spawns the game's own networked lightning object, so the game applies
    /// lightning damage through its own hit path on the owner and every nearby player sees the strike. A Meteor Shower uses
    /// the same orders: the impact is the same networked object marked as a meteor, its lightning look hidden on every peer
    /// and replaced by the game's meteor hit effect, with blunt damage instead of lightning (and fire while Strike Starts
    /// Fires applies) and no lingering ground lightning. Main thread only.
    /// </summary>
    public static class TempestStrikes
    {
        public const string Rpc = "Tempest_Strike";
        public const string StrikePrefab = "lightningAOE";
        public const string TelegraphPrefab = "fx_Lightning";

        /// <summary>Network object key marking a strike object as a meteor impact (1).</summary>
        public const string MeteorKey = "Tempest_Meteor";

        /// <summary>The game's meteor hit effects, first found wins (Yagluth's, then Fader's).</summary>
        static readonly string[] MeteorImpacts = { "fx_goblinking_meteor_hit", "fx_fader_meteor_hit" };
        static GameObject? meteorImpact;
        static bool lookedForImpact;
        const double PlanSeconds = 1;
        const double MaximumCatchUp = 5;

        static readonly StrikeLedger Ledger = new StrikeLedger(256);
        static readonly List<(float Due, Vector3 At, StrikeOrder Order)> Pending = new List<(float, Vector3, StrikeOrder)>();
        static readonly List<ZDO> Nearby = new List<ZDO>();
        static readonly List<SparedArea> Areas = new List<SparedArea>();
        static readonly List<PiecePoint> Pieces = new List<PiecePoint>();
        static readonly Dictionary<int, (float Ward, float Base, bool Piece)> PrefabInfo = new Dictionary<int, (float, float, bool)>();
        static ZRoutedRpc? registeredOn;
        static double planned = -1;
        static int plannedSerial;
        static bool warnedPrefab;
        static MeadworksLog? log;

        public static void Initialize(MeadworksLog logger)
        {
            log = logger;
            TempestHazards.Initialize(logger);
            TempestFires.Initialize(logger);
        }

        /// <summary>Called from Update.</summary>
        public static void Tick()
        {
            if (ZNet.instance == null || ZRoutedRpc.instance == null)
            {
                Reset();
                return;
            }
            if (registeredOn != ZRoutedRpc.instance)
            {
                ZRoutedRpc.instance.Register<string>(Rpc, OnStrike);
                registeredOn = ZRoutedRpc.instance;
                Reset();
            }
            TempestEffects.Tick();
            if (TempestWorld.IsAuthority) Plan();
            Execute();
            TempestHazards.Tick();
        }

        public static void Shutdown()
        {
            Reset();
            TempestHazards.Shutdown();
            TempestFires.Shutdown();
            PrefabInfo.Clear();
            meteorImpact = null;
            lookedForImpact = false;
            registeredOn = null;
            log = null;
        }

        static void Reset()
        {
            TempestEffects.Clear();
            TempestHazards.Reset();
            Pending.Clear();
            Ledger.Clear();
            planned = -1;
            plannedSerial = 0;
        }

        // Authority.

        static void Plan()
        {
            var settings = Plugin.Settings;
            var e = TempestWorld.Current;
            var now = TempestWorld.Now;
            if (settings == null || e == null || !EventKinds.Strikes(e.Kind) || !TempestModules.Active(TempestModule.Events) || ZDOMan.instance == null)
            {
                planned = now;
                return;
            }
            if (plannedSerial != e.Serial || planned < 0)
            {
                // A new storm, a restart or a late authority start plans from now: no burst of missed strikes.
                plannedSerial = e.Serial;
                planned = now;
                return;
            }
            if (now - planned < PlanSeconds) return;
            var from = Math.Max(planned, now - MaximumCatchUp);
            planned = now;
            var fx = TempestSettings.Effects;
            var perMinute = StrikePlanner.Rate(e, settings.Get(fx.StrikesPerMinute), settings.Get(fx.LightningStrikeMultiplier));
            if (perMinute <= 0) return;
            var damage = settings.Get(fx.StrikeDamage);
            var clearance = settings.Get(fx.BaseClearance);
            var effects = StrikeTargets.Effects(settings.Get(fx.StrikeHurtsPlayers), settings.Get(fx.StrikeStartsFires), settings.Get(fx.StrikeHitsTrees));
            Func<float, float, bool> spared = (x, z) => Spared(x, z, clearance);
            if (!ZNet.instance.IsDedicated() && Player.m_localPlayer != null)
                Send(e, ZRoutedRpc.instance.m_id, Player.m_localPlayer.transform.position, perMinute, damage, effects, from, now, spared);
            foreach (var peer in ZNet.instance.GetPeers())
                if (peer != null && peer.IsReady() && peer.m_uid != 0)
                    Send(e, peer.m_uid, peer.m_refPos, perMinute, damage, effects, from, now, spared);
        }

        static void Send(TempestEvent e, long uid, Vector3 at, int perMinute, float damage, StrikeEffects effects, double from, double to, Func<float, float, bool> spared)
        {
            var generator = WorldGenerator.instance;
            if (generator == null || !e.Affects((WeatherBiome)(int)generator.GetBiome(at))) return;
            // A player inside a vanilla random event (raid) gets no strikes; the event's own sky already wins there.
            if (TempestWorld.InVanillaEvent(at)) return;
            foreach (var order in StrikeOrders.Plan(e, uid, at.x, at.z, perMinute, damage, effects, from, to, spared))
                ZRoutedRpc.instance.InvokeRoutedRPC(uid, Rpc, order.Encode());
        }

        /// <summary>W3 from the world's objects, which the authority holds for every zone: wards, player base areas (fires, workbenches) and player-built pieces.</summary>
        static bool Spared(float x, float z, float clearance)
        {
            if (ZNetScene.instance == null) return true;
            Nearby.Clear();
            Areas.Clear();
            Pieces.Clear();
            var reach = Mathf.Clamp(1 + Mathf.CeilToInt(Mathf.Max(clearance, 32f) / 64f), 1, 3);
            ZDOMan.instance.FindSectorObjects(ZoneSystem.GetZone(new Vector3(x, 0, z)), new SimulationDistance(reach, 0, classic: true), Nearby);
            foreach (var zdo in Nearby)
            {
                if (zdo == null) continue;
                var info = Info(zdo.GetPrefab());
                if (info.Ward <= 0 && info.Base <= 0 && !info.Piece) continue;
                var p = zdo.GetPosition();
                if (info.Ward > 0) Areas.Add(new SparedArea(p.x, p.z, info.Ward));
                if (info.Base > 0) Areas.Add(new SparedArea(p.x, p.z, info.Base));
                if (info.Piece && zdo.GetLong(ZDOVars.s_creator) != 0) Pieces.Add(new PiecePoint(p.x, p.z));
            }
            var result = BaseExclusion.Spared(x, z, Areas, Pieces, clearance);
            Nearby.Clear();
            return result;
        }

        static (float Ward, float Base, bool Piece) Info(int hash)
        {
            if (PrefabInfo.TryGetValue(hash, out var known)) return known;
            var prefab = ZNetScene.instance.GetPrefab(hash);
            var info = (Ward: 0f, Base: 0f, Piece: false);
            if (prefab != null)
            {
                var ward = prefab.GetComponentInChildren<PrivateArea>(true);
                if (ward != null) info.Ward = Mathf.Max(0, ward.m_radius);
                foreach (var area in prefab.GetComponentsInChildren<EffectArea>(true))
                    if ((area.m_type & EffectArea.Type.PlayerBase) != 0 && area.GetComponent<Collider>() is SphereCollider sphere)
                        info.Base = Mathf.Max(info.Base, sphere.radius * Mathf.Max(Mathf.Abs(area.transform.lossyScale.x), Mathf.Abs(area.transform.lossyScale.z)));
                info.Piece = prefab.GetComponent<Piece>() != null;
            }
            PrefabInfo[hash] = info;
            return info;
        }

        // Executing client.

        static void OnStrike(long sender, string text)
        {
            try
            {
                if (!TempestModules.Active(TempestModule.Events) || !FromAuthority(sender) || !StrikeOrder.TryDecode(text, out var order)) return;
                var player = Player.m_localPlayer;
                if (player == null || ZoneSystem.instance == null || ZNetScene.instance == null) return;
                var forced = (EnvMan.instance != null && !string.IsNullOrEmpty(EnvMan.instance.m_forceEnv)) || player.InInterior();
                if (StrikeOrders.Accept(order, TempestWorld.Current, ZRoutedRpc.instance.m_id, TempestWorld.Now, forced) != StrikeVerdict.Accepted) return;
                if (!Ledger.Admit(order.Id)) return;
                var at = new Vector3(order.X, 0, order.Z);
                at.y = ZoneSystem.instance.GetGroundHeight(at);
                if (TempestWorld.Current == null || EventKinds.Lightning(TempestWorld.Current.Kind)) Spawn(TelegraphPrefab, at);
                Pending.Add((Time.time + (float)StrikeOrders.TelegraphSeconds, at, order));
            }
            catch (Exception error)
            {
                log?.Warning("Lightning strike refused: " + error.GetType().Name);
            }
        }

        static bool FromAuthority(long sender)
        {
            if (ZNet.instance == null || ZRoutedRpc.instance == null) return false;
            if (ZNet.instance.IsServer()) return sender == ZRoutedRpc.instance.m_id;
            var server = ZNet.instance.GetServerPeer();
            return server != null && sender == server.m_uid;
        }

        static void Execute()
        {
            for (var i = Pending.Count - 1; i >= 0; i--)
            {
                var p = Pending[i];
                var player = Player.m_localPlayer;
                var forced = (EnvMan.instance != null && !string.IsNullOrEmpty(EnvMan.instance.m_forceEnv)) || (player != null && player.InInterior())
                    || TempestWorld.LocalInVanillaEvent;
                if (player == null || !TempestModules.Active(TempestModule.Events)
                    || StrikeOrders.Accept(p.Order, TempestWorld.Current, ZRoutedRpc.instance.m_id, TempestWorld.Now, forced) != StrikeVerdict.Accepted)
                {
                    Pending.RemoveAt(i);
                    continue;
                }
                if (Time.time < p.Due) continue;
                Pending.RemoveAt(i);
                try { Strike(p.At, p.Order); }
                catch (Exception error) { log?.Warning("Lightning strike failed: " + error.GetType().Name); }
            }
        }

        static void Strike(Vector3 at, StrikeOrder order)
        {
            var meteor = TempestWorld.Current?.Kind == EventKind.MeteorShower && MeteorImpact() != null;
            var strike = Spawn(StrikePrefab, at);
            if (strike == null) return;
            strike.AddComponent<TempestStrikeMark>().Effects = order.Effects;
            if (meteor)
            {
                var view = strike.GetComponent<ZNetView>();
                if (view != null && view.IsValid()) view.GetZDO().Set(MeteorKey, 1);
                ShowMeteor(strike);
            }
            else TempestHazards.Mark(strike, order.Serial);
            // Rain cancels fire: no fire damage and no ignition while the game reports rain here.
            var effects = StrikeTargets.InWeather(order.Effects, EnvMan.IsWet());
            var damage = StrikeTargets.Damage(order.Damage, effects);
            var radius = 0f;
            foreach (var aoe in strike.GetComponentsInChildren<Aoe>(true))
            {
                aoe.m_damage = meteor
                    ? new HitData.DamageTypes { m_blunt = damage.Lightning, m_fire = damage.Fire }
                    : new HitData.DamageTypes { m_lightning = damage.Lightning, m_fire = damage.Fire };
                if ((effects & StrikeEffects.StartsFires) == 0) aoe.m_spawnOnHitTerrain = null;
                aoe.m_damagePerLevel = new HitData.DamageTypes();
                if (aoe.m_useCollider == null) radius = Mathf.Max(radius, aoe.m_radius);
            }
            if ((effects & StrikeEffects.StartsFires) != 0) TempestFires.Ignite(at, radius, effects);
        }

        static GameObject? Spawn(string name, Vector3 at)
        {
            var prefab = ZNetScene.instance != null ? ZNetScene.instance.GetPrefab(name) : null;
            if (prefab == null)
            {
                if (!warnedPrefab) log?.Warning("The game's lightning object " + name + " was not found; strikes are skipped.");
                warnedPrefab = true;
                return null;
            }
            var spawned = UnityEngine.Object.Instantiate(prefab, at, Quaternion.identity);
            TempestEffects.Track(spawned, activeOnly: true);
            return spawned;
        }

        /// <summary>The game's meteor hit effect, or null (then a meteor impact shows as an ordinary strike).</summary>
        static GameObject? MeteorImpact()
        {
            if (lookedForImpact || ZNetScene.instance == null) return meteorImpact;
            lookedForImpact = true;
            foreach (var name in MeteorImpacts)
            {
                meteorImpact = ZNetScene.instance.GetPrefab(name);
                if (meteorImpact != null) break;
            }
            if (meteorImpact == null) log?.Warning("The game's meteor hit effect was not found; Meteor Shower impacts show as lightning strikes.");
            return meteorImpact;
        }

        /// <summary>Hides the strike object's lightning look and sound and shows the meteor hit effect in its place (every peer).</summary>
        static void ShowMeteor(GameObject strike)
        {
            var impact = MeteorImpact();
            if (impact == null) return;
            foreach (var renderer in strike.GetComponentsInChildren<Renderer>(true)) renderer.enabled = false;
            foreach (var light in strike.GetComponentsInChildren<Light>(true)) light.enabled = false;
            foreach (var sound in strike.GetComponentsInChildren<ZSFX>(true)) sound.enabled = false;
            foreach (var audio in strike.GetComponentsInChildren<AudioSource>(true)) { audio.Stop(); audio.enabled = false; }
            var shown = UnityEngine.Object.Instantiate(impact, strike.transform.position, Quaternion.identity);
            TempestEffects.Track(shown, activeOnly: false);
        }

        /// <summary>A meteor impact created on another peer from its network object gets the same look.</summary>
        [HarmonyPatch(typeof(ZNetScene), nameof(ZNetScene.CreateObject))]
        static class CreateObjectPatch
        {
            static void Postfix(object[] __args, GameObject __result)
            {
                try
                {
                    if (__result == null || __args == null || __args.Length == 0 || !(__args[0] is ZDO zdo)) return;
                    if (zdo.GetPrefab() != StrikeHash || zdo.GetInt(MeteorKey) != 1) return;
                    ShowMeteor(__result);
                }
                catch (Exception error)
                {
                    log?.Warning("Meteor impact look failed: " + error.GetType().Name);
                }
            }
        }

        static readonly int StrikeHash = StrikePrefab.GetStableHashCode();

        /// <summary>Marks a Tempest strike so the hit filter applies Strike Hurts Players, Strike Hits Trees and piece sparing.</summary>
        public sealed class TempestStrikeMark : MonoBehaviour
        {
            public StrikeEffects Effects;
        }

        [HarmonyPatch(typeof(Aoe), nameof(Aoe.ShouldHit))]
        static class ShouldHitPatch
        {
            static void Postfix(Aoe __instance, Collider collider, ref bool __result)
            {
                if (!__result || __instance == null) return;
                var mark = __instance.GetComponentInParent<TempestStrikeMark>();
                if (mark == null) return;
                var hit = Projectile.FindHitObject(collider);
                if (hit == null) return;
                __result = StrikeTargets.Allow(mark.Effects, Classify(hit));
            }

            static StrikeTarget Classify(GameObject hit)
            {
                var character = hit.GetComponent<Character>();
                if (character != null) return character.IsPlayer() ? StrikeTarget.Player : StrikeTarget.Creature;
                if (hit.GetComponent<TreeBase>() != null || hit.GetComponent<TreeLog>() != null) return StrikeTarget.Tree;
                if (hit.GetComponent<Piece>() != null) return StrikeTarget.Piece;
                return StrikeTarget.Other;
            }
        }
    }
}
