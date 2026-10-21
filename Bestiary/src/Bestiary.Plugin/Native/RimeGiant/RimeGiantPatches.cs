// Portions Copyright (C) 2026 Gerald Glasgow. EliteCreaturesPack 0.7.2 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Changed by expDARE, 2026: Bestiary namespace, owned Crucible settings, rebuilt-only bundles and lifecycle adapters.
// SPDX-License-Identifier: GPL-3.0-or-later
using ExtraPotions.Bestiary.Runtime.Native.Core;
using HarmonyLib;
using UnityEngine;

namespace ExtraPotions.Bestiary.Runtime.Native.RimeGiant
{
    /// <summary>Each zone's spawn system takes the giant's spawn entry as it wakes (<see cref="RimeGiantSpawns"/>).</summary>
    [HarmonyPatch(typeof(SpawnSystem), "Awake")]
    public static class RimeGiantSpawnPatch
    {
        private static void Postfix(SpawnSystem __instance) =>
            SafeCall.Run("SpawnSystem.Awake rime giant", static system => RimeGiantSpawns.Join(system), __instance);
    }

    /// <summary>
    /// A giant about to spawn from the wild spawn system: only on a mountain whose giant has not come yet, which the
    /// spawn then claims (<see cref="RimeGiantSpawns.Allow"/>). Every other spawn passes untouched.
    /// </summary>
    [HarmonyPatch(typeof(SpawnSystem), "Spawn")]
    public static class RimeGiantSpawnGatePatch
    {
        private static bool Prefix(SpawnSystem.SpawnData critter, Vector3 spawnPoint)
        {
            if (critter?.m_prefab == null || critter.m_prefab != RimeGiantPrefabs.Prefab || SpawnSystem.m_nospawn)
            {
                return true;   // not ours, or the game's no-spawn switch is on and it spawns nothing (nor claims)
            }
            return SafeCall.Run("SpawnSystem.Spawn rime giant", static point => RimeGiantSpawns.Allow(point), spawnPoint, false);
        }
    }

    /// <summary>
    /// A hit landing on a giant, on its owner and before resistances: its plates turn the physical part aside
    /// (<see cref="RimeArmour.Soften"/>). Runs for every hit on every creature, so it only looks the armour up.
    /// </summary>
    [HarmonyPatch(typeof(Character), "RPC_Damage")]
    public static class RimeArmourHitPatch
    {
        private static void Prefix(Character __instance, HitData hit)
        {
            if (hit != null && __instance.TryGetComponent(out RimeArmour armour) && __instance.m_nview.IsOwner())
            {
                SafeCall.Run("Character.RPC_Damage rime armour", static (a, h) => a.Soften(h), armour, hit);
            }
        }
    }

    /// <summary>
    /// Fire reaching a giant: the game hands every hit's fire to burning, which applies it tick by tick here, on the
    /// owner; each tick counts towards breaking a plate (<see cref="RimeArmour.Melt"/>).
    /// </summary>
    [HarmonyPatch(typeof(Character), nameof(Character.ApplyDamage))]
    public static class RimeArmourFirePatch
    {
        private static void Prefix(Character __instance, HitData hit)
        {
            if (hit != null && hit.m_damage.m_fire > 0f && __instance.TryGetComponent(out RimeArmour armour))
            {
                SafeCall.Run("Character.ApplyDamage rime armour", static (a, fire) => a.Melt(fire), armour, hit.m_damage.m_fire);
            }
        }
    }

    /// <summary>The slam landing (on the giant's owner, where attacks run) sends the avalanche (<see cref="RimeAvalanche"/>).</summary>
    [HarmonyPatch(typeof(Attack), nameof(Attack.OnAttackTrigger))]
    public static class RimeAvalanchePatch
    {
        private static void Postfix(Attack __instance)
        {
            if (__instance.m_weapon?.m_shared.m_name != RimeAttacks.SlamName || __instance.m_character == null
                || __instance.m_character.IsStaggering() || !__instance.m_character.TryGetComponent(out RimeAvalanche avalanche))
            {
                return;
            }
            SafeCall.Run("Attack.OnAttackTrigger rime avalanche", avalanche.Roll);
        }
    }

    /// <summary>
    /// The colour the game hands a falling creature's ragdoll: an unstarred giant's is its frost (<see cref="RimeLook"/>),
    /// not the troll's plain blue. Every star look the giant has already carries the frost (<see cref="RimeLook.StarLooks"/>),
    /// whether the game picked it from the level or Elite Creatures Reborn from its stars, so only the plain look - no
    /// shift at all - is replaced. Runs last, after any other mod's choice.
    /// </summary>
    [HarmonyPatch(typeof(LevelEffects), nameof(LevelEffects.GetColorChanges))]
    public static class RimeGiantColorPatch
    {
        [HarmonyPriority(Priority.Last)]
        private static void Postfix(LevelEffects __instance, ref float hue, ref float saturation, ref float value)
        {
            bool plain = hue == 0f && saturation == 0f && value == 0f;
            if (plain && __instance.m_character != null && __instance.m_character.TryGetComponent(out RimeArmour _))
            {
                hue = RimeLook.Hue;
                saturation = RimeLook.Saturation;
                value = RimeLook.Value;
            }
        }
    }
}
