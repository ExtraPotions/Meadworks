// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using System;
using ExtraPotions.Wildblood.Adopted.Tally;
using ExtraPotions.Wildblood.Adopted.Traits;
using HarmonyLib;
using ExtraPotions.Wildblood.Runtime;
using ExtraPotions.Wildblood.Integration;
using ExtraPotions.Wildblood.AdoptedLibs.PatchGuard;
using UnityEngine;

namespace ExtraPotions.Wildblood.Adopted.Patches
{
    /// <summary>
    /// The boss damage board's tally, on the owner of the boss or of one of its Phantom copies, where the game applies
    /// every hit. The health before the hit is read in the prefix and the loss credited to the attacking player in the
    /// postfix, so the tally counts what was actually lost: after resistances, and never the overkill below zero. Only
    /// players are credited. A hit on a copy counts on its boss's board, the same way (<see cref="BossCredit"/>). The
    /// prefix never throws, so it can never stop a hit landing.
    /// </summary>
    [HarmonyPatch(typeof(Character), nameof(Character.ApplyDamage))]
    public static class BossDamagePatch
    {
        private static void Prefix(Character __instance, out float __state)
        {
            __state = 0f;
            try
            {
                __state = Tallied(__instance) ? __instance.GetHealth() : 0f;
            }
            catch (Exception e)
            {
                Guard.Report(e, "Character.ApplyDamage boss tally");
            }
        }

        // A copy is its boss's own prefab, so it is a boss too; its mark is read as well in case that ever changes.
        private static bool Tallied(Character character)
        {
            if (!Plugin.NativeRulesReady || !WildbloodModules.Active(WildbloodModule.Bosses)) return false;
            if (character.IsBoss())
            {
                return true;
            }
            ZNetView nview = character.m_nview;
            return !character.IsPlayer() && nview != null && nview.IsValid()
                && AspectStore.GetPhantomOf(nview.GetZDO()) != ZDOID.None;
        }

        // Every hit on every character comes through here: anything but a tallied one stops at the first test.
        private static void Postfix(Character __instance, HitData hit, float __state)
        {
            if (__state > 0f)
            {
                SafeCall.Run("Character.ApplyDamage boss tally", static (victim, blow) => Credit(victim, blow.hit, blow.before),
                    __instance, (hit, before: __state));
            }
        }

        private static void Credit(Character victim, HitData hit, float before)
        {
            ZNetView nview = victim.m_nview;
            if (!Plugin.NativeRulesReady || !WildbloodModules.Active(WildbloodModule.Bosses) || before <= 0f || nview == null || !nview.IsValid() || !nview.IsOwner())
            {
                return;
            }
            float loss = before - Mathf.Max(0f, victim.GetHealth());
            if (loss > 0f && hit.GetAttacker() is Player player)
            {
                BossCredit.Add(nview.GetZDO(), player, loss);
            }
        }
    }

    /// <summary>
    /// The boss damage board's announcement, on the boss's owner as it dies, while its ZDO still holds the tally (the
    /// game resets the ZDO by the end of <c>OnDeath</c>). Never throws: nothing here may stop a boss dying.
    /// </summary>
    [HarmonyPatch(typeof(Character), "OnDeath")]
    public static class BossBoardDeathPatch
    {
        // Every death of every character comes through here: anything but a boss stops at the first test.
        private static void Prefix(Character __instance)
        {
            if (Plugin.NativeRulesReady && WildbloodModules.Active(WildbloodModule.Bosses) && __instance.IsBoss())
            {
                SafeCall.Run("Character.OnDeath boss board", static victim => Announce(victim), __instance);
            }
        }

        private static void Announce(Character victim)
        {
            ZNetView nview = victim.m_nview;
            if (victim.IsBoss() && nview != null && nview.IsValid() && nview.IsOwner())
            {
                BossBoardRpc.Announce(victim, nview.GetZDO());
            }
        }
    }

    /// <summary>
    /// World start, on every machine: the board's messages are registered before any boss can be hit or fall - the
    /// board itself, a copy's hits credited to its boss's owner, and a late joiner asking the server for the latest.
    /// </summary>
    [HarmonyPatch(typeof(ZoneSystem), "Start")]
    public static class BossBoardStartPatch
    {
        private static void Postfix()
        {
            SafeCall.Run("ZoneSystem.Start boss board", BossBoardRpc.EnsureRegistered);
            SafeCall.Run("ZoneSystem.Start boss credit", BossCredit.EnsureRegistered);
            SafeCall.Run("ZoneSystem.Start boss board recall", BossBoardRecall.EnsureRegistered);
        }
    }
}
