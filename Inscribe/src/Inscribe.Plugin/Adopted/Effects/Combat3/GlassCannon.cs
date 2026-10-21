// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
using HarmonyLib;
using UnityEngine;

namespace ExtraPotions.Inscribe.Adopted.Effects.Combat3
{
    /// <summary>
    /// <c>glass_cannon</c> (Glass Cannon), player-global, one value X for both sides: the local player's hits deal X% more
    /// of every damage type (folded into the aggregate's damage-dealt table on rebuild, exactly as
    /// <c>damage_dealt:all</c>, applied by its ModifyAttack on the attacker's client while each hit is built), and the
    /// player's body armour is X% lower. The armour is the number the game computes for the player (Player.GetBodyArmor,
    /// after the status effects' armour changes): the victim's own client applies it to every hit it takes, and the
    /// inventory panel and tooltips read the same number, so what the player sees is what protects them. Local player
    /// only; another player's armour is computed on their own client.
    /// </summary>
    [HarmonyPatch(typeof(Player), nameof(Player.GetBodyArmor))]
    internal static class GlassCannonArmorPatch
    {
        private static void Postfix(Player __instance, ref float __result)
        {
            float loss = AggregateHost.Current[EffectKind.GlassCannon];
            if (loss > 0f && ReferenceEquals(__instance, Player.m_localPlayer))
            {
                __result *= Mathf.Max(0f, 1f - loss);
            }
        }
    }
}
