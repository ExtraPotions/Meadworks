// OpenKeep. Copyright (C) 2026 Gerald Glasgow. GPL-3.0-or-later.
// Adapted from Homestead BedWait/BedLoadPatch and AreaLoading AreaSettle at 684671f98de54db6ea6652092ec041f44016c37d.
// Changed by expDARE, 2026: observed request ownership, reversible per-call thresholds and bounded remote quiet sampling.
// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using ExtraPotions.Ember.Beds;
using ExtraPotions.Ember.Config;
using UnityEngine;
using CruciblePlugin = ExtraPotions.Meadworks.Crucible.Plugin;

namespace ExtraPotions.Ember.Runtime
{
    internal static class QuickRespawn
    {
        internal sealed class DeathCall { internal Player Actor; internal Game Game; internal long Sequence, Attempts; internal string Scope; internal long Generation; }
        internal sealed class LoadCall { internal Game Game; internal float Before, Applied; internal bool Changed; }
        static long sequence, attempts, ownedAttempt, lastRequestFrame; static Player? actor;
        static bool choicePending, chosenLoad, currentBedObserved; static float choiceDeadline; static Player? lastActor; static Game? lastGame;
        static float lastDelay, lastTime; static bool lastAfterDeath, selfSchedule;
        static bool captured, requestFired, segmentStarted, segmentDisabled, holding;
        static string? scope; static long generation; static Game? game;
        static BedPoint deathPoint, target; static float deathTime, scheduledDelay, scheduledAt, loadStartedAt, baseDuration, lastWait;
        static int sampleCount = -1, sampleReads, sampleChanges;
        static long sampleIds, sampleRevisions;
        static float sampleTime, sampleChangedAt, lastBaseThreshold, lastEffectiveThreshold, lastWaitBefore, lastWaitAfter;
        static int loadCalls, appliedCalls, restoredCalls, skippedCalls, thrownCalls, foreignValueCalls;
        static string holdReason = "none";
        static readonly AreaQuiet quiet = new AreaQuiet();
        static readonly List<ZDO> found = new List<ZDO>();
        static readonly SimulationDistance around = new SimulationDistance(1, 0);

        internal static bool Active => Plugin.AllowsUi && EmberModules.ProductActive(out _)
            && Plugin.Settings?.Get(EmberSettings.Sleep.Enabled) == true
            && Plugin.Settings.Get(EmberSettings.Sleep.QuickRespawn) && SourceAllows();
        static bool SourceAllows()
        {
            if (!Chainloader.PluginInfos.TryGetValue("milkyteam.openkeep", out var source)) return true;
            var plugin = source.Instance as BaseUnityPlugin;
            return plugin != null && plugin.Config.TryGetEntry<bool>(new ConfigDefinition("8. Homestead", "Quick Respawn"), out var enabled) && !enabled.Value;
        }
        static BedPoint Point(Vector3 p) => new BedPoint(p.x, p.y, p.z);
        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        static bool CurrentScope() => captured && game && ReferenceEquals(game, Game.instance)
            && scope == OwnedBedRespawn.Scope() && generation == CruciblePlugin.Session.Generation;
        static bool Current() => CurrentScope() && Active;
        internal static bool ChoiceCurrent => choicePending && CurrentScope() && OwnedBedRespawn.Active
            && actor && actor == Player.m_localPlayer && actor.IsDead() && actor.m_nview && actor.m_nview.IsValid()
            && actor.m_nview.IsOwner() && attempts == ownedAttempt && game.IsInvoking("_RequestRespawn") && !game.WaitingForRespawn();
        static bool PendingOwned() => CurrentScope() && actor && actor == Player.m_localPlayer && actor.IsDead()
            && actor.m_nview && actor.m_nview.IsValid() && actor.m_nview.IsOwner() && attempts == ownedAttempt
            && game.IsInvoking("_RequestRespawn") && !game.WaitingForRespawn();
        internal static DeathCall? BeforeDeath(Player player)
        {
            var current = OwnedBedRespawn.Scope();
            if ((!Active && !OwnedBedRespawn.Active) || current == null || !Game.instance || !player || player != Player.m_localPlayer || player.IsDead()
                || !player.m_nview || !player.m_nview.IsValid() || !player.m_nview.IsOwner()) return null;
            return new DeathCall { Actor = player, Game = Game.instance, Sequence = sequence, Attempts = attempts, Scope = current, Generation = CruciblePlugin.Session.Generation };
        }
        internal static void RequestBeginning(Game instance)
        {
            attempts++;
            if (!selfSchedule && captured) Reset();
        }
        internal static void RequestFailed(Game instance) { if (captured && ReferenceEquals(game, instance)) Reset(); }
        internal static void Requested(Game instance, float delay, bool afterDeath, bool original)
        {
            if (!original) return;
            sequence++; lastGame = instance; lastActor = Player.m_localPlayer; lastDelay = delay;
            lastAfterDeath = afterDeath; lastRequestFrame = Time.frameCount; lastTime = Time.time;
            if (!selfSchedule && captured) Reset();
        }
        internal static void AfterDeath(Player player, DeathCall? call, bool original)
        {
            if (call == null || !original) return;
            Reset();
            if ((!Active && !OwnedBedRespawn.Active) || player != call.Actor || player != Player.m_localPlayer || !player.IsDead()
                || !ReferenceEquals(call.Game, Game.instance) || call.Scope != OwnedBedRespawn.Scope()
                || call.Generation != CruciblePlugin.Session.Generation || sequence != call.Sequence + 1 || attempts != call.Attempts + 1
                || lastActor != player || !ReferenceEquals(lastGame, call.Game) || !lastAfterDeath || lastDelay != RespawnWait.DeathSeconds
                || lastRequestFrame != Time.frameCount || !call.Game.IsInvoking("_RequestRespawn") || call.Game.WaitingForRespawn()) return;
            var origin = Point(player.transform.position); float nativeDuration = call.Game.m_respawnLoadDuration;
            if (!BedPoints.Finite(origin) || !Finite(nativeDuration) || nativeDuration < 0 || !TryTarget(call.Game, out var destination)) return;
            captured = true; game = call.Game; actor = player; scope = call.Scope; generation = call.Generation;
            deathPoint = origin; deathTime = lastTime; ownedAttempt = attempts;
            long beforeChoice = attempts;
            if (OwnedBedChoice.TryOpen(player, OwnedBedRespawn.Choices(), player.transform.position) || attempts != beforeChoice) return;
            if (!PendingOwned()) return;
            if (!Active) { Reset(); return; }
            Schedule(RespawnWait.DeathDelay(BedPoints.MapDistance(origin, destination), nativeDuration, Time.time - deathTime));
        }
        static bool Schedule(float delay)
        {
            if (!PendingOwned() || !Finite(delay) || delay < 0) return false;
            var scheduledGame = game; var scheduledActor = actor;
            long beforeSequence = sequence, beforeAttempts = attempts;
            selfSchedule = true;
            try { scheduledGame.RequestRespawn(delay, true); }
            catch { Reset(); throw; }
            finally { selfSchedule = false; }
            ownedAttempt = attempts;
            if (sequence != beforeSequence + 1 || attempts != beforeAttempts + 1 || !ReferenceEquals(lastGame, scheduledGame)
                || lastActor != scheduledActor || lastDelay != delay || !lastAfterDeath || lastRequestFrame != Time.frameCount
                || !PendingOwned()) { Reset(); return false; }
            scheduledDelay = delay; scheduledAt = Time.time; return true;
        }
        internal static bool TryBeginChoice(out float deadline)
        {
            deadline = 0;
            if (choicePending || !OwnedBedRespawn.Active || !PendingOwned()) return false;
            if (!Schedule(30f)) return false;
            choicePending = true; choiceDeadline = deadline = scheduledAt + scheduledDelay; return true;
        }
        internal static bool CompleteChoice()
        {
            if (!ChoiceCurrent) return false;
            bool quick = Active;
            float delay = quick ? 0 : Math.Max(0, deathTime + RespawnWait.DeathSeconds - Time.time);
            if (!Schedule(delay)) return false;
            choicePending = false; chosenLoad = quick;
            if (!quick) Reset();
            return true;
        }
        internal static void AbandonChoice() { if (choicePending) Reset(); }
        static bool TryTarget(Game instance, out BedPoint destination)
        {
            destination = default; var profile = instance.GetPlayerProfile();
            if (profile != null && profile.HaveCustomSpawnPoint()) { destination = Point(profile.GetCustomSpawnPoint()); return BedPoints.Finite(destination); }
            if (!ZoneSystem.instance || !ZoneSystem.instance.GetLocationIcon(instance.m_StartLocation, out var start)) return false;
            destination = Point(start); return BedPoints.Finite(destination);
        }
        internal static void RequestFired(Game instance, bool original)
        {
            if (original && CurrentScope() && ReferenceEquals(instance, game) && attempts == ownedAttempt)
            {
                if (choicePending) { choicePending = false; chosenLoad = Active; }
                requestFired = true;
                if (!Active) Reset();
            }
        }
        internal static void Tick()
        {
            if (captured && (!CurrentScope() || (choicePending ? !ChoiceCurrent : !Active))) Reset();
        }
        internal static LoadCall? BeforeLoad(Game instance, float dt, bool original)
        {
            if (!original || !Current() || !requestFired || !ReferenceEquals(instance, game)
                || !instance.m_respawnAfterDeath || !instance.WaitingForRespawn() || !Finite(dt) || dt <= 0) return null;
            var profile = instance.GetPlayerProfile();
            if (profile == null || !profile.HaveCustomSpawnPoint()) return null;
            var at = Point(profile.GetCustomSpawnPoint()); float duration = instance.m_respawnLoadDuration, wait = instance.m_respawnWait;
            if (!BedPoints.Finite(at) || !Finite(duration) || duration < 0 || !Finite(wait)) { segmentDisabled = true; return null; }
            bool fresh = !segmentStarted || wait <= 0;
            if (segmentStarted && wait > 0 && wait < lastWait) { segmentDisabled = true; quiet.Reset(); }
            if (fresh) { target = at; baseDuration = duration; loadStartedAt = Time.time; segmentStarted = true; ResetQuiet(); }
            else if (target.X != at.X || target.Y != at.Y || target.Z != at.Z || duration != baseDuration) { segmentDisabled = true; quiet.Reset(); }
            if (segmentDisabled) return null;
            loadCalls++; lastWaitBefore = wait; lastBaseThreshold = duration;
            float threshold = chosenLoad ? 0 : RespawnWait.LoadDuration(BedPoints.MapDistance(deathPoint, at), duration); holding = false; holdReason = "local-authority";
            if (ZNet.instance && !ZNet.instance.IsServer())
            {
                if (ZDOMan.instance == null || !ZoneSystem.instance) { segmentDisabled = true; holdReason = "unavailable"; return null; }
                bool settled;
                try { settled = Settled(at); }
                catch (Exception) { segmentDisabled = true; holdReason = "sample-failed"; quiet.Reset(); return null; }
                bool ready = settled && (!chosenLoad || currentBedObserved);
                bool deadline = Time.time - loadStartedAt >= baseDuration;
                holdReason = ready ? "settled" : deadline ? "native-deadline" : chosenLoad && !currentBedObserved ? "bed-arrival" : "quiet";
                if (!ready && !deadline) { threshold = Math.Max(threshold, wait + dt); holding = true; }
            }
            lastEffectiveThreshold = threshold;
            var call = new LoadCall { Game = instance, Before = duration, Applied = threshold, Changed = threshold != duration };
            if (call.Changed) { instance.m_respawnLoadDuration = threshold; appliedCalls++; }
            return call;
        }
        static bool Settled(BedPoint at)
        {
            if (!quiet.Due(Time.time)) return quiet.Settled;
            found.Clear();
            try
            {
                var position = new Vector3(at.X, at.Y, at.Z);
                ZDOMan.instance.FindSectorObjects(ZoneSystem.GetZone(position), around, found);
                // Quiet sparse objects do not establish that the selected remote bed has arrived.
                if (chosenLoad) currentBedObserved = game.FindBedNearby(position, 5f) != null;
                long ids = 0, revisions = 0;
                unchecked { foreach (var zdo in found) { ids ^= zdo.m_uid.UserID * 31L + zdo.m_uid.ID; revisions += zdo.DataRevision; } }
                sampleReads++;
                if (sampleCount != found.Count || sampleIds != ids || sampleRevisions != revisions) { sampleChanges++; sampleChangedAt = Time.time; }
                sampleCount = found.Count; sampleIds = ids; sampleRevisions = revisions; sampleTime = Time.time;
                return quiet.Observe(Time.time, found.Count, ids, revisions);
            }
            finally { found.Clear(); }
        }
        internal static void AfterLoad(LoadCall? call, bool original, bool threw = false)
        {
            if (call == null) return;
            if (call.Changed && call.Game)
            {
                if (call.Game.m_respawnLoadDuration == call.Applied) { call.Game.m_respawnLoadDuration = call.Before; restoredCalls++; }
                else foreignValueCalls++;
            }
            call.Changed = false;
            if (!original || threw) { segmentDisabled = true; if (threw) thrownCalls++; else skippedCalls++; quiet.Reset(); return; }
            lastWait = lastWaitAfter = call.Game.m_respawnWait;
        }
        static void ResetQuiet()
        {
            quiet.Reset(); currentBedObserved = false; sampleCount = -1; sampleIds = sampleRevisions = 0; sampleTime = sampleChangedAt = 0;
        }
        internal static void Reset()
        {
            captured = requestFired = segmentStarted = segmentDisabled = holding = choicePending = chosenLoad = false;
            actor = null; ownedAttempt = 0; choiceDeadline = 0;
            scope = null; game = null; generation = 0; deathPoint = target = default;
            deathTime = scheduledDelay = scheduledAt = loadStartedAt = baseDuration = lastWait = 0;
            ResetQuiet(); found.Clear(); holdReason = "none";
        }
        internal static object Status() => new {
            active = Active, captured, scope, requestSequence = sequence, requestAttempts = attempts, ownedRequestAttempt = ownedAttempt,
            choicePending, choiceDeadline, choiceOwned = ChoiceCurrent, choiceActor = actor ? actor.GetPlayerID().ToString() : null, chosenLoad, scheduledDelay, deathTime,
            scheduledAt, deathDeadline = scheduledAt + scheduledDelay, requestFired, loadStartedAt, nativeDeadline = loadStartedAt + baseDuration,
            deathPoint = new { x = deathPoint.X, y = deathPoint.Y, z = deathPoint.Z },
            target = new { x = target.X, y = target.Y, z = target.Z },
            baseDuration, nativeWait = Game.instance ? Game.instance.m_respawnWait : 0,
            nativeDuration = Game.instance ? Game.instance.m_respawnLoadDuration : 0,
            lastBaseThreshold, lastEffectiveThreshold, lastWaitBefore, lastWaitAfter,
            segmentDisabled, holding, holdReason, currentBedObserved, quiet = quiet.Settled,
            sampleCount, sampleIds, sampleRevisions, sampleTime, sampleChangedAt, sampleReads, sampleChanges,
            loadCalls, appliedCalls, restoredCalls, skippedCalls, thrownCalls, foreignValueCalls
        };
    }
}
