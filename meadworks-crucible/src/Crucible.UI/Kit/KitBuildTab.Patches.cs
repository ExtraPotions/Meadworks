// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
// KX06 game hooks; the set follows OpenKeep 3.0.0 BlueprintTabPatches and BlueprintPatches (see KitBuildTab.cs),
// rebuilt as guards that leave a product's own prefixes in charge. PatchGuard style: a failing hook leaves the game alone.
using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;

namespace ExtraPotions.Meadworks.Crucible.UI
{
    [HarmonyPatch]
    internal static class KitBuildTabPatches
    {
        private static void Guard(string what, Action action)
        {
            try
            {
                action();
            }
            catch (Exception e)
            {
                CrucibleUiPlugin.Log?.Error($"Crucible.UI: build tab {what} failed: {e}");
            }
        }

        /// <summary>A new build menu: every registered tab joins it.</summary>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(BuildUi), "Awake")]
        private static void MenuAwake(BuildUi __instance) => Guard("install", () => KitBuildTab.MenuAwake(__instance));

        [HarmonyPostfix]
        [HarmonyPatch(typeof(BuildUi), "OnDestroy")]
        private static void MenuDestroyed(BuildUi __instance) => Guard("menu teardown", () => KitBuildTab.MenuDestroyed(__instance));

        [HarmonyPostfix]
        [HarmonyPatch(typeof(BuildUi), "UpdatePieceButtons")]
        private static void FolderDrags(BuildUi __instance) => Guard("folder drag handles", () => KitBuildTab.SyncFolderButtons(__instance));

        /// <summary>The menu's window fits its rows (the cultivator's short list), full height back for a longer one.</summary>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(BuildUi), "UpdatePieceButtons")]
        private static void FitRows(BuildUi __instance) => Guard("row fit", () => KitBuildFit.Fit(__instance));

        /// <summary>Views apply before the menu picks its list (a reopened menu never lands on a hidden tab) and once it knows its tool.</summary>
        [HarmonyPrefix]
        [HarmonyPatch(typeof(BuildUi), nameof(BuildUi.OpenBuildMenu))]
        private static void BeforeOpen() => Guard("view", KitBuildTab.TickViews);

        [HarmonyPostfix]
        [HarmonyPatch(typeof(BuildUi), nameof(BuildUi.OpenBuildMenu))]
        private static void AfterOpen()
        {
            Guard("view", KitBuildTab.TickViews);
            Guard("open", KitBuildTab.MenuOpened);
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(BuildUi), "ConfigureButtonNavigation")]
        private static void FolderNavigation(BuildUi __instance) => Guard("folder navigation", () => KitBuildFolders.NativeFolderNavigation(__instance));

        /// <summary>A click on an entry is reported to its tab; an action entry's click selects nothing and leaves the menu open.</summary>
        [HarmonyPrefix]
        [HarmonyPatch(typeof(BuildUi), nameof(BuildUi.OnSelectPiece))]
        private static bool EntryClick(Piece piece) => Ask(() => KitBuildTab.PieceClicked(piece));

        /// <summary>Entries are never favourites (middle click) nor recent pieces.</summary>
        [HarmonyPrefix]
        [HarmonyPatch(typeof(BuildUi), nameof(BuildUi.PressedFavoriteButton))]
        private static bool NoFavourite(BuildUiPieceButton btn) => Ask(() => btn == null || !KitBuildTab.IsAnyEntry(btn.Piece));

        [HarmonyPrefix]
        [HarmonyPatch(typeof(BuildUi), nameof(BuildUi.AddRecentPiece))]
        private static bool NoRecent(Piece piece) => Ask(() => !KitBuildTab.IsAnyEntry(piece));

        /// <summary>Entries become known without the game's "new piece" message.</summary>
        [HarmonyPrefix]
        [HarmonyPatch(typeof(Player), "AddKnownPiece")]
        private static bool LearnQuietly(Player __instance, Piece piece) => Ask(() => KitBuildTab.LearnQuietly(__instance, piece));

        /// <summary>
        /// The game never places an entry. Runs last: when a product's own prefix already took the click
        /// (<c>__runOriginal</c> false) its result stands; otherwise the click places nothing.
        /// </summary>
        [HarmonyPrefix]
        [HarmonyPriority(Priority.Last)]
        [HarmonyPatch(typeof(Player), nameof(Player.TryPlacePiece))]
        private static bool NoPlace(Piece piece, ref bool __result, bool __runOriginal)
        {
            if (!__runOriginal)
                return false;
            if (!Ask(() => !KitBuildTab.IsAnyEntry(piece)))
            {
                __result = false;
                return false;
            }
            return true;
        }

        /// <summary>While an entry is selected, the hammer's remove button takes nothing down for the local player.</summary>
        [HarmonyPrefix]
        [HarmonyPriority(Priority.Last)]
        [HarmonyPatch(typeof(Player), "RemovePiece")]
        private static bool NoRemove(Player __instance, ref bool __result, bool __runOriginal)
        {
            if (!__runOriginal)
                return false;
            if (!Ask(() => __instance != Player.m_localPlayer || !KitBuildTab.IsAnyEntry(__instance.GetSelectedPiece())))
            {
                __result = false;
                return false;
            }
            return true;
        }

        /// <summary>Entry names stay out of the character file: withdrawn for the save, restored after.</summary>
        [HarmonyPrefix]
        [HarmonyPatch(typeof(Player), nameof(Player.Save))]
        private static void BeforeSave(Player __instance, out List<(KitBuildTab, IReadOnlyList<string>)> __state)
        {
            List<(KitBuildTab, IReadOnlyList<string>)> withdrawn = null;
            Guard("save", () => withdrawn = KitBuildTab.BeforeSave(__instance));
            __state = withdrawn;
        }

        [HarmonyFinalizer]
        [HarmonyPatch(typeof(Player), nameof(Player.Save))]
        private static void AfterSave(Player __instance, List<(KitBuildTab, IReadOnlyList<string>)> __state) =>
            Guard("save", () => KitBuildTab.AfterSave(__instance, __state));

        // OpenKeep TabCtrlPatch: picking with Ctrl must not also toggle the local player's sneak.
        [HarmonyPrefix]
        [HarmonyPatch(typeof(Player), nameof(Player.SetControls))]
        private static void GroupControl(Player __instance, ref bool crouch) {
            if (crouch && __instance == Player.m_localPlayer && KitBuildTab.GroupControlActive()) crouch = false;
        }

        /// <summary>A failing question answers "leave the game alone".</summary>
        private static bool Ask(Func<bool> question)
        {
            try
            {
                return question();
            }
            catch (Exception e)
            {
                CrucibleUiPlugin.Log?.Error($"Crucible.UI: build tab guard failed: {e}");
                return true;
            }
        }
    }

    /// <summary>
    /// The game's own four lists (By Usage, By Material, Recent, Favorites) never list an installed tab's entries. A tab the
    /// menu refused leaves its entries in those lists, so they stay reachable (OpenKeep's fallback).
    /// </summary>
    [HarmonyPatch]
    internal static class KitBuildTabListFilter
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(ByUsagePieceList), nameof(ByUsagePieceList.GetAvailablePiecesWithTag));
            yield return AccessTools.Method(typeof(ByMaterialPieceList), nameof(ByMaterialPieceList.GetAvailablePiecesWithTag));
            yield return AccessTools.Method(typeof(RecentPieceList), nameof(RecentPieceList.GetAvailablePiecesWithTag));
            yield return AccessTools.Method(typeof(FavoritePieceList), nameof(FavoritePieceList.GetAvailablePiecesWithTag));
        }

        [HarmonyPostfix]
        private static void Postfix(IList<Piece> resultOut)
        {
            try
            {
                for (int i = resultOut.Count - 1; i >= 0; i--)
                    if (KitBuildTab.IsInstalledEntry(resultOut[i]))
                        resultOut.RemoveAt(i);
            }
            catch (Exception e)
            {
                CrucibleUiPlugin.Log?.Error($"Crucible.UI: build tab list filter failed: {e}");
            }
        }
    }
}
