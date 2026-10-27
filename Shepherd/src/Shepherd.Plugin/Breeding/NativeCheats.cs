// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
// Effective C02 switches at native meal and hunger queries; normal tame effects and ownership stay native.
using ExtraPotions.Meadworks.Crucible;
using ExtraPotions.Shepherd.Runtime.Adopted.Husbandry;
using HarmonyLib;

namespace ExtraPotions.Shepherd.Runtime.Breeding
{
    public static class NativeCheats
    {
        static Harmony? harmony;
        static bool installed;
        // A failed install waits for the module to be switched off and on (or a restart) instead of re-patching every frame.
        static bool installFailed;
        static bool Active => harmony != null && Plugin.Ready && MeadworksAuthority.ProductActive(Plugin.Guid, out _)
            && ShepherdSettings.Enabled.Value;
        public static void Initialize() { Shutdown(); harmony = new Harmony(Plugin.Guid + ".cheats"); }
        public static void Tick()
        {
            bool wanted = Active;
            if (!wanted) installFailed = false;
            if (wanted == installed || harmony == null || (wanted && installFailed)) return;
            if (!wanted) { harmony.UnpatchSelf(); installed = false; return; }
            try
            {
                harmony.CreateClassProcessor(typeof(Meal)).Patch();
                harmony.CreateClassProcessor(typeof(Hunger)).Patch();
                installed = true;
            }
            catch { harmony.UnpatchSelf(); installed = false; installFailed = true; throw; }
        }
        public static void Shutdown() { harmony?.UnpatchSelf(); harmony = null; installed = installFailed = false; }

        [HarmonyPatch(typeof(Tameable), nameof(Tameable.OnConsumedItem))]
        static class Meal
        {
            [HarmonyPostfix]
            static void Postfix(Tameable __instance, ItemDrop item)
            {
                if (!Active || !ShepherdSettings.InstantTame.Value || item == null) return;
                NativeHusbandry.Run("instant tame", static tame =>
                {
                    var view = tame.m_nview;
                    if (view != null && view.IsValid() && view.IsOwner() && tame.m_character != null && !tame.m_character.IsDead()
                        && tame.m_monsterAI != null && !tame.IsTamed()) tame.Tame();
                }, __instance);
            }
        }
        [HarmonyPatch(typeof(Tameable), nameof(Tameable.IsHungry))]
        static class Hunger
        {
            [HarmonyPostfix]
            static void Postfix(Tameable __instance, ref bool __result)
            {
                if (Active && ShepherdSettings.NoHunger.Value && __instance.m_nview != null && __instance.m_nview.IsValid()
                    && __instance.m_character != null && !__instance.m_character.IsDead() && __instance.IsTamed()) __result = false;
            }
        }
    }
}
