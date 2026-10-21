// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: keeps the game's status effect row clear of Cairn's top-center compass plank.
using System;
using ExtraPotions.Cairn.Hud;
using HarmonyLib;
using UnityEngine;
using GameHud = global::Hud;

namespace ExtraPotions.Cairn.Runtime
{
    /// <summary>
    /// The game lays its status effects out in rows of <c>m_effectsPerRow</c>, growing leftwards from the top-right corner. Mantle
    /// can move that row left to make room for its stat column, and with several effects the row then runs under the compass
    /// plank. While the plank shows and shares the row's height, the row wraps sooner (<see cref="HudLayout.StatusEffectsPerRow"/>)
    /// using the game's own spacing and wrap formula; otherwise, or once Cairn stops, the game's positions come back. While a
    /// window that hides the plank is open (the inventory and crafting window above all) the row keeps the wrap it had
    /// (<see cref="StatusRowHold"/>), so opening the window never moves the effects.
    /// </summary>
    public static class CairnStatusRoom
    {
        static readonly Vector3[] corners = new Vector3[4];
        static bool adjusted;
        static readonly StatusRowHold hold = new StatusRowHold();
        static int heldPerRow;

        /// <summary>The windows that hide the plank; Mantle holds the row on the same ones, so the two never disagree.</summary>
        static bool WindowOpen()
        {
            if (InventoryGui.IsVisible() || Menu.IsVisible() || Minimap.IsOpen()) return true;
            try { return ExtraPotions.Meadworks.Crucible.UI.KitWindows.HudBlocked; }
            catch { return false; }
        }

        static void Apply(GameHud hud)
        {
            if (hud == null || hud.m_statusEffectListRoot == null) return;
            var effects = hud.m_statusEffects;
            var native = Math.Max(1, hud.m_effectsPerRow);
            var perRow = native;
            var root = hud.m_rootObject != null ? hud.m_rootObject.transform as RectTransform : null;
            var held = hold.Holding(WindowOpen(), Time.frameCount);
            if (held)
                perRow = heldPerRow > 0 ? Math.Min(heldPerRow, native) : native;
            else if (effects.Count > 1 && root != null && effects[0] != null)
            {
                var plank = CairnHud.Occupied();
                // The first cell never moves (row 0, column 0); its center and the native spacing give every other cell.
                effects[0].GetWorldCorners(corners);
                Vector3 low = root.InverseTransformPoint(corners[0]), high = root.InverseTransformPoint(corners[2]);
                var center = (low.x + high.x) * .5f - root.rect.xMin;
                var top = root.rect.yMax - Mathf.Max(low.y, high.y);
                var list = hud.m_statusEffectListRoot;
                var unit = root.lossyScale.x != 0 ? list.lossyScale.x / root.lossyScale.x : 1f;
                var cell = hud.m_statusEffectSpacing * unit;
                perRow = HudLayout.StatusEffectsPerRow(native, center, cell, plank.Item1, plank.Item2 + plank.Item4,
                    plank.Item3, plank.Item3 + plank.Item5, top, top + cell);
            }
            if (!held) heldPerRow = perRow;
            if (perRow == native && !adjusted) return;
            Place(hud, perRow);
            adjusted = perRow != native;
        }

        /// <summary>The game's own placement (UpdateStatusEffects), with <paramref name="perRow"/> cells per row.</summary>
        static void Place(GameHud hud, int perRow)
        {
            var effects = hud.m_statusEffects;
            for (var i = 0; i < effects.Count; i++)
            {
                var effect = effects[i];
                if (effect == null) continue;
                int row = i / perRow, column = i - row * perRow;
                var wanted = new Vector2(-4f - column * hud.m_statusEffectSpacing, -row * hud.m_statusEffectSpacing);
                if (effect.anchoredPosition != wanted) effect.anchoredPosition = wanted;
            }
        }

        public static void Reset()
        {
            var hud = GameHud.instance;
            if (adjusted && hud != null) Place(hud, Math.Max(1, hud.m_effectsPerRow));
            adjusted = false;
            hold.Reset();
            heldPerRow = 0;
        }

        /// <summary>Right after the game places and fills its status effects, so a new effect never shows one frame in the old place.</summary>
        [HarmonyPatch(typeof(GameHud), "UpdateStatusEffects")]
        internal static class Patches
        {
            [HarmonyPostfix, HarmonyPriority(Priority.Last)]
            static void Postfix(GameHud __instance)
            {
                try { Apply(__instance); }
                catch (Exception error) { Reset(); Plugin.Log?.Warning("Cairn status effect room: " + error.GetType().Name); }
            }
        }
    }
}
