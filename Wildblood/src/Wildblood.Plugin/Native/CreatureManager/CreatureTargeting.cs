// SPDX-License-Identifier: GPL-3.0-only
// Portions from CreatureManager 1.3.1 (sighsorry1029), GPL-3.0
// e0a216a1ab73fc9fbec45060ed46dae031859b5f
// Target switching adapted from CreatureManager e0a216a1ab73fc9fbec45060ed46dae031859b5f
// Changed by expDARE, 2026: effective settings, owned session reset and existing pursuit priorities.
using System;
using System.Collections.Generic;
using UnityEngine;

namespace ExtraPotions.Wildblood.AdoptedCm;

// Owner-local decisions only. The game's existing UpdateTarget cadence, attack gate and
// subsequent target validation/abandonment still run; no target IDs or timers are persisted.
internal static class CreatureTargeting
{
    private static readonly Dictionary<Character, TargetState> States = new();
    private static bool lastEnabled;
    private static float lastInterval,lastHold;
    private static int lastChance;
    private static long lastGeneration=-1;
    private static bool RefreshSettings()
    {
        bool enabled=TargetingSettings.Enabled;float interval=TargetingSettings.Interval,hold=TargetingSettings.Hold;
        int chance=TargetingSettings.Chance;long generation=TargetingSettings.Generation;
        if(enabled!=lastEnabled||interval!=lastInterval||hold!=lastHold||chance!=lastChance||generation!=lastGeneration)States.Clear();
        lastEnabled=enabled;lastInterval=interval;lastHold=hold;lastChance=chance;lastGeneration=generation;
        return enabled&&chance>0;
    }

    private sealed class TargetState
    {
        internal ZDO Zdo = null!;
        internal long Owner;
        internal ushort OwnerRevision;
        internal float NextAttempt;
        internal Player? HeldPlayer;
        internal float HoldUntil;
    }

    // Character and view are loaded from the original protected BaseAI fields inside
    // MonsterAI.UpdateTarget by the transpiler, rather than accessed through publicized C#.
    internal static Character? SelectTarget(Character? vanillaTarget, MonsterAI ai, Character creature, ZNetView view)
    {
        try{return Select(vanillaTarget,ai,creature,view);}
        catch(Exception error){States.Remove(creature);ExtraPotions.Wildblood.AdoptedLibs.PatchGuard.Guard.Report(error,"MonsterAI.UpdateTarget player switch");return vanillaTarget;}
    }
    private static Character? Select(Character? vanillaTarget, MonsterAI ai, Character creature, ZNetView view)
    {
        if (!RefreshSettings())
        {
            return vanillaTarget;
        }

        Character current = ai.GetTargetCreature();
        if (view == null || !view.IsValid() || !view.IsOwner() || creature.IsDead() || creature.IsTamed() ||
            !ai.IsAlerted() || current == null || current.IsDead() || !BaseAI.IsEnemy(creature, current))
        {
            States.Remove(creature);
            return vanillaTarget;
        }

        ZDO zdo = view.GetZDO();
        if (Utils.GetPrefabName(creature.gameObject)=="FrozenKing_p2" || TargetingSettings.Priority(ai,creature))
        {
            States.Remove(creature);
            return vanillaTarget;
        }

        float now = Time.time;
        long owner = zdo.GetOwner();
        if (!States.TryGetValue(creature, out TargetState state) || !ReferenceEquals(state.Zdo, zdo) ||
            state.Owner != owner || state.OwnerRevision != zdo.OwnerRevision)
        {
            States[creature] = new TargetState
            {
                Zdo = zdo, Owner = owner, OwnerRevision = zdo.OwnerRevision,
                NextAttempt = now + lastInterval
            };
            return vanillaTarget;
        }

        if (state.HeldPlayer != null)
        {
            // Never restore a target that vanilla or another mod already cleared/replaced.
            if (state.HeldPlayer == current && now < state.HoldUntil &&
                CanSelect(ai, creature, state.HeldPlayer) && HasPath(ai, state.HeldPlayer))
            {
                return state.HeldPlayer;
            }

            state.HeldPlayer = null;
        }

        if (now < state.NextAttempt)
        {
            return vanillaTarget;
        }

        // A failed roll, empty candidate set or unreachable selection consumes this attempt.
        state.NextAttempt = now + lastInterval;
        int chance = lastChance;
        if (chance < 100 && UnityEngine.Random.value * 100f >= chance)
        {
            return vanillaTarget;
        }

        Player? selected = null;
        int count = 0;
        foreach (Player player in Player.GetAllPlayers())
        {
            if (player == current || !CanSelect(ai, creature, player)) continue;
            // Reservoir selection avoids sorting, temporary lists and a nearest-player bias.
            if (UnityEngine.Random.Range(0, ++count) == 0) selected = player;
        }

        // Only the selected player's path is queried, not a path for every candidate.
        if (selected == null || !HasPath(ai, selected)) return vanillaTarget;
        state.HeldPlayer = selected;
        state.HoldUntil = now + lastHold;
        return selected;
    }

    private static bool CanSelect(MonsterAI ai, Character creature, Player player)
    {
        return player != null && !player.IsDead() && !player.m_aiSkipTarget &&
               !player.InGhostMode() && !player.InDebugFlyMode() &&
               !(player == Player.m_localPlayer && CinematicsManager.IsPlaying()) &&
               BaseAI.IsEnemy(creature, player) && !(ai.m_skipLavaTargets && player.AboveOrInLava()) &&
               ai.CanSenseTarget(player);
    }

    private static bool HasPath(MonsterAI ai, Player player)
    {
        // Original HavePath preserves flying behavior and the creature's navigation agent.
        return TargetingSettings.HasPath(ai,player);
    }

    internal static void ForgetCharacter(Character character) => States.Remove(character);

    internal static void ResetRuntimeState() => States.Clear();
}
