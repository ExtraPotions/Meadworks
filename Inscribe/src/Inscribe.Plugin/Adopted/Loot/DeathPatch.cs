// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
using System;
using ExtraPotions.Inscribe.Adopted.Core;
using HarmonyLib;

namespace ExtraPotions.Inscribe.Adopted.Loot
{
    /// <summary>
    /// Our own death hook (drops.md section 1, DECISIONS RC-12): our stones and gear never enter the creature's vanilla
    /// drop list, so the ragdoll path (prefab and amount only, custom data lost) and other loot mods never see them.
    /// A prefix, because the game destroys the creature's ZDO at the end of its death handling, and the roll needs the
    /// ZDO (attackers, ally flag, cheated, Elite Creatures Reborn's stars and copy marks). The game calls this on every peer that runs the death; the roller acts only
    /// on the ZDO owner, the same peer the game's own drops run on. Player deaths use the player's override and never
    /// reach this method.
    /// <para>
    /// The killer's loot-find totals are read once here (<see cref="KillerStats"/>): Fateweaver and Norns' Favour go into
    /// our roll, Trophy Taker and Hoardfinder open the <see cref="VanillaDropBoost"/> window for the game's own drop roll,
    /// which runs later in the same method; the finalizer closes it. Our drops are held while the death runs: the
    /// creature's ragdoll takes them (<see cref="CorpseLoot"/>), and the finalizer drops what no ragdoll took.
    /// </para>
    /// </summary>
    [HarmonyPatch(typeof(Character), nameof(Character.OnDeath))]
    internal static class DeathPatch
    {
        private static void Prefix(Character __instance)
        {
            try
            {
                if (!Runtime.InscribeModules.Active(global::ExtraPotions.Inscribe.Integration.InscribeModule.Drops) || __instance == null || __instance.IsPlayer() || __instance.m_nview == null || !__instance.m_nview.IsOwner())
                {
                    return;
                }
                LootModifiers modifiers = LootModifiers.ForDeath(__instance);
                LootRoller.OnDeath(__instance, modifiers);
                VanillaDropBoost.Begin(__instance, modifiers);
            }
            catch (Exception e)
            {
                // A loot failure must never stop the creature from dying (a rethrow would skip the game's own death).
                Log.Error($"creature drop roll failed: {e}");
            }
        }

        private static void Finalizer()
        {
            VanillaDropBoost.End();
            try
            {
                CorpseLoot.End();
            }
            catch (Exception e)
            {
                Log.Error($"creature drops failed to spawn: {e}");
            }
        }
    }
}
