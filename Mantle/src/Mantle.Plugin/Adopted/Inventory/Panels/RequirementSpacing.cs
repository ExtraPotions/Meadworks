using HarmonyLib;
using Mantle.Core;
using UnityEngine;

namespace Mantle.Panels
{
    /// <summary>
    /// The crafting panel's requirement slots spread evenly over their bar. When the game shows the required station level
    /// star (<c>m_minStationLevelIcon</c>) at the bar's left, the spread starts right of it (play test P5, 2026-10-13: from x 0
    /// the first slot was drawn under the star).
    /// </summary>
    [HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.Update))]
    public static class RequirementSpacing
    {
        private const float StarGap = 4f;
        private static readonly Vector3[] corners = new Vector3[4];
        private static readonly System.Reflection.FieldInfo requirements = AccessTools.Field(typeof(InventoryGui), "m_recipeRequirementList");
        [HarmonyPostfix, HarmonyPriority(Priority.Last)]
        public static void Postfix(InventoryGui __instance)
        {
            if (!InventoryState.Active || requirements == null) return;
            var slots = requirements.GetValue(__instance) as GameObject[];
            if (slots == null || slots.Length == 0 || slots[0] == null) return;
            var parent = slots[0].transform.parent as RectTransform;
            if (parent == null || parent.rect.width <= 0) return;
            float left = StarRight(__instance, parent);
            float step = (parent.rect.width - left) / slots.Length;
            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i] == null) continue;
                var rect = slots[i].transform as RectTransform;
                if (rect == null) continue;
                // MP2: every frame, so each value is written only when it differs (a write marks the canvas for a rebuild).
                Vector2 anchor = new Vector2(0, .5f), pivot = new Vector2(.5f, .5f);
                Vector2 size = new Vector2(step, rect.sizeDelta.y), at = new Vector2(left + (i + .5f) * step, rect.anchoredPosition.y);
                if (rect.anchorMin != anchor) rect.anchorMin = anchor;
                if (rect.anchorMax != anchor) rect.anchorMax = anchor;
                if (rect.pivot != pivot) rect.pivot = pivot;
                if (rect.sizeDelta != size) rect.sizeDelta = size;
                if (rect.anchoredPosition != at) rect.anchoredPosition = at;
            }
        }

        /// <summary>From the bar's left edge to just past the shown star, in the bar's units; 0 without it.</summary>
        private static float StarRight(InventoryGui gui, RectTransform bar)
        {
            var star = gui.m_minStationLevelIcon != null ? gui.m_minStationLevelIcon.rectTransform : null;
            if (star == null || !star.gameObject.activeInHierarchy) return 0f;
            star.GetWorldCorners(corners);
            return PanelMath.SpreadStart(bar.InverseTransformPoint(corners[2]).x - bar.rect.xMin, bar.rect.width, StarGap);
        }
    }
}
