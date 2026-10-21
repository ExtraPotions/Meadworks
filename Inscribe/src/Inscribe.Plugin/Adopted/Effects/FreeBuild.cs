// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
using HarmonyLib;

namespace ExtraPotions.Inscribe.Adopted.Effects
{
    /// <summary>
    /// Masterbuilder (<c>free_build</c>): while this hammer or hoe is in hand, building ignores the crafting-station
    /// requirement, as the game's own "no workbench" world setting does - for this player and this tool only. Item-local:
    /// the flag on the tool in the right hand (where the game keeps the build tool).
    /// <para>
    /// The game asks <c>Player.HaveRequirements(piece, CanBuild)</c> before placing a piece (and to colour the build
    /// menu); a missing station makes it false. With the tool in hand, the piece's station requirement is set aside for
    /// that one call (a first prefix takes it off, a finalizer puts it back), so the check runs once and the materials,
    /// the DLC and every other mod's part of it (OpenKeep's materials from chests) still count. Removing a piece skips
    /// the station check the same way. The builder's own client: placement and removal are decided there.
    /// </para>
    /// </summary>
    internal static class FreeBuild
    {
        /// <summary>Whether this player is the local player with a Masterbuilder tool in hand.</summary>
        public static bool Active(Player player)
        {
            if (!ReferenceEquals(player, Player.m_localPlayer))
            {
                return false;
            }
            ItemLocalSums? sums = ItemLocalCache.Get(player.RightItem);
            return sums != null && sums.Get(EffectKind.FreeBuild) > 0f;
        }
    }

    [HarmonyPatch]
    internal static class FreeBuildPatches
    {
        [HarmonyPrefix, HarmonyPriority(Priority.First)]
        [HarmonyPatch(typeof(Player), nameof(Player.HaveRequirements), new[] { typeof(Piece), typeof(Player.RequirementMode) })]
        private static void SetStationAside(Player __instance, Piece piece, Player.RequirementMode mode, out CraftingStation? __state)
        {
            __state = null;
            if (mode != Player.RequirementMode.CanBuild || piece == null || piece.m_craftingStation == null)
            {
                return;
            }
            if (FreeBuild.Active(__instance))
            {
                __state = piece.m_craftingStation;
                piece.m_craftingStation = null;
            }
        }

        /// <summary>Puts the station back after every postfix, also when the check threw.</summary>
        [HarmonyFinalizer]
        [HarmonyPatch(typeof(Player), nameof(Player.HaveRequirements), new[] { typeof(Piece), typeof(Player.RequirementMode) })]
        private static void PutStationBack(Piece piece, CraftingStation? __state)
        {
            if (__state != null && piece != null)
            {
                piece.m_craftingStation = __state;
            }
        }

        // The removal check is only the station check: with the tool in hand, removing is allowed.
        [HarmonyPrefix]
        [HarmonyPatch(typeof(Player), nameof(Player.CheckCanRemovePiece))]
        private static bool Removal(Player __instance, ref bool __result)
        {
            if (!FreeBuild.Active(__instance))
            {
                return true;
            }
            __result = true;
            return false;
        }
    }
}
