// Adapted from assets/references/source/ValheimMods-openkeep-3.0.0/GrindstoneSkills/GrindstoneSkills/Fishing/Records/FishTooltip.cs at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Copyright (C) 2026 Gerald Glasgow. SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Saga namespace, configuration, CS1 and ownership boundaries.
using System.Globalization;
using System.Linq;
using HarmonyLib;

namespace ExtraPotions.Saga.Runtime.Adopted.Grindstone
{
    /// <summary>
    /// A fish's tooltip tells what the local player's angler's log says about its species: the levels landed and the
    /// record, or that it is not in the log yet; a legendary fish also says so. The static ItemData.GetTooltip builds
    /// every item tooltip before localization; the lines go at the end. A second item's tooltip appended to this one is a
    /// call of its own with appending set and is skipped.
    /// </summary>
    [HarmonyPatch(typeof(ItemDrop.ItemData), nameof(ItemDrop.ItemData.GetTooltip),
        typeof(ItemDrop.ItemData), typeof(int), typeof(bool), typeof(float), typeof(int), typeof(bool))]
    public static class FishTooltip
    {
        [HarmonyPostfix]
        private static void Postfix(ItemDrop.ItemData item, bool appending, ref string __result)
        {
            if (appending || __result == null || !FishSkill.Active || !FishInfo.IsFishItem(item))
                return;
            __result += HookGuard.Run("fish tooltip", static fish => Lines(fish), item, "");
        }

        private static string Lines(ItemDrop.ItemData item)
        {
            string lines = FishInfo.IsLegendary(item.m_quality) ? SagaText.Text("gs_a25b28642e1e5dae") : "";
            Player player = Player.m_localPlayer;
            if (player == null)
                return lines;
            string prefab = item.m_dropPrefab.name;
            int levels = AnglerLog.Levels(player, prefab);
            if (levels == 0)
                return lines + SagaText.Text("gs_7cb46cdcf5a19e54");
            lines += SagaText.Format("gs_6c1be8a45a2a4d58", string.Join(" ", AnglerLog.LevelsIn(levels).Select(level => level.ToString(CultureInfo.InvariantCulture))));
            if (AnglerLog.Best(player, prefab, out float weight, out int best))
                lines += SagaText.Format("gs_ad9ca565dd4c8388", weight.ToString("0.0", CultureInfo.InvariantCulture), best);
            return lines;
        }
    }
}
