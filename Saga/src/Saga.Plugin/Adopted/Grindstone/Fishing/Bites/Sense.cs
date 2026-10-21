// Adapted from assets/references/source/ValheimMods-openkeep-3.0.0/GrindstoneSkills/GrindstoneSkills/Fishing/Bites/Sense.cs at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Copyright (C) 2026 Gerald Glasgow. SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Saga namespace, configuration, CS1 and ownership boundaries.
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;

namespace ExtraPotions.Saga.Runtime.Adopted.Grindstone
{
    /// <summary>
    /// The angler's senses, milestones of the angler's level at the cast, all on the angler's own client:
    /// <list type="bullet">
    /// <item><b>Species Sense Level:</b> a nibble names the fish ("A Pike nibbles!", centre), and a cast that lands tells
    /// the fishing conditions (top left, <see cref="FishingConditions"/>).</item>
    /// <item><b>Size Sense Level:</b> the nibble also tells its size ("A level 4 Pike nibbles!", "A legendary Pike
    /// nibbles!"), and looking at a fish in the water shows its level.</item>
    /// <item><b>Water Sense Level:</b> a cast that lands tells which fish within the float's reach (50 m) take the bait on
    /// the hook, counted by species, and whether a legendary fish is among the fish in reach (any bait).</item>
    /// </list>
    /// Only fish loaded on this client are counted: the game loads the world around the player, which covers a float's
    /// reach. A legendary fish in the water reads "Legendary Pike" to anyone who looks at it.
    /// </summary>
    public static class Sense
    {
        [HarmonyPatch(typeof(Fish), nameof(Fish.GetHoverText))]
        private static class Hover
        {
            [HarmonyPostfix]
            private static void Postfix(Fish __instance, ref string __result)
            {
                if (!FishSkill.Active || __result == null || __instance.IsOutOfWater())
                    return;
                __result = HookGuard.Run("fish hover", static hover => HoverText(hover.fish, hover.text), (fish: __instance, text: __result), __result);
            }
        }

        /// <summary>Called by <see cref="Strike"/> for each nibble with the right bait on the local player's float.</summary>
        public static void OnNibble(FloatFight fight, Fish fish, Player angler)
        {
            if (fish == null || !FishSkill.Reached(fight.Level, FishingBiteSettings.SpeciesSenseLevel.Value))
                return;
            string what = SagaText.Language == "English" ? FishInfo.WithArticle(FishInfo.Name(fish)) : FishInfo.Name(fish);
            if (FishSkill.Reached(fight.Level, FishingBiteSettings.SizeSenseLevel.Value))
                what = SizedName(fish);
            angler.Message(MessageHud.MessageType.Center, SagaText.Format("gs_807d7e0c8fdeb92f", char.ToUpperInvariant(what[0]) + what.Substring(1)));
        }

        /// <summary>Called by <see cref="FloatScope"/> every step of the local player's float; reads the water once, when it lands in it.</summary>
        public static void OnStep(FishingFloat fishingFloat, FloatFight fight, Player angler)
        {
            if (fight.Landed || !fishingFloat.IsInWater())
                return;
            fight.Landed = true;
            if (FishSkill.Reached(fight.Level, FishingBiteSettings.SpeciesSenseLevel.Value))
                angler.Message(MessageHud.MessageType.TopLeft, FishingConditions.Describe());
            if (FishSkill.Reached(fight.Level, FishingBiteSettings.WaterSenseLevel.Value))
                angler.Message(MessageHud.MessageType.TopLeft, ReadWater(fishingFloat));
        }

        /// <summary>"a level 4 Pike", "a legendary Pike".</summary>
        private static string SizedName(Fish fish)
        {
            int level = FishInfo.Level(fish);
            string name = FishInfo.Name(fish);
            return FishInfo.IsLegendary(level) ? SagaText.Format("gs_6daa264a277664bc", name) : SagaText.Format("gs_0cdb70105339fcb4", level, name);
        }

        private static string HoverText(Fish fish, string text)
        {
            int level = FishInfo.Level(fish);
            if (FishInfo.IsLegendary(level))
                return SagaText.Format("gs_428c9c86b11bee44", text);
            return FishSkill.Reached(FishSkill.Local(), FishingBiteSettings.SizeSenseLevel.Value) ? SagaText.Format("gs_47b88e174fca2199", text, level) : text;
        }

        private static string ReadWater(FishingFloat fishingFloat)
        {
            string bait = fishingFloat.GetBait();
            Dictionary<string, int> biting = new Dictionary<string, int>();
            bool lurks = false;
            foreach (Fish fish in InReach(fishingFloat))
            {
                lurks |= FishInfo.IsLegendary(FishInfo.Level(fish));
                if (TakesBait(fish, bait))
                    biting[FishInfo.Name(fish)] = biting.TryGetValue(FishInfo.Name(fish), out int count) ? count + 1 : 1;
            }
            string text = biting.Count == 0 ? SagaText.Text("gs_4861eaf9f84ded99")
                : SagaText.Format("gs_e7bbceacc5e4623f", string.Join(", ", biting.Select(pair => pair.Value + " " + pair.Key)));
            return lurks ? SagaText.Format("gs_ed76ff235b2a28c5", text) : text;
        }

        /// <summary>Loaded fish swimming within the float's reach.</summary>
        private static IEnumerable<Fish> InReach(FishingFloat fishingFloat)
        {
            Vector3 at = fishingFloat.transform.position;
            foreach (IMonoUpdater updater in Fish.Instances)
            {
                if (updater is Fish fish && fish != null && !fish.IsOutOfWater()
                    && Vector3.Distance(at, fish.transform.position) <= fishingFloat.m_range)
                    yield return fish;
            }
        }

        private static bool TakesBait(Fish fish, string bait) =>
            fish.m_baits.Exists(setting => setting.m_bait != null && setting.m_bait.name == bait && setting.m_chance > 0f);
    }
}
