// Larder - Copyright (C) 2026 expDARE (ExtraPotions Meadworks).
// Shared-lifecycle integration for the directly adopted FeastMaster HUD presentation.
using System;
using System.Collections.Generic;
using ExtraPotions.Larder.Runtime.Adopted.FeastMaster;
using ExtraPotions.Meadworks.Crucible;
using UnityEngine;

namespace ExtraPotions.Larder.Runtime.Adopted.FoodHud
{
    public readonly struct FoodHudGeometry
    {
        public FoodHudGeometry(int visibleCount, float bottomEdge) { VisibleCount = visibleCount; BottomEdge = bottomEdge; }
        public int VisibleCount { get; }
        /// <summary>Bottom edge in screen pixels, measured from the screen's bottom as Unity screen coordinates are.</summary>
        public float BottomEdge { get; }
    }

    public static class NativeFoodHud
    {
        /// <summary>Call before NativeFeastMaster.Initialize; the existing switch owns installation and removal.</summary>
        public static void Register()
        {
            NativeFeastMaster.AddPatchTypes(new[] { typeof(FoodSlotsHud), typeof(FoodSlotFrames), typeof(FoodTimersPatch) }, "Eating");
            NativeFeastMaster.AddPatchTypes(new[] { typeof(HideHealthNumberPatch), typeof(HideStaminaNumberPatch), typeof(HideEitrNumberPatch) }, "Eating");
            NativeFeastMaster.AddPatchTypes(new[] { typeof(VigorTooltipPatch) }, "Recovery");
        }

        /// <summary>The actual native/copy food column, for the existing Larder/Mantle capability boundary.</summary>
        public static FoodHudGeometry Geometry()
        {
            Hud hud = Hud.instance;
            Player player = Player.m_localPlayer;
            if (!MeadworksHeadless.AllowsUi || hud == null || player == null || hud.m_foodIcons == null)
                return new FoodHudGeometry(0, 0f);
            int count = 0;
            float bottom = float.PositiveInfinity;
            var corners = new Vector3[4];
            for (int i = 0; i < hud.m_foodIcons.Length; i++)
            {
                var roots = new HashSet<Transform>();
                int other = i == 0 ? 1 : i - 1;
                AddRoot(hud.m_foodIcons, i, other, roots);
                AddRoot(hud.m_foodBars, i, other, roots);
                AddRoot(hud.m_foodTime, i, other, roots);
                bool visible = false;
                foreach (Transform root in roots)
                {
                    if (root == null || !root.gameObject.activeInHierarchy) continue;
                    visible = true;
                    foreach (RectTransform rect in root.GetComponentsInChildren<RectTransform>(false)) Measure(rect, corners, ref bottom);
                }
                if (visible) count++;
            }
            return new FoodHudGeometry(count, float.IsInfinity(bottom) ? 0f : bottom);
        }

        public static Tuple<int, float> CapabilityGeometry()
        {
            var geometry = Geometry();
            return Tuple.Create(geometry.VisibleCount, geometry.BottomEdge);
        }

        private static void AddRoot<T>(T[] parts, int slot, int other, HashSet<Transform> roots) where T : Component
        {
            if (parts == null || slot >= parts.Length || parts[slot] == null) return;
            Transform item = parts[slot].transform;
            roots.Add(other < parts.Length && parts[other] != null ? FoodSlotsHud.SlotRoot(item, parts[other].transform) : item);
        }

        private static void Measure(RectTransform rect, Vector3[] corners, ref float bottom)
        {
            if (rect == null) return;
            Canvas canvas = rect.GetComponentInParent<Canvas>();
            Camera camera = canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            rect.GetWorldCorners(corners);
            foreach (Vector3 corner in corners) bottom = Mathf.Min(bottom, RectTransformUtility.WorldToScreenPoint(camera, corner).y);
        }
    }
}
