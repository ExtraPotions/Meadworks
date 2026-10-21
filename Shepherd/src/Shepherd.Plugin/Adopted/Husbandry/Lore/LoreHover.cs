// Adapted from ValheimMods GrindstoneSkills/GrindstoneSkills/Husbandry/Lore/LoreHover.cs
// Copyright (C) 2026 Gerald Glasgow. GPL-3.0-or-later.
// Pin: ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Shepherd lifecycle/cache invalidation, local Compact detail and guarded hover builds.
// SPDX-License-Identifier: GPL-3.0-or-later
using HarmonyLib;
using UnityEngine;

namespace ExtraPotions.Shepherd.Runtime.Adopted.Husbandry
{
    /// <summary>
    /// Animal lore, on each client for its own player: from Animal Lore Level (Husbandry), the crosshair text of an animal
    /// or an egg gets timer lines under the game's text (<see cref="TameableLore"/>, <see cref="GrowthLore"/>).
    /// <list type="bullet">
    /// <item><b>Where:</b> Hud.UpdateCrosshair shows the hovered Hoverable's GetHoverText every frame. A tameable
    /// creature's is <c>Tameable.GetHoverText</c> (<c>Character.GetHoverText</c> forwards to it). A young animal has no
    /// Tameable, so the game shows nothing for it; with lore it gets its name and the growing line. An egg's is
    /// <c>EggGrow.GetHoverText</c>.</item>
    /// <item><b>Cost:</b> the lines are rebuilt at most every 0.25 s for the object looked at, as the vein hover does.</item>
    /// <item><b>Never throws:</b> building runs inside <see cref="NativeHusbandry"/>; a failure shows the game's text alone.</item>
    /// </list>
    /// </summary>
    public static class LoreHover
    {
        private const float RefreshSeconds = 0.25f;

        private static Component? lastSource;
        private static Character? lastCharacter;
        private static bool lastYoung;
        private static string lastLines = "";
        private static float refreshAt;

        [HarmonyPatch(typeof(Tameable), nameof(Tameable.GetHoverText))]
        private static class TameableHover
        {
            [HarmonyPostfix]
            private static void Postfix(Tameable __instance, ref string __result) => Append(__instance, ref __result);
        }

        [HarmonyPatch(typeof(Character), nameof(Character.GetHoverText))]
        private static class YoungHover
        {
            [HarmonyPostfix]
            private static void Postfix(Character __instance, ref string __result)
            {
                if (HusbandrySkill.Active && IsYoung(__instance))
                    Append(__instance, ref __result);
            }
        }

        [HarmonyPatch(typeof(EggGrow), nameof(EggGrow.GetHoverText))]
        private static class EggHover
        {
            [HarmonyPostfix]
            private static void Postfix(EggGrow __instance, ref string __result) => Append(__instance, ref __result);
        }

        /// <summary>A young animal: a Growup and no Tameable. Decided once for the character last looked at.</summary>
        private static bool IsYoung(Character character)
        {
            if (!ReferenceEquals(character, lastCharacter))
            {
                lastCharacter = character;
                lastYoung = character.GetComponent<Tameable>() == null && character.GetComponent<Growup>() != null;
            }
            return lastYoung;
        }

        private static void Append(Component source, ref string text)
        {
            if (!Plugin.AllowsUi || !HusbandrySkill.Active || source == null)
                return;
            string lines = Cached(source);
            if (lines.Length == 0)
                return;
            if (string.IsNullOrEmpty(text) && source is Character young)
                text = LoreText.Literal(global::Localization.instance != null ? global::Localization.instance.Localize(young.m_name) : young.m_name);
            text = LoreText.Add(text, lines);
        }

        private static string Cached(Component source)
        {
            float now = Time.time;
            if (ReferenceEquals(source, lastSource) && now < refreshAt)
                return lastLines;
            lastSource = source;
            refreshAt = now + RefreshSeconds;
            lastLines = NativeHusbandry.Run("animal lore", static lore => Build(lore), source, "") ?? "";
            if (ShepherdSettings.Detail.Value == LoreDetail.Compact) lastLines = lastLines.Replace("\n", " · ");
            return lastLines;
        }

        public static void Clear()
        {
            lastSource = null; lastCharacter = null; lastYoung = false; lastLines = ""; refreshAt = 0;
        }

        private static string Build(Component source)
        {
            bool lore = LoreText.Visible;
            if (source is Tameable tameable)
                return tameable.m_nview != null && tameable.m_nview.IsValid() ? TameableLore.Lines(tameable, lore) : "";
            if (!lore)
                return "";
            if (source is EggGrow egg)
                return GrowthLore.EggLine(egg);
            return source is Character young ? GrowthLore.YoungLine(young) : "";
        }
    }
}
