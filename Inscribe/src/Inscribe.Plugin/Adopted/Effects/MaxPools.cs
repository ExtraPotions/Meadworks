// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.11.0 at a0e33b9e74be2141e6ef38909d1d77de482cd7ac.
// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
using System;
using HarmonyLib;

namespace ExtraPotions.Inscribe.Adopted.Effects
{
    /// <summary>
    /// Flat maximum health, stamina and eitr (effects <c>max_health</c>, <c>max_stamina</c>, <c>max_eitr</c>, plus Stout
    /// Heart's health and Restless Mind's percentage eitr loss, applied after the flat eitr): added to
    /// the food totals in a postfix on <c>Player.GetTotalFoodValue</c>, which the game calls once per food tick and turns
    /// into the maximum pools. Local player only (the pools of a player are computed on its own client).
    /// </summary>
    internal static class MaxPools
    {
        /// <summary>The totals the pools depend on, taken before a rebuild.</summary>
        public readonly struct Snapshot
        {
            public Snapshot(float health, float stamina, float eitr, float eitrLoss)
            {
                Health = health;
                Stamina = stamina;
                Eitr = eitr;
                EitrLoss = eitrLoss;
            }

            public float Health { get; }
            public float Stamina { get; }
            public float Eitr { get; }
            public float EitrLoss { get; }

            public bool Same(Snapshot other) =>
                Health == other.Health && Stamina == other.Stamina && Eitr == other.Eitr && EitrLoss == other.EitrLoss;
        }

        public static Snapshot Take()
        {
            AggregateValues v = AggregateBuilder.Values;
            return new Snapshot(v[EffectKind.MaxHealth], v[EffectKind.MaxStamina], v[EffectKind.MaxEitr], v[EffectKind.EitrForRegen]);
        }

        /// <summary>After a rebuild: resize the pools now instead of on the next food tick, only when a total changed.</summary>
        public static void RefreshIfChanged(Player player, Snapshot before)
        {
            if (Take().Same(before) || player.IsDead())
            {
                return;
            }
            // The same three calls the game's food tick makes, without ticking the foods down (UpdateFood(0, true) would).
            player.GetTotalFoodValue(out float hp, out float st, out float ei);
            player.SetMaxHealth(hp, true);
            player.SetMaxStamina(st, true);
            player.SetMaxEitr(ei, true);
        }

        internal static void AddTo(Player player, ref float hp, ref float stamina, ref float eitr)
        {
            if (!ReferenceEquals(player, Player.m_localPlayer) || !ItemEffects.Enabled)
            {
                return;
            }
            FoodValues.AddTo(player, ref hp, ref stamina, ref eitr);
            AggregateValues v = AggregateBuilder.Values;
            hp += v[EffectKind.MaxHealth];
            stamina += v[EffectKind.MaxStamina];
            eitr = (eitr + v[EffectKind.MaxEitr]) * Math.Max(0f, 1f - v[EffectKind.EitrForRegen]);
        }
    }

    [HarmonyPatch(typeof(Player), nameof(Player.GetTotalFoodValue))]
    internal static class MaxPoolsPatch
    {
        // Runs where the player's food ticks run: its own client. Hearty Appetite first, then the pool bonuses.
        private static void Postfix(Player __instance, ref float hp, ref float stamina, ref float eitr)
        {
            MaxPools.AddTo(__instance, ref hp, ref stamina, ref eitr);
        }
    }
}
