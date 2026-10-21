// Adopted from ValheimMods FeastMaster/FeastMaster/FeastMasterCore/UpdateStatsPatch.cs at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Copyright (C) 2026 Gerald Glasgow. SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Larder namespaces, Meadworks configuration/module lifecycle boundaries.
using HarmonyLib;

namespace ExtraPotions.Larder.Runtime.Adopted.FeastMaster
{
    /// <summary>What the Player.UpdateStats prefix scaled for the call, put back by the finalizer.</summary>
    public struct UpdateStatsSaved
    {
        public ScaledGroup Regen;
        public ScaledFields Encumbered;
    }

    /// <summary>
    /// The one Player.UpdateStats patch (it runs every physics step): installed while any of its rules is changed, each
    /// rule then asked in turn through its <see cref="ChangedRules"/> flag. The prefix scales the regen fields
    /// (<see cref="RegenBasicsRule"/>) and the encumbered drain (<see cref="EncumberedCostRule"/>) for the call, the
    /// postfix adds the regeneration while encumbered or swimming (<see cref="RestrictedStaminaRegen"/>) with the
    /// fields still scaled, and the finalizer puts the fields back.
    /// </summary>
    [HarmonyPatch(typeof(Player), nameof(Player.UpdateStats), typeof(float))]
    public static class UpdateStatsPatch
    {
        public static bool Prepare() => ChangedRules.RegenBasics || ChangedRules.EncumberedCost || ChangedRules.RestrictedRegen || SafeStamina.Enabled;

        [HarmonyPrefix]
        public static void Prefix(Player __instance, out UpdateStatsSaved __state)
        {
            __state = default;
            if (ChangedRules.RegenBasics)
                __state.Regen = RegenBasicsRule.Scale(__instance);
            if (ChangedRules.EncumberedCost)
                __state.Encumbered = EncumberedCostRule.Scale(__instance);
            if (ChangedRules.RestrictedRegen || SafeStamina.Enabled)
                StaminaCapture.Begin(__instance);
        }

        [HarmonyPostfix]
        public static void Postfix(Player __instance, float dt)
        {
            if (ChangedRules.RestrictedRegen || SafeStamina.Enabled)
                RestrictedStaminaRegen.Apply(__instance, dt);
        }

        [HarmonyFinalizer]
        public static void Finalizer(Player __instance, UpdateStatsSaved __state)
        {
            StaminaCapture.End();
            RegenBasicsRule.Restore(__instance, __state.Regen);
            EncumberedCostRule.Restore(__instance, __state.Encumbered);
        }
    }

    /// <summary>
    /// The stamina multiplier the game's own UpdateStats got from SEMan.ModifyStaminaRegen (after every mod's postfix),
    /// so the regeneration while encumbered or swimming reuses it instead of asking every status effect again in the
    /// same physics step. Only the first call for the updating player's SEMan inside UpdateStats is kept.
    /// </summary>
    internal static class StaminaCapture
    {
        private static SEMan watched;
        private static bool seen;
        private static float multiplier;

        public static void Begin(Player player)
        {
            watched = player.m_seman;
            seen = false;
        }

        public static void End() => watched = null;

        public static void Seen(SEMan seman, float value)
        {
            if (seen || watched == null || !ReferenceEquals(seman, watched))
                return;
            multiplier = value;
            seen = true;
        }

        public static bool TryGet(Player player, out float value)
        {
            value = multiplier;
            return seen && watched != null && ReferenceEquals(player.m_seman, watched);
        }
    }
}
