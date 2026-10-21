// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
using ExtraPotions.Meadworks.Crucible.UI;
using HarmonyLib;
using Mantle.Consume;
using UnityEngine;

namespace Mantle.Panels
{
    /// <summary>
    /// The user, 2026-10-12 19:15: while any MeadWorks window is open (<see cref="KitWindows.AnyOpen"/>) the HUD column
    /// beside the minimap (armor, carry, the globe and every square other products put in it) and the Food and Mead bar
    /// hide, as the game hides its HUD for the Compendium. A CanvasGroup fades them out, so the code that shows or hides
    /// each square keeps its own state; they come back as they were when the last window closes. Local only.
    /// </summary>
    [HarmonyPatch(typeof(Hud), nameof(Hud.Update))]
    public static class KitWindowHide
    {
        [HarmonyPostfix]
        [HarmonyPriority(Priority.Last)]
        public static void Postfix(Hud __instance)
        {
            bool hide = KitWindows.AnyOpen;
            GameObject small = Minimap.instance != null ? Minimap.instance.m_smallRoot : null;
            Hide(small != null ? small.transform.Find(ExtraPotions.Mantle.Embedded.PlateColumn.HudContainer.Name) : null, hide);
            Transform root = __instance.m_healthPanel != null ? __instance.m_healthPanel.parent : null;
            Hide(root != null ? root.Find(ConsumeBar.Name) : null, hide);
        }

        private static void Hide(Transform part, bool hide)
        {
            if (part == null)
                return;
            CanvasGroup group = part.GetComponent<CanvasGroup>();
            if (group == null)
            {
                if (!hide)
                    return;
                group = part.gameObject.AddComponent<CanvasGroup>();
            }
            group.alpha = hide ? 0f : 1f;
            group.blocksRaycasts = !hide;
            group.interactable = !hide;
        }
    }
}
