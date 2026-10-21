// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using System.Globalization;
using ExtraPotions.Wildblood.Adopted.Runtime;
using ExtraPotions.Wildblood.Adopted.Rules;
using ExtraPotions.Wildblood.Adopted.Scaling;
using ExtraPotions.Wildblood.Adopted.Traits;

namespace ExtraPotions.Wildblood.Adopted.Mutations
{
    /// <summary>
    /// Frostbound drinks frost: the frost in a hit on it never hurts it, and heals it by `frost heal` percent of that
    /// frost instead; the game's Frost slow never takes hold on it. Decided on the creature's owner, where the game
    /// resolves every hit (<c>FrostHealPatch</c>): before the hit, the frost is taken out of it - its frost damage and a
    /// Frost status it carries - and measured as it arrives, before the creature's own frost resistance, so a creature
    /// immune to frost drinks it as readily as one weak to it and the heal is the frost the attacker sent. Once the hit has
    /// landed, the creature heals and every client near it sees the game's green heal number, "+30 frost", where the
    /// frost would have shown as damage - even at full health, so a player always sees why their frost did nothing. A
    /// hit the game ignores (a dead creature, a teleport, an attacker no longer there) is left alone. Event-shaped: no
    /// component.
    /// </summary>
    public static class FrostHeal
    {
        /// <summary>
        /// Owner side, before the hit: takes the frost out of a hit on a Frostbound creature and returns the health it
        /// will heal once the hit has landed, or 0 when there is nothing to heal.
        /// </summary>
        public static float Strip(Character victim, HitData hit)
        {
            EliteController? controller = Frostbound(victim);
            if (controller == null || !Lands(victim, hit))
            {
                return 0f;
            }
            if (hit.m_statusEffectHash == SEMan.s_statusEffectFrost)
            {
                hit.m_statusEffectHash = 0; // a frost weapon's slow, refused outright
            }
            float frost = hit.m_damage.m_frost;
            hit.m_damage.m_frost = 0f; // no frost damage, and so no Frost slow from it either
            float percent = Enhance.Magnitude(controller.Rules, controller.Traits, Mutation.Frostbound, Fields.FrostHeal);
            return frost > 0f && percent > 0f ? frost * percent / 100f : 0f;
        }

        /// <summary>Owner side, once the hit has landed: heals the creature, unless the hit killed it, and shows it.</summary>
        public static void Feed(Character victim, float heal)
        {
            if (heal <= 0f || victim.IsDead() || victim.GetHealth() <= 0f)
            {
                return;
            }
            victim.Heal(heal, showText: false); // the number below says what healed it
            DamageText text = DamageText.instance;
            if (text != null && ZRoutedRpc.instance != null)
            {
                text.ShowText(DamageText.TextType.Heal, victim.GetTopPoint(),
                    heal.ToString("0.#", CultureInfo.InvariantCulture) + " frost");
            }
        }

        private static EliteController? Frostbound(Character victim)
        {
            if (victim == null || victim.IsPlayer())
            {
                return null;
            }
            EliteController controller = victim.GetComponent<EliteController>();
            return controller != null && controller.Ready && controller.Traits.Has(Mutation.Frostbound) ? controller : null;
        }

        /// <summary>The game's own reasons, in RPC_Damage, to ignore a hit on a creature before it does anything.</summary>
        private static bool Lands(Character victim, HitData hit) =>
            !victim.IsDead() && victim.GetHealth() > 0f && !victim.IsTeleporting() && !victim.InCutscene()
            && !(hit.m_dodgeable && victim.IsDodgeInvincible()) && !(hit.HaveAttacker() && hit.GetAttacker() == null);
    }
}
