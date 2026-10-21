// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Original native API implementation. VAT is a behavior only design reference; no source or assemblies accessed.
// DeathTweaks was inspected as an approved public-domain inspiration; its complete OnDeath replacement is not adopted.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using CruciblePlugin = ExtraPotions.Meadworks.Crucible.Plugin;

namespace ExtraPotions.Hex.Runtime.Native;

internal static class HexDeath
{
    private static List<StatusEffect>? savedEffects;
    private static long savedGeneration, savedPlayer;
    private static Player? deadPlayer;
    private static Action<StatusEffect, Character>? attach;

    // OnDeath has already set the dead ZDO flag at these calls. Keep the owner gate, without the living-player condition.
    private static bool Keep(Player actor, string key) => actor != null && actor == Player.m_localPlayer
        && actor.m_nview != null && actor.m_nview.IsValid() && actor.m_nview.IsOwner()
        && Plugin.Settings?.Effective(key, false) == true;

    private static void Tombstone(Player player)
    {
        if (!Keep(player, "keep_items")) player.CreateTombStone();
    }
    private static void Food(List<Player.Food> foods, Player player)
    {
        if (!Keep(player, "keep_food")) foods.Clear();
    }
    private static void ResetSkills(Skills skills, Player player)
    {
        if (!Keep(player, "keep_skills")) skills.Clear();
    }
    private static void ReduceSkills(Skills skills, Player player)
    {
        if (!Keep(player, "keep_skills")) skills.OnDeath();
    }

    private static void RemoveEffects(SEMan manager, bool quiet, Player player)
    {
        Reset();
        if (Keep(player, "keep_status_effects"))
        {
            try
            {
                var captured = new List<StatusEffect>();
                foreach (var effect in manager.GetStatusEffects())
                {
                    try
                    {
                        if (effect == null) continue;
                        if (effect.GetType().Assembly != typeof(StatusEffect).Assembly)
                        {
                            UnityEngine.Debug.LogWarning("Hex leaves an unsupported external status-effect type on the native death-removal path.");
                            continue;
                        }
                        var clone = effect.Clone();
                        // Native Clone is shallow. Visual instances belong to the dying manager.
                        clone.m_startEffectInstances = null;
                        captured.Add(clone);
                    }
                    catch (Exception) { UnityEngine.Debug.LogWarning("Hex could not retain a status effect at death."); }
                }
                savedGeneration = CruciblePlugin.Session.Generation;
                savedPlayer = player.GetPlayerID();
                deadPlayer = player;
                savedEffects = captured;
            }
            catch (Exception) { Reset(); UnityEngine.Debug.LogWarning("Hex could not retain status effects at death."); }
        }
        // Capture failures never suppress native Stop, the remaining death path, or respawn.
        manager.RemoveAllStatusEffects(quiet);
    }

    internal static void Reset() { savedEffects = null; deadPlayer = null; savedPlayer = savedGeneration = 0; }

    internal static void Tick()
    {
        if (savedEffects == null) return;
        if (!Plugin.Ready || !Plugin.Active || !CruciblePlugin.IsReady
            || savedGeneration != CruciblePlugin.Session.Generation) { Reset(); return; }
        if (CruciblePlugin.Session.AuthorityReady && Plugin.Settings?.Effective("keep_status_effects", false) != true)
        { Reset(); return; }
        var player = Player.m_localPlayer;
        if (player == null || player == deadPlayer || !HexMovement.Owned(player)) return;
        if (player.GetPlayerID() != savedPlayer) { Reset(); return; }
        if (!CruciblePlugin.Session.AuthorityReady) return;
        var effects = savedEffects;
        Reset(); // a failing custom effect must not retry or duplicate the other effects
        if (!Keep(player, "keep_status_effects")) return;
        var manager = player.GetSEMan();
        foreach (var effect in effects)
        {
            var hash = 0;
            var added = false;
            try
            {
                hash = effect.NameHash();
                if (manager.HaveStatusEffect(hash) || !effect.CanAdd(player)) continue;
                manager.m_statusEffects.Add(effect);
                manager.m_statusEffectsHashSet.Add(hash);
                added = true;
                var elapsed = effect.m_time;
                var ttl = effect.m_ttl;
                // Reattach through the native base Setup. Derived initializers can regrant potion
                // up-front resources or refresh Rested; the retained runtime state already exists.
                attach ??= AccessTools.MethodDelegate<Action<StatusEffect, Character>>(
                    AccessTools.DeclaredMethod(typeof(StatusEffect), nameof(StatusEffect.Setup)), virtualCall: false);
                attach(effect, player);
                effect.m_time = elapsed;
                effect.m_ttl = ttl;
            }
            catch (Exception)
            {
                if (added)
                {
                    manager.m_statusEffects.Remove(effect);
                    manager.m_statusEffectsHashSet.Remove(hash);
                    try { effect.Stop(); } catch (Exception) { }
                }
                UnityEngine.Debug.LogWarning("Hex could not restore a status effect after death.");
            }
        }
    }

    [HarmonyPatch(typeof(Player), nameof(Player.OnDeath))]
    private static class Death
    {
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var replacements = new Dictionary<MethodInfo, (string Method, bool Actor)> {
                [AccessTools.DeclaredMethod(typeof(Player), nameof(Player.CreateTombStone))] = (nameof(Tombstone), false),
                [AccessTools.DeclaredMethod(typeof(List<Player.Food>), nameof(List<Player.Food>.Clear))] = (nameof(Food), true),
                [AccessTools.DeclaredMethod(typeof(Skills), nameof(Skills.Clear))] = (nameof(ResetSkills), true),
                [AccessTools.DeclaredMethod(typeof(Skills), nameof(Skills.OnDeath))] = (nameof(ReduceSkills), true),
                [AccessTools.DeclaredMethod(typeof(SEMan), nameof(SEMan.RemoveAllStatusEffects))] = (nameof(RemoveEffects), true) };
            var codes = instructions.ToList();
            foreach (var target in replacements.Keys)
            {
                var calls = codes.Where(code => code.Calls(target)).ToList();
                if (calls.Count != 1 || calls[0].blocks.Count != 0)
                    throw new InvalidOperationException("Hex death protection refused: native death call changed: " + target.Name);
            }
            foreach (var code in codes)
            {
                if ((code.opcode == OpCodes.Call || code.opcode == OpCodes.Callvirt)
                    && code.operand is MethodInfo target && replacements.TryGetValue(target, out var replacement))
                {
                    if (replacement.Actor)
                    {
                        var actor = new CodeInstruction(OpCodes.Ldarg_0);
                        code.MoveLabelsTo(actor);
                        yield return actor;
                    }
                    code.opcode = OpCodes.Call;
                    code.operand = AccessTools.DeclaredMethod(typeof(HexDeath), replacement.Method);
                }
                yield return code;
            }
        }
    }
}
