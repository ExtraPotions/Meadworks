// Cairn - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original, clean room: names only on hover or while a key is held, smaller icons (player, ping and shout pins kept),
// a larger hovered pin and faded crossed-out pins, from the user's requests. No TidyPins code, DLL, download or implementation
// was inspected. Native Valheim APIs were inspected, not copied.
using System.Collections.Generic;
using ExtraPotions.Cairn.Config;
using ExtraPotions.Cairn.Hud;
using ExtraPotions.Meadworks.Crucible;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace ExtraPotions.Cairn.Runtime
{
    /// <summary>Local presentation only. Never changes pin data, checked state, ownership, map fog or saved names.</summary>
    public static class CairnTidyPins
    {
        // Reuse the storage each frame. Restore before the game updates its pins so size/alpha never compound.
        private static readonly List<VisualState> written = new List<VisualState>();

        private struct VisualState
        {
            public RectTransform Rect;
            public Vector3 BeforeScale, AppliedScale;
            public Graphic Icon, Check;
            public Color BeforeIcon, AppliedIcon, BeforeCheck, AppliedCheck;
            public GameObject Name;
            public bool BeforeName, AppliedName;

            public void Restore()
            {
                // A later writer owns its changes. Destroyed Unity objects are deliberately skipped.
                if (Rect != null && Rect.localScale == AppliedScale) Rect.localScale = BeforeScale;
                if (Icon != null && Icon.color == AppliedIcon) Icon.color = BeforeIcon;
                if (Check != null && Check.color == AppliedCheck) Check.color = BeforeCheck;
                if (Name != null && Name.activeSelf == AppliedName) Name.SetActive(BeforeName);
            }
        }

        /// <summary>Restores only the last values this feature still owns, including when disabled or unloaded.</summary>
        public static void Reset()
        {
            for (var i = 0; i < written.Count; i++) written[i].Restore();
            written.Clear();
        }

        /// <summary>Run after Minimap.Update, whose native pin refresh may run less frequently than cursor movement.</summary>
        public static void Apply(Minimap map)
        {
            Reset();
            var settings = Plugin.Settings;
            if (!Plugin.Ready || !Plugin.AllowsUi || settings == null || !settings.Get(CairnSettings.Map.TidyPins) || map == null) return;

            // Native pad pin interactions use the center reticle; mouse interactions use the game pointer.
            var cursor = ZInput.IsGamepadActive()
                ? new Vector2(Screen.width * 0.5f, Screen.height * 0.5f)
                : (Vector2)ZInput.pointerPosition;
            var large = map.m_mode == Minimap.MapMode.Large;
            var canvas = map.m_mapImageLarge != null ? map.m_mapImageLarge.GetComponentInParent<Canvas>() : null;
            var camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            var overMap = large && !ZInput.VirtualKeyboardOpen && map.m_mapImageLarge != null
                && RectTransformUtility.RectangleContainsScreenPoint(map.m_mapImageLarge.rectTransform, cursor, camera);
            Minimap.PinData hovered = null;
            var nearest = float.MaxValue;
            if (overMap)
                foreach (var pin in map.m_pins)
                {
                    var rect = pin.m_uiElement;
                    if (rect == null || !rect.gameObject.activeInHierarchy || pin.m_worldSize > 0f
                        || !RectTransformUtility.RectangleContainsScreenPoint(rect, cursor, camera)) continue;
                    var distance = (RectTransformUtility.WorldToScreenPoint(camera, rect.position) - cursor).sqrMagnitude;
                    if (distance >= nearest) continue;
                    nearest = distance;
                    hovered = pin;
                }
            // The map owns the cursor, so the key is read in the window context; typing a pin name never counts.
            var showAll = large && MeadworksInput.Held(settings.ShortcutEntry(CairnSettings.Map.ShowAllNamesKey), InputContext.Window);
            var hoverScale = settings.Get(CairnSettings.Map.HoverScale);

            foreach (var pin in map.m_pins)
            {
                var rect = pin.m_uiElement;
                if (rect == null || !rect.gameObject.activeInHierarchy || pin.m_worldSize > 0f) continue;
                var icon = pin.m_iconElement;
                var check = pin.m_checkedElement != null ? pin.m_checkedElement.GetComponent<Graphic>() : null;
                var name = pin.m_NamePinData?.PinNameGameObject;
                var type = pin.m_type.ToString();
                var isHovered = ReferenceEquals(pin, hovered);
                var state = new VisualState
                {
                    Rect = rect, BeforeScale = rect.localScale, AppliedScale = rect.localScale * TidyPinRules.Scale(type, isHovered, hoverScale),
                    Icon = icon, BeforeIcon = icon != null ? icon.color : Color.white,
                    Check = check, BeforeCheck = check != null ? check.color : Color.white,
                    Name = name, BeforeName = name != null && name.activeSelf,
                    AppliedName = TidyPinRules.ShowsName(large, isHovered, showAll, pin.m_name),
                };
                state.AppliedIcon = state.BeforeIcon;
                state.AppliedCheck = state.BeforeCheck;
                var alpha = TidyPinRules.Alpha(type, pin.m_checked);
                state.AppliedIcon.a *= alpha;
                state.AppliedCheck.a *= alpha;
                written.Add(state);
                rect.localScale = state.AppliedScale;
                if (icon != null) icon.color = state.AppliedIcon;
                if (check != null) check.color = state.AppliedCheck;
                if (name != null) name.SetActive(state.AppliedName);
            }
        }

        [HarmonyPatch(typeof(Minimap), "Update")]
        internal static class Patches
        {
            [HarmonyPrefix, HarmonyPriority(Priority.First)]
            private static void Prefix() => Reset();

            [HarmonyPostfix, HarmonyPriority(Priority.Last)]
            private static void Postfix(Minimap __instance) => Apply(__instance);
        }
    }
}
