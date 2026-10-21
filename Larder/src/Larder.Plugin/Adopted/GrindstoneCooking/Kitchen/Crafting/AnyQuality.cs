// Copyright (C) 2026 Gerald Glasgow. SPDX-License-Identifier: GPL-3.0-or-later
// Adopted from ValheimMods 7c710ed119b547783812e8171c86f31ce1f7ea50: ValheimModLibs/RecipeCounts/AnyQuality.cs
// Changed by expDARE, 2026: Larder session lifecycle. The count rule is RecipeQuality.
using System.Collections.Generic;
using System.Reflection;
using ExtraPotions.Larder.Cooking;
using ExtraPotions.Meadworks.Crucible;
using HarmonyLib;

namespace ExtraPotions.Larder.Runtime.Adopted.GrindstoneCooking
{
    /// <summary>
    /// While a recipe needs every ingredient, CountItems(name, 1) for a maximum quality of 1 counts every quality.
    /// The follow-up call at quality -1 runs the original count.
    /// </summary>
    public static class AnyQuality
    {
        const string HarmonyId = "com.meadworks.larder.recipecounts";
        static readonly Dictionary<string, bool> singleQuality = new Dictionary<string, bool>();
        static Harmony? harmony;
        static bool patched;
        static bool counting;

        public static void Install(MeadworksLog log)
        {
            if (patched) return;
            MethodInfo have = AccessTools.Method(typeof(Player), "HaveRequirementItems", new[] { typeof(Recipe), typeof(bool), typeof(int), typeof(int) });
            MethodInfo count = AccessTools.Method(typeof(Inventory), nameof(Inventory.CountItems), new[] { typeof(string), typeof(int), typeof(bool) });
            if (have == null || count == null)
            {
                log?.Error("Starred-egg recipe count was not installed: HaveRequirementItems or CountItems is missing.");
                return;
            }
            Harmony created = new Harmony(HarmonyId);
            try
            {
                created.Patch(have,
                    prefix: new HarmonyMethod(typeof(AnyQuality), nameof(Open)),
                    finalizer: new HarmonyMethod(typeof(AnyQuality), nameof(Close)));
                created.Patch(count, prefix: new HarmonyMethod(typeof(AnyQuality), nameof(CountPrefix)));
                harmony = created;
                patched = true;
            }
            catch
            {
                created.UnpatchSelf();
                throw;
            }
        }

        public static void Shutdown()
        {
            harmony?.UnpatchSelf();
            harmony = null;
            patched = false;
            counting = false;
            singleQuality.Clear();
        }

        static void Open(Recipe piece) =>
            counting = RecipeQuality.TracksEveryIngredient(piece != null, piece != null && piece.m_requireOnlyOneIngredient);

        static void Close() => counting = false;

        static bool CountPrefix(Inventory __instance, string name, int quality, bool matchWorldLevel, ref int __result, bool __runOriginal)
        {
            if (!__runOriginal) return false;
            if (!counting || quality != 1 || !RecipeQuality.CountEveryQuality(true, 1, SingleMax(__instance, name)))
                return true;
            __result = __instance.CountItems(name, -1, matchWorldLevel);
            return false;
        }

        /// <summary>Maximum quality of the first stack with this name, or 2 when the name is absent so the count stays narrow.</summary>
        static int SingleMax(Inventory inventory, string name)
        {
            if (name == null) return 2;
            if (singleQuality.TryGetValue(name, out bool single)) return single ? 1 : 2;
            foreach (ItemDrop.ItemData item in inventory.GetAllItems())
            {
                if (item.m_shared.m_name == name)
                {
                    single = item.m_shared.m_maxQuality <= 1;
                    singleQuality[name] = single;
                    return single ? 1 : item.m_shared.m_maxQuality;
                }
            }
            return 2;
        }
    }
}
