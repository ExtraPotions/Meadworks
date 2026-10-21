// Adapted from assets/references/source/ValheimMods-openkeep-3.0.0/GrindstoneSkills/GrindstoneSkills/Pickaxes/Core/OwnerHit.cs at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Copyright (C) 2026 Gerald Glasgow. SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Saga namespace, configuration, CS1 and ownership boundaries.
using HarmonyLib;

namespace ExtraPotions.Saga.Runtime.Adopted.Grindstone
{
    /// <summary>
    /// Hits being applied on a rock's ZDO owner: MineRock5.RPC_Damage, MineRock.RPC_Hit and Destructible.RPC_Damage run
    /// there for every hit, from any machine (synchronously inside the swing when the miner's own client owns the rock).
    /// <list type="bullet">
    /// <item><see cref="Depth"/> counts the handlers running, on every machine, so <see cref="MineHit"/> can tell a hit
    /// the owner re-sends (an intact deposit's owner damages the new fractured rock with the same hit) from a new swing.</item>
    /// <item>On a MineRock5, a Pickaxes hit opens a <see cref="Scope"/> (<see cref="Current"/>): the rock, the hit, its
    /// area and the <see cref="Miner"/>. <see cref="ChunkBreaks"/> notes whether the hit landed and gives the scope's miner
    /// to chunks that fall when the hit (or its splash) took their support. After the game applied the hit and ran its
    /// support check, a postfix hands the scope to <see cref="Splash.OnOwnerHit"/>, unless the rock is gone, the hit is
    /// marked as a later hit of its swing (<see cref="SplashOnce"/>), or a hit of the same outermost handler already
    /// splashed (an intact deposit's re-sends, nested in its Destructible.RPC_Damage).</item>
    /// </list>
    /// A hit without Pickaxes, or any hit while Pickaxes is off, opens no scope and is left to the game.
    /// </summary>
    public static class OwnerHit
    {
        /// <summary>A hit of the outermost handler running now has splashed.</summary>
        private static bool splashed;

        /// <summary>How many owner hit handlers are running on this machine right now (nested ones included).</summary>
        public static int Depth { get; private set; }

        /// <summary>The Pickaxes hit on a MineRock5 being applied on this machine right now; null outside one.</summary>
        public static Scope Current { get; private set; }

        [HarmonyPatch(typeof(MineRock5), nameof(MineRock5.RPC_Damage))]
        private static class ChunkDamage
        {
            [HarmonyPrefix]
            private static void Prefix(MineRock5 __instance, HitData hit, int hitAreaIndex, out Scope __state)
            {
                __state = Current;
                Enter();
                Current = PickSkill.Active ? HookGuard.Run("owner hit", static owner => Begin(owner.rock, owner.hit, owner.index), (rock: __instance, hit, index: hitAreaIndex), (Scope)null) : null;
            }

            [HarmonyPostfix]
            private static void Postfix(HitData hit)
            {
                Scope scope = Current;
                if (scope != null && scope.Hit == hit && MaySplash(scope))
                    HookGuard.Run("splash", () => Splash.OnOwnerHit(scope.Rock, scope.Hit, scope.Area, scope.Miner, scope.Landed));
            }

            [HarmonyFinalizer]
            private static void Finalizer(Scope __state)
            {
                Depth--;
                Current = __state;
            }
        }

        [HarmonyPatch(typeof(MineRock), nameof(MineRock.RPC_Hit))]
        private static class OldChunkDamage
        {
            [HarmonyPrefix]
            private static void Prefix() => Enter();

            [HarmonyFinalizer]
            private static void Finalizer() => Depth--;
        }

        [HarmonyPatch(typeof(Destructible), nameof(Destructible.RPC_Damage))]
        private static class PieceDamage
        {
            [HarmonyPrefix]
            private static void Prefix() => Enter();

            [HarmonyFinalizer]
            private static void Finalizer() => Depth--;
        }

        /// <summary>Called by <see cref="ChunkBreaks"/> after the game applied <paramref name="hit"/> to its chunk.</summary>
        public static void NoteLanded(HitData hit, bool landed)
        {
            if (Current != null && Current.Hit == hit)
                Current.Landed = landed;
        }

        /// <summary>A handler starts; an outermost one starts with no splash yet.</summary>
        private static void Enter()
        {
            if (Depth == 0)
                splashed = false;
            Depth++;
        }

        /// <summary>The scope's hit is the one to splash: see the class summary. A landed hit uses up the handler's splash.</summary>
        private static bool MaySplash(Scope scope)
        {
            if (!PickSkill.Active || !scope.Rock.IsValid || !SplashOnce.Carries(scope.Hit) || splashed)
                return false;
            splashed = scope.Landed;
            return true;
        }

        private static Scope Begin(MineRock5 chunks, HitData hit, int area)
        {
            Miner miner = Miner.FromHit(hit);
            Rock rock = miner != null ? Rock.Of(chunks) : null;
            if (rock == null || !rock.IsOwner)
                return null;
            return new Scope { Rock = rock, Hit = hit, Area = area, Miner = miner };
        }

        /// <summary>A Pickaxes hit on a MineRock5 chunk, while its owner applies it.</summary>
        public sealed class Scope
        {
            public Rock Rock { get; set; }

            /// <summary>The hit as the owner received it; the game applies the rock's resistances to it in place.</summary>
            public HitData Hit { get; set; }

            /// <summary>The chunk's area index.</summary>
            public int Area { get; set; }

            public Miner Miner { get; set; }

            /// <summary>The hit passed the tool tier check and took health off its chunk.</summary>
            public bool Landed { get; set; }
        }
    }
}
