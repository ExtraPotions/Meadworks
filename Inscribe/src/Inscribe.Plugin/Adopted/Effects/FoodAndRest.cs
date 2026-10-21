// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.11.0 at a0e33b9e74be2141e6ef38909d1d77de482cd7ac.
// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
using HarmonyLib;

namespace ExtraPotions.Inscribe.Adopted.Effects
{
    // Food and rest affixes on the local player's own bookkeeping: the game ticks a player's foods, regeneration and
    // Rested on the machine that owns that player (its maximum pools reach everyone through its ZDO, as in vanilla),
    // and each patch filters to Player.m_localPlayer. Nothing is sent.

    /// <summary>
    /// Hearty Appetite (<c>food_values</c>): the foods' part of maximum health, stamina and eitr is X% larger (the base
    /// values are not). Called first by <see cref="MaxPoolsPatch"/> (the one GetTotalFoodValue postfix), so Restless
    /// Mind's eitr loss applies to the whole. A change shows at the next food tick, within a second.
    /// </summary>
    internal static class FoodValues
    {
        /// <summary>The local player's foods, effects on (tested by the caller).</summary>
        public static void AddTo(Player player, ref float hp, ref float stamina, ref float eitr)
        {
            float more = AggregateBuilder.Values[EffectKind.FoodValues];
            if (more <= 0f)
            {
                return;
            }
            foreach (Player.Food food in player.m_foods)
            {
                hp += food.m_health * more;
                stamina += food.m_stamina * more;
                eitr += food.m_eitr * more;
            }
        }
    }

    /// <summary>
    /// Well Fed (<c>food_regen</c>): the food health tick (every 10 s, the foods' regeneration times the status effects'
    /// multiplier, Player.UpdateFood) is X% larger, on top of everything else. That tick is the only caller of
    /// SEMan.ModifyHealthRegen in the game.
    /// </summary>
    [HarmonyPatch(typeof(SEMan), nameof(SEMan.ModifyHealthRegen))]
    internal static class FoodRegenPatch
    {
        private static void Postfix(SEMan __instance, ref float regenMultiplier)
        {
            float more = AggregateHost.Current[EffectKind.FoodRegen];
            if (more > 0f && ReferenceEquals(__instance.m_character, Player.m_localPlayer))
            {
                regenMultiplier *= 1f + more;
            }
        }
    }

    /// <summary>
    /// Deep Rest (<c>rested_duration</c>): Rested lasts X% longer. The game sets Rested's time from its base time and
    /// time per comfort level when it is added and each time it is refreshed (SE_Rested.UpdateTTL, from Setup and
    /// ResetTime); both values of the player's own clone are set from the ObjectDB template times 1 + X just before,
    /// so the refresh while resting tops up to the longer time and the game's own rules do the rest.
    /// </summary>
    internal static class RestedDuration
    {
        private static ObjectDB? _db;
        private static SE_Rested? _template;

        public static void Apply(SE_Rested rested, Character? character)
        {
            SE_Rested? template = character != null && ReferenceEquals(character, Player.m_localPlayer) ? Template(rested) : null;
            if (template == null || ReferenceEquals(template, rested))
            {
                return;
            }
            float longer = 1f + AggregateHost.Current[EffectKind.RestedDuration];
            rested.m_baseTTL = template.m_baseTTL * longer;
            rested.m_TTLPerComfortLevel = template.m_TTLPerComfortLevel * longer;
        }

        // ObjectDB's lookup walks every status effect; Rested is refreshed every few seconds while resting, so the
        // template is kept until ObjectDB changes (a new world loads a new one).
        private static SE_Rested? Template(SE_Rested rested)
        {
            ObjectDB? db = ObjectDB.instance;
            if (!ReferenceEquals(db, _db) || _template == null)
            {
                _db = db;
                _template = db != null ? db.GetStatusEffect(rested.NameHash()) as SE_Rested : null;
            }
            return _template;
        }
    }

    [HarmonyPatch]
    internal static class RestedDurationPatches
    {
        [HarmonyPrefix]
        [HarmonyPatch(typeof(SE_Rested), nameof(SE_Rested.Setup))]
        private static void Setup(SE_Rested __instance, Character character) => RestedDuration.Apply(__instance, character);

        [HarmonyPrefix]
        [HarmonyPatch(typeof(SE_Rested), nameof(SE_Rested.ResetTime))]
        private static void ResetTime(SE_Rested __instance) => RestedDuration.Apply(__instance, __instance.m_character);
    }
}
