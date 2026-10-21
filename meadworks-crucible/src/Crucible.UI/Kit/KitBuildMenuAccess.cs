// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace ExtraPotions.Meadworks.Crucible.UI
{
    /// <summary>
    /// KX06: the build menu's and the player's private members the tab service reads (Crucible builds against the
    /// game's assemblies as they ship, without a publicizer). A member the game renamed reads as null, and the
    /// service then refuses the menu (<see cref="KitBuildTabRules.MenuRefusal"/>) instead of throwing.
    /// </summary>
    internal static class KitBuildMenuAccess
    {
        private static readonly AccessTools.FieldRef<BuildUi, RectTransform> TabContainerRef = Field<BuildUi, RectTransform>("m_tabContainer");
        private static readonly AccessTools.FieldRef<BuildUi, TabHandler> TabHandlerRef = Field<BuildUi, TabHandler>("m_tabHandler");
        private static readonly AccessTools.FieldRef<BuildUi, List<IPieceList>> PieceListsRef = Field<BuildUi, List<IPieceList>>("m_pieceLists");
        private static readonly AccessTools.FieldRef<BuildUi, List<Button>> TabButtonsRef = Field<BuildUi, List<Button>>("m_tabButtons");
        private static readonly AccessTools.FieldRef<BuildUi, PieceTable> BuildToolRef = Field<BuildUi, PieceTable>("m_currentBuildTool");
        private static readonly AccessTools.FieldRef<BuildUi, int> CurrentListRef = Field<BuildUi, int>("m_currentPieceList");
        private static readonly AccessTools.FieldRef<BuildUi, BuildUiPieceButton> HoveredRef = Field<BuildUi, BuildUiPieceButton>("m_currentHoveredPieceButton");
        private static readonly AccessTools.FieldRef<Player, HashSet<string>> KnownRef = Field<Player, HashSet<string>>("m_knownRecipes");
        private static readonly MethodInfo UpdatePieceButtonsMethod = AccessTools.Method(typeof(BuildUi), "UpdatePieceButtons");
        private static readonly MethodInfo UpdateTagButtonsMethod = AccessTools.Method(typeof(BuildUi), "UpdateTagButtons");
        private static readonly MethodInfo UpdateAvailableMethod = AccessTools.Method(typeof(Player), "UpdateAvailablePiecesList");

        private static AccessTools.FieldRef<T, F> Field<T, F>(string name) where T : class
        {
            try
            {
                return AccessTools.Field(typeof(T), name) != null ? AccessTools.FieldRefAccess<T, F>(name) : null;
            }
            catch (System.Exception)
            {
                return null;
            }
        }

        public static RectTransform TabContainer(BuildUi ui) => ui != null && TabContainerRef != null ? TabContainerRef(ui) : null;
        public static TabHandler Handler(BuildUi ui) => ui != null && TabHandlerRef != null ? TabHandlerRef(ui) : null;
        public static List<IPieceList> PieceLists(BuildUi ui) => ui != null && PieceListsRef != null ? PieceListsRef(ui) : null;
        public static List<Button> TabButtons(BuildUi ui) => ui != null && TabButtonsRef != null ? TabButtonsRef(ui) : null;
        public static PieceTable BuildTool(BuildUi ui) => ui != null && BuildToolRef != null ? BuildToolRef(ui) : null;
        public static int CurrentList(BuildUi ui) => ui != null && CurrentListRef != null ? CurrentListRef(ui) : -1;

        public static void SetCurrentList(BuildUi ui, int list)
        {
            if (ui != null && CurrentListRef != null)
                CurrentListRef(ui) = list;
        }

        /// <summary>The menu's button under the mouse or chosen with a pad, while it is shown; else null.</summary>
        public static BuildUiPieceButton Hovered(BuildUi ui)
        {
            BuildUiPieceButton hovered = ui != null && HoveredRef != null ? HoveredRef(ui) : null;
            return hovered != null && hovered.gameObject.activeInHierarchy ? hovered : null;
        }

        public static HashSet<string> Known(Player player) => player != null && KnownRef != null ? KnownRef(player) : null;

        /// <summary>The menu's own complete set of members the service needs.</summary>
        public static bool Complete => TabContainerRef != null && TabHandlerRef != null && PieceListsRef != null && TabButtonsRef != null
            && BuildToolRef != null && CurrentListRef != null && UpdatePieceButtonsMethod != null && UpdateTagButtonsMethod != null;

        public static void UpdatePieceButtons(BuildUi ui) => UpdatePieceButtonsMethod?.Invoke(ui, null);

        public static void UpdateTagButtons(BuildUi ui) => UpdateTagButtonsMethod?.Invoke(ui, new object[] { true });

        public static void UpdateAvailable(Player player)
        {
            if (player != null)
                UpdateAvailableMethod?.Invoke(player, null);
        }
    }
}
