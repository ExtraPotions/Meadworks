// Adapted from assets/references/source/ValheimMods-openkeep-3.0.0/GrindstoneSkills/GrindstoneSkills/Pickaxes/Veins/VeinHover.cs at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Copyright (C) 2026 Gerald Glasgow. SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Saga namespace, configuration, CS1 and ownership boundaries.
using HarmonyLib;
using UnityEngine;

namespace ExtraPotions.Saga.Runtime.Adopted.Grindstone
{
    /// <summary>
    /// Read the rock, on each client for its own player: from "Read The Rock Level" (Pickaxes), the crosshair text of an
    /// ore deposit gets <see cref="VeinText"/>'s lines under the game's name. Below the level, on plain stone, and with
    /// Pickaxes off, the game's text is left alone.
    /// <list type="bullet">
    /// <item><b>Where:</b> Hud.UpdateCrosshair shows the hovered object's Hoverable.GetHoverText every frame. Multi-chunk
    /// rocks are Hoverables themselves (MineRock5, MineRock: their localized m_name). A Destructible is not: an intact
    /// deposit (copper, silver vein, mud pile) or a single-piece ore (tin, obsidian) shows the HoverText component its
    /// prefab carries, so HoverText.GetHoverText is patched and kept only when a rock's Destructible owns it. A deposit
    /// without any Hoverable (the intact flametal rockstand) shows nothing, as in the game; its fractured form has an
    /// empty name, and gets the lines on their own.</item>
    /// <item><b>Cost:</b> the lines are built at most every 0.25 s for the object being looked at and reused in between
    /// (one entry: the last hover source), so the per-frame work is a reference compare; that also covers a level or
    /// setting change within a quarter second.</item>
    /// <item><b>Never throws:</b> building the lines runs inside <see cref="HookGuard"/>; a failure shows the game's text
    /// alone until the next refresh.</item>
    /// </list>
    /// </summary>
    public static class VeinHover
    {
        private const float RefreshSeconds = 0.25f;

        private static Component lastSource;
        private static string lastLines = "";
        private static float refreshAt;

        [HarmonyPatch(typeof(MineRock5), nameof(MineRock5.GetHoverText))]
        private static class ChunkedHover
        {
            [HarmonyPostfix]
            private static void Postfix(MineRock5 __instance, ref string __result) => Append(__instance, ref __result);
        }

        [HarmonyPatch(typeof(MineRock), nameof(MineRock.GetHoverText))]
        private static class OldChunkedHover
        {
            [HarmonyPostfix]
            private static void Postfix(MineRock __instance, ref string __result) => Append(__instance, ref __result);
        }

        [HarmonyPatch(typeof(HoverText), nameof(HoverText.GetHoverText))]
        private static class PieceHover
        {
            [HarmonyPostfix]
            private static void Postfix(HoverText __instance, ref string __result) => Append(__instance, ref __result);
        }

        private static void Append(Component source, ref string text)
        {
            if (!PickSkill.Active || source == null)
                return;
            string lines = Cached(source);
            if (lines.Length > 0)
                text = string.IsNullOrEmpty(text) ? lines : text + "\n" + lines;
        }

        /// <summary>The lines for this hover source, rebuilt when the source changed or the refresh time passed.</summary>
        private static string Cached(Component source)
        {
            float now = Time.time;
            if (ReferenceEquals(source, lastSource) && now < refreshAt)
                return lastLines;
            lastSource = source;
            refreshAt = now + RefreshSeconds;
            lastLines = "";
            lastLines = HookGuard.Run("read the rock", static rock => Build(rock), source, "") ?? "";
            return lastLines;
        }

        /// <summary>The lines when the local player has the milestone and the source is an ore deposit; "" otherwise.</summary>
        private static string Build(Component source)
        {
            if (!PickSkill.Reached(PickSkill.Local(), VeinSettings.ReadTheRockLevel.Value))
                return "";
            Rock rock = Rock.Of(TargetOf(source));
            return rock != null && rock.IsOre ? VeinText.Lines(rock) : "";
        }

        /// <summary>The rock component behind a hover source: a HoverText's Destructible (null when none), else the source itself.</summary>
        private static Component TargetOf(Component source) =>
            source is HoverText ? source.GetComponentInParent<Destructible>() : source;
    }
}
