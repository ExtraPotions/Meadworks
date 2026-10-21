// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
// TI06 hammer cost adapter.
// Native Player.HaveRequirements/ConsumeResources checked in Valheim's own current assembly on 2026-10-08.
using System;
using HarmonyLib;
namespace ExtraPotions.Timber.Runtime
{
    public static class FreeBuildRuntime
    {
        static readonly Piece.Requirement[] Empty = Array.Empty<Piece.Requirement>();
        static bool Free(Player player, Piece piece)
        {
            if (!Plugin.FreeBuild || player == null || player != Player.m_localPlayer || piece == null ||
                !player.IsRecipeKnown(piece.m_name)) return false;
            var hammer = HammerTable();
            return hammer != null && hammer.m_pieces.Contains(piece.gameObject);
        }
        static ObjectDB? foundIn;
        static PieceTable? hammer;
        /// <summary>The hammer's build table, or null before the item database is up (looked up once per database).</summary>
        static PieceTable? HammerTable()
        {
            ObjectDB db = ObjectDB.instance;
            if (db == null) return null;
            if (db != foundIn || hammer == null)
            {
                var prefab = db.GetItemPrefab("Hammer");
                hammer = prefab != null ? prefab.GetComponent<ItemDrop>()?.m_itemData.m_shared.m_buildPieces : null;
                foundIn = hammer != null ? db : null;
            }
            return hammer;
        }
        public struct RequirementState { public Piece.Requirement[]? Before; public bool Changed; }
        [HarmonyPatch(typeof(Player), nameof(Player.HaveRequirements), new[] { typeof(Piece), typeof(Player.RequirementMode) })]
        static class Requirements
        {
            [HarmonyPrefix, HarmonyPriority(Priority.First)]
            static void Prefix(Player __instance, Piece piece, Player.RequirementMode mode, out RequirementState __state)
            {
                __state = default;
                if (mode == Player.RequirementMode.IsKnown || !Free(__instance,piece)) return;
                __state.Before=piece.m_resources; __state.Changed=true;
                piece.m_resources=Empty;
            }
            [HarmonyFinalizer]
            static Exception? Finalizer(Piece piece, RequirementState __state, Exception? __exception)
            {
                if (__state.Changed && piece != null && ReferenceEquals(piece.m_resources,Empty)) piece.m_resources=__state.Before!;
                return __exception;
            }
        }
        [HarmonyPatch(typeof(Player), nameof(Player.ConsumeResources))]
        static class Payment
        {
            [HarmonyPrefix]
            static bool Prefix(Player __instance, Piece.Requirement[] requirements, int qualityLevel)
            {
                if(qualityLevel != 0) return true;
                Piece selected=__instance.GetSelectedPiece();
                return !Free(__instance,selected) || !ReferenceEquals(requirements,selected.m_resources);
            }
        }
    }
}
