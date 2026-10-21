// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using ExtraPotions.Wildblood.Adopted.Aspects;
using ExtraPotions.Wildblood.Adopted.Hints;
using ExtraPotions.Wildblood.Adopted.Traits;
using HarmonyLib;
using ExtraPotions.Wildblood.Runtime;
using ExtraPotions.Wildblood.Integration;

namespace ExtraPotions.Wildblood.Adopted.Patches
{
    /// <summary>
    /// A boss hint, asked for on the boss's owner as it dies, while its ZDO still says what it was: never for a Phantom
    /// copy, a decoy, and for a Tethered pair only when the last of the two falls, as the damage board. Both of a Twin
    /// pair ask; the server answers once. Never throws: nothing here may stop a boss dying.
    /// </summary>
    [HarmonyPatch(typeof(Character), "OnDeath")]
    public static class BossHintDeathPatch
    {
        // Every death of every character comes through here: anything but a boss stops at the first test.
        private static void Prefix(Character __instance)
        {
            if (Plugin.NativeRulesReady && WildbloodModules.Active(WildbloodModule.Bosses) && __instance.IsBoss())
            {
                SafeCall.Run("Character.OnDeath boss hint", static victim => Ask(victim), __instance);
            }
        }

        private static void Ask(Character victim)
        {
            ZNetView nview = victim.m_nview;
            if (!victim.IsBoss() || nview == null || !nview.IsValid() || !nview.IsOwner())
            {
                return;
            }
            ZDO zdo = nview.GetZDO();
            if (AspectStore.GetPhantomOf(zdo) == ZDOID.None && !TetherPair.Standing(AspectStore.GetTether(zdo)))
            {
                BossHintRpc.Request(zdo.m_uid, Utils.GetPrefabName(victim.gameObject), victim.transform.position);
            }
        }
    }

    /// <summary>World start, on every machine: the hint's question and answer are registered before any boss can fall.</summary>
    [HarmonyPatch(typeof(ZoneSystem), "Start")]
    public static class BossHintStartPatch
    {
        private static void Postfix() => SafeCall.Run("ZoneSystem.Start boss hints", BossHintRpc.EnsureRegistered);
    }
}
