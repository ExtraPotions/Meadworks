// Adapted from assets/references/source/ValheimMods-openkeep-3.0.0/GrindstoneSkills/GrindstoneSkills/Pickaxes/Core/MineHit.cs at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Copyright (C) 2026 Gerald Glasgow. SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Saga namespace, configuration, CS1 and ownership boundaries.
using HarmonyLib;

namespace ExtraPotions.Saga.Runtime.Adopted.Grindstone
{
    /// <summary>
    /// The local player's pickaxe hit on a rock, on the miner's own client, before the game sends it to the rock's
    /// owner. MineRock5.Damage, MineRock.Damage and Destructible.Damage run on the hitting machine: they find the chunk
    /// (area index) and send the hit by RPC (handled at once when this client owns the rock). The prefixes here run
    /// first, for a hit that
    /// <list type="bullet">
    /// <item>is a Pickaxes hit (m_skill) by the local player, inside that player's <see cref="MineSwing"/>,</item>
    /// <item>lands on a <see cref="Rock"/>,</item>
    /// <item>and is not re-sent from inside an owner's handler (<see cref="OwnerHit.Depth"/>): an intact deposit's owner
    /// damages the new fractured rock with the same hit, which is no new swing.</item>
    /// </list>
    /// Each such hit is recorded in the swing, marked when it is not the swing's first on a rock that can splash
    /// (<see cref="SplashOnce"/>), handed to experience (<see cref="MineXp.OnRockHit"/>, discovery), and, on a MineRock5
    /// chunk hit by a single collider (every melee hit), to <see cref="Seams.OnLocalHit"/>, which may change the hit's
    /// damage before it goes out. Anything else is left to the game.
    /// </summary>
    public static class MineHit
    {
        [HarmonyPatch(typeof(MineRock5), nameof(MineRock5.Damage))]
        private static class ChunkHit
        {
            [HarmonyPrefix]
            private static void Prefix(MineRock5 __instance, HitData hit) => Local(__instance, hit);
        }

        [HarmonyPatch(typeof(MineRock), nameof(MineRock.Damage))]
        private static class OldChunkHit
        {
            [HarmonyPrefix]
            private static void Prefix(MineRock __instance, HitData hit) => Local(__instance, hit);
        }

        [HarmonyPatch(typeof(Destructible), nameof(Destructible.Damage))]
        private static class PieceHit
        {
            [HarmonyPrefix]
            private static void Prefix(Destructible __instance, HitData hit) => Local(__instance, hit);
        }

        /// <summary>Whether a hit is the local player's own pickaxe hit of the running swing (cheap checks only).</summary>
        public static bool IsLocalPickaxeHit(HitData hit)
        {
            Player player = Player.m_localPlayer;
            return hit != null && hit.m_skill == PickSkill.Skill && MineSwing.Active && OwnerHit.Depth == 0
                && player != null && hit.m_attacker == player.GetZDOID();
        }

        private static void Local(UnityEngine.Component target, HitData hit)
        {
            if (PickSkill.Active && IsLocalPickaxeHit(hit))
                HookGuard.Run("mine hit", static mine => Handle(mine.target, mine.hit), (target, hit));
        }

        private static void Handle(UnityEngine.Component target, HitData hit)
        {
            Rock rock = Rock.Of(target);
            if (rock == null || !rock.IsValid)
                return;
            hit.m_skillLevel = PickSkill.Local();
            MineSwing.Record(rock);
            SplashOnce.MarkLocal(rock, hit);
            HookGuard.Run("mine discovery", () => MineXp.OnRockHit(rock, hit));
            int area = rock.Chunks5 != null && hit.m_radius <= 0f ? RockChunks.AreaOf(rock, hit.m_hitCollider) : -1;
            if (area >= 0)
                HookGuard.Run("seams", () => Seams.OnLocalHit(rock, hit, area));
        }
    }
}
