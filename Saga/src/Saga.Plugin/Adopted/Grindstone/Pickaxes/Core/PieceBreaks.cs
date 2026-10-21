// Adapted from assets/references/source/ValheimMods-openkeep-3.0.0/GrindstoneSkills/GrindstoneSkills/Pickaxes/Core/PieceBreaks.cs at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Copyright (C) 2026 Gerald Glasgow. SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Saga namespace, configuration, CS1 and ownership boundaries.
using HarmonyLib;
using UnityEngine;

namespace ExtraPotions.Saga.Runtime.Adopted.Grindstone
{
    /// <summary>
    /// Single-piece rocks (tin, obsidian, small boulders) breaking, on the rock's ZDO owner. Destructible.RPC_Damage
    /// calls Destructible.Destroy(hit) there when the health reaches 0: it marks the ZDO cheated, spawns
    /// m_spawnWhenDestroyed, runs m_onDestroyed (DropOnDestroyed drops its table there) and destroys the ZDO. A prefix
    /// reads a <see cref="RockBreak"/> first, for a Pickaxes hit on a <see cref="Rock"/> that drops items itself; a
    /// postfix hands it to <see cref="MineBreak"/>. An intact deposit (1 health, no drops of its own, turning into its
    /// "_frac" rock) is no break: its first hit goes on to damage the fractured rock, whose chunks break through
    /// <see cref="ChunkBreaks"/>. Destroy without a hit (a time-out, the harvest of a plant) is left to the game.
    /// </summary>
    public static class PieceBreaks
    {
        [HarmonyPatch(typeof(Destructible), nameof(Destructible.Destroy), new[] { typeof(HitData) })]
        private static class Break
        {
            [HarmonyPrefix]
            private static void Prefix(Destructible __instance, HitData hit, out RockBreak __state) =>
                __state = PickSkill.Active && hit != null ? HookGuard.Run("piece break", static piece => Begin(piece.destructible, piece.hit), (destructible: __instance, hit), (RockBreak)null) : null;

            [HarmonyPostfix]
            private static void Postfix(RockBreak __state)
            {
                if (__state != null)
                    MineBreak.Dispatch(__state);
            }
        }

        private static RockBreak Begin(Destructible piece, HitData hit)
        {
            Miner miner = Miner.FromHit(hit);
            Rock rock = miner != null ? Rock.Of(piece) : null;
            DropTable drops = rock != null && rock.IsOwner ? RockPieces.Drops(piece) : null;
            if (drops == null)
                return null;
            DropOnDestroyed spawner = piece.GetComponent<DropOnDestroyed>();
            return new RockBreak
            {
                Rock = rock, RockId = rock.Id, Chunk = -1, Position = DropBase(piece, spawner), StackStep = spawner.m_spawnYStep,
                Drops = drops, Hit = hit, Miner = miner, Cause = BreakCause.Hit, Cheated = Cheated(rock, miner),
            };
        }

        /// <summary>Where DropOnDestroyed starts its stack: the piece's position, lifted onto the ground if below it, plus m_spawnYOffset.</summary>
        private static Vector3 DropBase(Destructible piece, DropOnDestroyed spawner)
        {
            Vector3 position = piece.transform.position;
            float ground = ZoneSystem.instance != null ? ZoneSystem.instance.GetGroundHeight(position) : position.y;
            if (position.y < ground)
                position.y = ground + 0.1f;
            return position + Vector3.up * spawner.m_spawnYOffset;
        }

        /// <summary>As Destructible.Destroy and DropOnDestroyed decide it: the ZDO's cheated mark, or a cheated miner, unless cheat checks are bypassed.</summary>
        private static bool Cheated(Rock rock, Miner miner)
        {
            if (PlayerProfile.s_bypassCheatChecks)
                return false;
            return miner.Cheated || rock.View.GetZDO().GetBool(ZDOVars.s_cheated);
        }
    }
}
