// Adapted from assets/references/source/ValheimMods-openkeep-3.0.0/GrindstoneSkills/GrindstoneSkills/Pickaxes/Core/ChunkBreaks.cs at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Copyright (C) 2026 Gerald Glasgow. SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Saga namespace, configuration, CS1 and ownership boundaries.
using HarmonyLib;
using UnityEngine;

namespace ExtraPotions.Saga.Runtime.Adopted.Grindstone
{
    /// <summary>
    /// Chunks of multi-chunk rocks breaking, on the rock's ZDO owner.
    /// <list type="bullet">
    /// <item><b>MineRock5.DamageArea(area, hit)</b> applies damage to one chunk and returns true when that chunk broke
    /// (it has already spawned the chunk's drops, and destroyed the rock when it was the last). The game calls it from
    /// RPC_Damage (a hit), from CheckSupport (a structural hit with no attacker, for chunks left unsupported), and
    /// <see cref="Splash"/> calls it directly. A prefix reads the chunk before the damage; a postfix tells
    /// <see cref="OwnerHit"/> whether the hit landed (took health off) and, on a break, hands a <see cref="RockBreak"/>
    /// to <see cref="MineBreak"/>. The miner is the hit's (a Pickaxes hit), else the owner hit being handled
    /// (<see cref="OwnerHit.Current"/>): a collapse or a splash inside it. Breaks with neither are left to the game.</item>
    /// <item><b>MineRock.RPC_Hit(hit, area)</b> does the same for the older rocks, without support checks; a chunk broke
    /// when its "Health" + area fell to 0 or the rock was destroyed (only after a break).</item>
    /// </list>
    /// </summary>
    public static class ChunkBreaks
    {
        [HarmonyPatch(typeof(MineRock5), nameof(MineRock5.DamageArea))]
        private static class AreaDamage
        {
            [HarmonyPrefix]
            private static void Prefix(MineRock5 __instance, int hitAreaIndex, HitData hit, out Pending __state) =>
                __state = PickSkill.Active ? HookGuard.Run("chunk damage", static area => BeginArea(area.rock, area.index, area.hit), (rock: __instance, index: hitAreaIndex, hit), (Pending)null) : null;

            [HarmonyPostfix]
            private static void Postfix(MineRock5 __instance, bool __result, Pending __state)
            {
                if (__state != null)
                    HookGuard.Run("chunk break", () => EndArea(__instance, __result, __state));
            }
        }

        [HarmonyPatch(typeof(MineRock), nameof(MineRock.RPC_Hit))]
        private static class OldChunkHit
        {
            [HarmonyPrefix]
            private static void Prefix(MineRock __instance, HitData hit, int hitAreaIndex, out Pending __state) =>
                __state = PickSkill.Active ? HookGuard.Run("old chunk damage", static area => BeginOld(area.rock, area.index, area.hit), (rock: __instance, index: hitAreaIndex, hit), (Pending)null) : null;

            [HarmonyPostfix]
            private static void Postfix(MineRock __instance, Pending __state)
            {
                if (__state != null)
                    HookGuard.Run("old chunk break", () => EndOld(__instance, __state));
            }
        }

        private static Pending BeginArea(MineRock5 chunks, int area, HitData hit)
        {
            Rock rock = Rock.Of(chunks);
            Miner miner = Miner.FromHit(hit) ?? OwnerHit.Current?.Miner;
            if (rock == null || miner == null || hit == null || !rock.IsOwner)
                return null;
            chunks.LoadHealth();
            float before = RockChunks.Health(rock, area);
            if (before <= 0f)
                return null;
            Collider collider = RockChunks.ColliderOf(rock, area);
            Vector3 spot = chunks.m_hitEffectAreaCenter && collider != null ? collider.bounds.center : hit.m_point;
            return new Pending(rock, area, hit, miner, before, spot, CauseOf(hit));
        }

        private static void EndArea(MineRock5 chunks, bool broke, Pending pending)
        {
            float after = chunks.m_hitAreas[pending.Area].m_health;
            OwnerHit.NoteLanded(pending.Hit, after < pending.Before);
            if (broke)
                MineBreak.Dispatch(pending.ToBreak(chunks.m_dropItems));
        }

        private static Pending BeginOld(MineRock chunks, int area, HitData hit)
        {
            Rock rock = Rock.Of(chunks);
            Miner miner = Miner.FromHit(hit);
            if (rock == null || miner == null || !rock.IsOwner)
                return null;
            float before = RockChunks.Health(rock, area);
            if (before <= 0f)
                return null;
            return new Pending(rock, area, hit, miner, before, hit.m_point - hit.m_dir * 0.2f, BreakCause.Hit);
        }

        private static void EndOld(MineRock chunks, Pending pending)
        {
            bool broke = !pending.Rock.IsValid || RockChunks.Health(pending.Rock, pending.Area) <= 0f;
            if (broke)
                MineBreak.Dispatch(pending.ToBreak(chunks.m_dropItems));
        }

        /// <summary>The owner hit being handled broke its own chunk; another Pickaxes hit is a splash; anything else fell.</summary>
        private static BreakCause CauseOf(HitData hit)
        {
            if (OwnerHit.Current != null && OwnerHit.Current.Hit == hit)
                return BreakCause.Hit;
            return hit.m_skill == PickSkill.Skill ? BreakCause.Splash : BreakCause.Collapse;
        }

        /// <summary>A chunk about to take damage, read before the game can destroy the rock.</summary>
        public sealed class Pending
        {
            public Pending(Rock rock, int area, HitData hit, Miner miner, float before, Vector3 spot, BreakCause cause)
            {
                Rock = rock;
                Id = rock.Id;
                Area = area;
                Hit = hit;
                Miner = miner;
                Before = before;
                Spot = spot;
                Cause = cause;
            }

            public Rock Rock { get; }
            public ZDOID Id { get; }
            public int Area { get; }
            public HitData Hit { get; }
            public Miner Miner { get; }
            public float Before { get; }
            public Vector3 Spot { get; }
            public BreakCause Cause { get; }

            public RockBreak ToBreak(DropTable drops) => new RockBreak
            {
                Rock = Rock, RockId = Id, Chunk = Area, Position = Spot, Drops = drops, Hit = Hit, Miner = Miner,
                Cause = Cause, Cheated = Miner.Cheated,
            };
        }
    }
}
