// Portions Copyright (C) 2026 Gerald Glasgow. EliteCreaturesPack 0.7.2 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Changed by expDARE, 2026: Bestiary namespace, owned Crucible settings, rebuilt-only bundles and lifecycle adapters.
// SPDX-License-Identifier: GPL-3.0-or-later
using System.Collections.Generic;
using HarmonyLib;

namespace ExtraPotions.Bestiary.Runtime.Native.Core
{
    /// <summary>A creature that can pass for something else: while it does, its hover text and name are replaced.</summary>
    public interface IDisguise
    {
        /// <summary>Whether the disguise holds right now (a dormant mimic, a sleeping giant).</summary>
        bool Holds { get; }

        string HoverText();

        string HoverName();
    }

    /// <summary>
    /// The loaded creatures that can be disguised, registered by their own components from Awake to OnDestroy. The
    /// hover and name plate patches below run for every character the game shows, every frame, so for every other
    /// creature they cost one count test (nothing is loaded) or one lookup.
    /// </summary>
    public static class Disguises
    {
        private static readonly Dictionary<Character, IDisguise> loaded = new Dictionary<Character, IDisguise>();

        public static void Register(Character? character, IDisguise disguise)
        {
            if (character != null)
            {
                loaded[character] = disguise;
            }
        }

        public static void Unregister(Character? character)
        {
            if (character != null)
            {
                loaded.Remove(character);
            }
        }

        /// <summary>The disguise this character wears right now, or null when it has none or it does not hold.</summary>
        public static IDisguise? Holding(Character? character)
        {
            if (loaded.Count == 0 || character == null || !loaded.TryGetValue(character, out IDisguise disguise))
            {
                return null;
            }
            return disguise.Holds ? disguise : null;
        }
    }

    /// <summary>
    /// A disguised creature's hover text and name are the disguise's, decided after every other mod's decoration
    /// (Elite Creatures Reborn's star and mutation words included, when it is installed). One patch per hot method.
    /// </summary>
    [HarmonyPatch(typeof(Character))]
    public static class DisguiseHoverPatch
    {
        [HarmonyPatch("GetHoverText"), HarmonyPostfix, HarmonyPriority(Priority.Last)]
        private static void HoverText(Character __instance, ref string __result)
        {
            IDisguise? disguise = Disguises.Holding(__instance);
            if (disguise != null)
            {
                __result = disguise.HoverText();
            }
        }

        [HarmonyPatch("GetHoverName"), HarmonyPostfix, HarmonyPriority(Priority.Last)]
        private static void HoverName(Character __instance, ref string __result)
        {
            IDisguise? disguise = Disguises.Holding(__instance);
            if (disguise != null)
            {
                __result = disguise.HoverName();
            }
        }
    }

    /// <summary>No name plate or health bar over a disguised creature.</summary>
    [HarmonyPatch(typeof(EnemyHud), "TestShow")]
    public static class DisguiseHudPatch
    {
        private static void Postfix(Character c, ref bool __result)
        {
            if (__result && Disguises.Holding(c) != null)
            {
                __result = false;
            }
        }
    }
}
