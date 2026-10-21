// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using System;
using ExtraPotions.Wildblood.Adopted.Aspects;
using HarmonyLib;
using ExtraPotions.Wildblood.AdoptedLibs.PatchGuard;
using UnityEngine;

namespace ExtraPotions.Wildblood.Adopted.Patches
{
    /// <summary>
    /// The moment a boss's attack starts - its wind-up - handed to a Portalbound boss's behaviour, which opens the far
    /// portal for an attack the portals carry. The game starts a creature's attacks only on its owner, so this runs
    /// there and nowhere else; an attack the game refuses to start never gets here. Every character's attacks come
    /// through, so anything that is not a boss is passed over at once. It runs inside the game's attack, so a failure
    /// is reported and swallowed rather than cutting the attack short.
    /// </summary>
    [HarmonyPatch(typeof(Attack), nameof(Attack.Start))]
    public static class PortalStartPatch
    {
        private static void Postfix(Attack __instance, bool __result, Humanoid character)
        {
            if (!__result || character == null || !character.IsBoss())
            {
                return;
            }
            SafeCall.Run("Attack started (Portalbound)", () => Started(__instance, character));
        }

        private static void Started(Attack attack, Humanoid boss)
        {
            PortalboundBehaviour portal = boss.GetComponent<PortalboundBehaviour>();
            if (portal != null)
            {
                portal.Started(attack);
            }
        }
    }

    /// <summary>
    /// Where the game places a projectile and which way it sends it: asked once as a projectile attack's animation
    /// lets go (for the release sound) and again for every projectile of every burst after it, only on the attacker's
    /// owner. For a Portalbound boss's portal-carried attack the answer is changed to the far portal and the target, so
    /// the vines or the slime - the game's own networked projectiles, with the game's own spread added after - fly from there.
    /// Anything that is not a boss is a single check. A failure is reported and never rethrown.
    /// </summary>
    [HarmonyPatch(typeof(Attack), nameof(Attack.GetProjectileSpawnPoint))]
    public static class PortalSpawnPointPatch
    {
        private static void Postfix(Attack __instance, Humanoid ___m_character, ref Vector3 spawnPoint, ref Vector3 aimDir)
        {
            if (___m_character == null || !___m_character.IsBoss())
            {
                return;
            }
            try
            {
                PortalboundBehaviour portal = ___m_character.GetComponent<PortalboundBehaviour>();
                if (portal != null)
                {
                    portal.Redirect(__instance, ref spawnPoint, ref aimDir);
                }
            }
            catch (Exception e)
            {
                Guard.Report(e, "Attack.GetProjectileSpawnPoint portalbound");
            }
        }
    }
}
