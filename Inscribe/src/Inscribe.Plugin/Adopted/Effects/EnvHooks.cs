// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
using HarmonyLib;

namespace ExtraPotions.Inscribe.Adopted.Effects
{
    /// <summary>
    /// Weather statuses of the local player, decided in Player.UpdateEnvStatusEffects on the player's own client. While
    /// that method runs for the local player, the status changes it asks for pass through here:
    /// <list type="bullet">
    /// <item>Oilskin: Wet from rain is not added (Wet from wading comes from Character.UpdateWater, outside this method,
    /// and still applies).</item>
    /// <item>Emberheart: Cold is not added.</item>
    /// <item>Winterborn: Freezing is not added; the player gets Cold instead (unless Emberheart), so freezing weather
    /// still reads as cold. The game's freezing branch first removes Cold quietly and adds Freezing only when there
    /// was no Cold to remove; that one quiet removal is refused so the swap stays stable instead of flickering.</item>
    /// </list>
    /// </summary>
    internal static class EnvHooks
    {
        private static bool _active;

        public static void Begin(Player player) => _active = ReferenceEquals(player, Player.m_localPlayer);

        public static void End() => _active = false;

        /// <summary>False = do not add this status now.</summary>
        public static bool AllowAdd(SEMan seman, int hash)
        {
            if (!_active)
            {
                return true;
            }
            AggregateValues v = AggregateHost.Current;
            if (hash == SEMan.s_statusEffectWet)
            {
                return v[EffectKind.RainShield] <= 0f;
            }
            if (hash == SEMan.s_statusEffectCold)
            {
                return v[EffectKind.ColdImmunity] <= 0f;
            }
            return hash != SEMan.s_statusEffectFreezing || v[EffectKind.FreezeImmunity] <= 0f || ColdInstead(seman, v);
        }

        // Winterborn: whatever Freezing is on goes, Cold takes its place (unless Emberheart). Always false: skip the add.
        private static bool ColdInstead(SEMan seman, AggregateValues v)
        {
            seman.RemoveStatusEffect(SEMan.s_statusEffectFreezing, quiet: true);
            if (v[EffectKind.ColdImmunity] <= 0f)
            {
                seman.AddStatusEffect(SEMan.s_statusEffectCold);
            }
            return false;
        }

        /// <summary>False = keep the status (Winterborn keeps Cold in freezing weather).</summary>
        public static bool AllowRemove(int hash, bool quiet) =>
            !_active || !quiet || hash != SEMan.s_statusEffectCold || AggregateHost.Current[EffectKind.FreezeImmunity] <= 0f;
    }

    [HarmonyPatch]
    internal static class EnvStatusPatches
    {
        [HarmonyPrefix]
        [HarmonyPatch(typeof(Player), nameof(Player.UpdateEnvStatusEffects))]
        private static void Begin(Player __instance) => EnvHooks.Begin(__instance);

        // A finalizer, not a postfix: the window closes even when the method throws, so other characters' statuses are never filtered.
        [HarmonyFinalizer]
        [HarmonyPatch(typeof(Player), nameof(Player.UpdateEnvStatusEffects))]
        private static void End() => EnvHooks.End();

        [HarmonyPrefix]
        [HarmonyPatch(typeof(SEMan), nameof(SEMan.AddStatusEffect), new[] { typeof(int), typeof(bool), typeof(int), typeof(float), typeof(short) })]
        private static bool Add(SEMan __instance, int nameHash, ref StatusEffect? __result)
        {
            if (EnvHooks.AllowAdd(__instance, nameHash))
            {
                return true;
            }
            __result = null;
            return false;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(SEMan), nameof(SEMan.RemoveStatusEffect), new[] { typeof(int), typeof(bool) })]
        private static bool Remove(int nameHash, bool quiet, ref bool __result)
        {
            if (EnvHooks.AllowRemove(nameHash, quiet))
            {
                return true;
            }
            __result = false;
            return false;
        }
    }
}
