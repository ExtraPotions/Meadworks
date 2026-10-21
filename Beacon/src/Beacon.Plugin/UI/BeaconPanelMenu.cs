// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: the Join Game attachment follows Cairn's owned native-navigation baseline.
using System.Collections.Generic;
using ExtraPotions.Beacon.Servers;
using ExtraPotions.Meadworks.Crucible.UI;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace ExtraPotions.Beacon.Runtime
{
    internal static class BeaconPanelMenu
    {
        /// <summary>Space kept between the Beacon button and the game's Back and Join buttons beside it.</summary>
        const float Gap = 8f;
        static Button? button, template;
        static FejdStartup? owner;
        static Navigation previous;
        public static void Tick()
        {
            var startup = FejdStartup.instance;
            if (startup == null || startup.m_serverListPanel == null) { Reset(); return; }
            if (owner != startup || button == null)
            {
                Reset();
                var gui = startup.m_serverListPanel.GetComponentInChildren<ServerListGui>(true);
                template = gui?.m_addServerButton;
                if (gui == null || template == null) return;
                var parent = (RectTransform)template.transform.parent;
                button = GameButtons.Make(template, parent, "Meadworks_Beacon", "Beacon", BeaconPanel.Open);
                if (button == null) return;
                BeaconPanel.Tip(button.gameObject, "servers");
                owner = startup;
                Place(parent, (RectTransform)button.transform, (RectTransform)template.transform, gui.m_joinGameButton);
                previous = template.navigation;
                var nav = previous; nav.mode = Navigation.Mode.Explicit; nav.selectOnRight = button; template.navigation = nav;
                button.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnLeft = template, selectOnUp = template, selectOnRight = previous.selectOnRight };
            }
        }

        /// <summary>
        /// Inside the Join Game frame, in the free space of its bottom row between the game's Back and Join buttons; no native
        /// control moves or resizes. The former place, one button right of Add server, lay outside the frame (playtest 2026-10-20 #17).
        /// </summary>
        static void Place(RectTransform parent, RectTransform rect, RectTransform add, Button? join)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            rect.localScale = Vector3.one;
            var joinRect = join != null ? (RectTransform)join.transform : null;
            Rect? back = null;
            if (joinRect != null && joinRect.parent == parent)
            {
                var joinBox = Box(parent, joinRect);
                // The nearest native button left of Join that shares its row (the game's Back button).
                foreach (Transform child in parent)
                {
                    if (child == rect || child == joinRect || !child.gameObject.activeSelf || child.GetComponent<Button>() == null) continue;
                    var box = Box(parent, (RectTransform)child);
                    if (box.xMax > joinBox.xMin || box.yMax < joinBox.yMin || box.yMin > joinBox.yMax) continue;
                    if (back == null || box.xMax > back.Value.xMax) back = box;
                }
                if (back != null)
                {
                    var room = joinBox.xMin - back.Value.xMax - 2 * Gap;
                    var height = Mathf.Min(add.rect.height, back.Value.height);
                    if (room >= 60f)
                    {
                        rect.sizeDelta = new Vector2(Mathf.Min(add.rect.width, room), height);
                        rect.anchoredPosition = new Vector2((back.Value.xMax + joinBox.xMin) * .5f, back.Value.center.y) - parent.rect.center;
                        return;
                    }
                }
            }
            // No room found in this layout: above Add server's row, still inside the frame and clear of the native controls.
            var addBox = Box(parent, add);
            rect.sizeDelta = add.rect.size;
            rect.anchoredPosition = new Vector2(addBox.center.x, Mathf.Min(addBox.yMax + Gap + add.rect.height * .5f, parent.rect.yMax - add.rect.height * .5f)) - parent.rect.center;
        }

        /// <summary>A direct child's rectangle in its parent's local units.</summary>
        static Rect Box(RectTransform parent, RectTransform child)
        {
            var local = child.rect;
            var at = (Vector2)child.localPosition;
            return new Rect(at + local.min, local.size);
        }

        public static void Reset()
        {
            if (template != null && button != null && template.navigation.selectOnRight == button)
            { var nav = template.navigation; nav.selectOnRight = previous.selectOnRight; template.navigation = nav; }
            if (button != null) { button.gameObject.SetActive(false); UnityEngine.Object.Destroy(button.gameObject); }
            button = template = null; owner = null;
        }
    }

    /// <summary>
    /// The Beacon panel opens over the Join Game list, and the game keeps reading keys for that list underneath it: R refreshes,
    /// the arrows change the selection and W/S move a favorite. Typing a server name into Beacon's fields moved favorites, and S
    /// with no favorite selected threw the game's ArgumentOutOfRangeException in LocalServerList.Swap (playtest 2026-10-20 log).
    /// </summary>
    [HarmonyPatch(typeof(ServerListGui))]
    internal static class BeaconServerListPatches
    {
        [HarmonyPatch("UpdateInput"), HarmonyPrefix]
        static bool Input() => BeaconPanel.Window?.IsOpen != true;

        /// <summary>
        /// The game moves a favorite by its row in the filtered list but swaps by that number in the full favorites list:
        /// with nothing selected (its S key passes -1) the swap throws, and with a filter typed it swaps the wrong pair.
        /// The selected row and its visible neighbor are found in the full list by join data and swapped there; entries the
        /// filter hides keep their places. A move without a valid pair is skipped. Unfiltered moves run the game's own code.
        /// </summary>
        [HarmonyPatch(nameof(ServerListGui.OnMoveServerUpButton)), HarmonyPrefix]
        static bool Up(ServerListGui __instance) => Move(__instance, -1);

        [HarmonyPatch(nameof(ServerListGui.OnMoveServerDownButton)), HarmonyPrefix]
        static bool Down(ServerListGui __instance) => Move(__instance, 1);

        static readonly List<int> VisibleToFull = new List<int>();

        static bool Move(ServerListGui gui, int offset)
        {
            try
            {
                var favorites = gui.m_favoriteServersList; var visible = gui.CurrentServerListFiltered;
                VisibleToFull.Clear();
                foreach (var row in visible) VisibleToFull.Add(favorites.TryGetIndexOf(row.m_joinData, out var index) ? index : -1);
                var selected = gui.GetSelectedServer();
                if (!FavoriteMove.Resolve(VisibleToFull, favorites.Count, selected, offset, out var from, out var to)) return false;
                if (from == selected && to == selected + offset) return true;
                favorites.Swap(from, to);
                gui.m_filteredListOutdated = true;
                gui.UpdateServerListGui(centerSelection: true);
                return false;
            }
            catch { return false; }
            finally { VisibleToFull.Clear(); }
        }
    }
}
