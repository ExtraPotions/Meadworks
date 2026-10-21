// Portions Copyright (C) 2026 Gerald Glasgow. EliteCreaturesPack 0.7.2 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Changed by expDARE, 2026: Bestiary namespace, owned Crucible settings, rebuilt-only bundles and lifecycle adapters.
// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using ExtraPotions.Bestiary.Runtime.Native.Core;
using HarmonyLib;
using UnityEngine;

namespace ExtraPotions.Bestiary.Runtime.Native.Kraken
{
    /// <summary>
    /// The Kraken shield bites back: a creature whose blow is parried with it takes <c>Shield Parry Damage</c> pierce,
    /// more at each quality level (<see cref="KrakenSettings.ParryDamage"/>), from the parrying player through the game's
    /// own damage, so the creature's resistances, hit effects and death apply and the hit reaches whichever machine owns
    /// it. Melee or ranged, any creature; never a player. A kraken's own blows parried with its beak bite it too.
    /// <para>
    /// Decided where the game decides the parry: in <c>Humanoid.BlockAttack</c>, on the parrying player's own machine.
    /// A parry is the game's own: the shield raised within its parry window, facing the blow, with stamina left and not
    /// staggered by it. The game clears the hit's status effect exactly when such a block holds, so for a kraken shield
    /// raised within its window the prefix marks that slot, and the postfix reads whether the game cleared it, putting
    /// the hit's own effect back when it did not.
    /// </para>
    /// </summary>
    [HarmonyPatch(typeof(Humanoid), nameof(Humanoid.BlockAttack))]
    public static class ParryBite
    {
        private const int Mark = 0x6B72616B;
        private const float ParryWindow = 0.25f;   // the game's: seconds after the shield goes up

        public struct Watch
        {
            public bool On;
            public int Effect;
            public int Quality;
        }

        [HarmonyPrefix]
        private static void Before(Humanoid __instance, HitData hit, Character attacker, float ___m_blockTimer, out Watch __state)
        {
            __state = default;
            ItemDrop.ItemData? shield = __instance.GetCurrentBlocker();
            if (attacker == null || attacker.IsPlayer() || shield == null || shield.m_shared.m_name != KrakenShield.Name)
            {
                return;
            }
            if (shield.m_shared.m_timedBlockBonus > 1f && ___m_blockTimer != -1f && ___m_blockTimer < ParryWindow)
            {
                __state = new Watch { On = true, Effect = hit.m_statusEffectHash, Quality = shield.m_quality };
                hit.m_statusEffectHash = Mark;
            }
        }

        [HarmonyPostfix]
        private static void After(Humanoid __instance, HitData hit, Character attacker, Watch __state)
        {
            if (!__state.On)
            {
                return;
            }
            if (hit.m_statusEffectHash != 0)
            {
                hit.m_statusEffectHash = __state.Effect;
                return;
            }
            SafeCall.Run("kraken shield parry", static (defender, foe, quality) => Bite(defender, foe, quality), __instance, attacker, __state.Quality);
        }

        /// <summary>If the game threw inside, the hit gets its own status effect back.</summary>
        [HarmonyFinalizer]
        private static Exception? Finally(Exception? __exception, HitData hit, Watch __state)
        {
            if (__state.On && hit.m_statusEffectHash == Mark)
            {
                hit.m_statusEffectHash = __state.Effect;
            }
            return __exception;
        }

        private static void Bite(Humanoid player, Character creature, int quality)
        {
            if (creature.IsDead())
            {
                return;
            }
            Vector3 away = creature.transform.position - player.transform.position;
            away.y = 0f;
            var bite = new HitData
            {
                m_point = creature.GetCenterPoint(), m_dir = away.sqrMagnitude > 1e-4f ? away.normalized : player.transform.forward,
                m_hitType = HitData.HitType.PlayerHit, m_skill = Skills.SkillType.Blocking, m_blockable = false, m_dodgeable = false,
            };
            bite.m_damage.m_pierce = KrakenSettings.ParryDamage(quality);
            bite.SetAttacker(player);
            creature.Damage(bite);
        }
    }
}
