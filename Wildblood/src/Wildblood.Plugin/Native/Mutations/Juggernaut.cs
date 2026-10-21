// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using ExtraPotions.Wildblood.Adopted.Runtime;
using ExtraPotions.Wildblood.Adopted.Traits;
using UnityEngine;

namespace ExtraPotions.Wildblood.Adopted.Mutations
{
    /// <summary>
    /// Juggernaut: nothing stops it. It never staggers - not from the stagger its hits build up, not from a parry, not
    /// from a hit or a trap that staggers outright - and nothing knocks it back, a parry's deflection included. The game
    /// has no other "stun" for a creature: frost and tar slow it, they do not stop it, and are left alone. Event-shaped
    /// like Warding, so no component: three patches ask <see cref="Holds"/> where the game decides, on the creature's
    /// owner (every stagger, wherever it was asked for, is carried out by the owner's RPC_Stagger; the stagger meter and
    /// the push live on the owner's copy). The player's side of a parry is the game's own - stamina, adrenaline, the
    /// parry flash - so to explain the missing stumble the creature shows "Unstoppable" instead, sent the way a damage
    /// number is to everyone within 30 m. No settings: it either holds or it does not.
    /// </summary>
    public static class Juggernaut
    {
        /// <summary>The tell, in the colour the game gives an immune hit (grey, like the mutation's own iron grey).</summary>
        private const string Tell = "Unstoppable";

        /// <summary>
        /// The game's mark for a hit that staggers outright (RPC_Damage staggers at once from 100 up); such a hit has
        /// already shown the tell through the stagger it asked for, so its meter does not show it a second time.
        /// </summary>
        private const float OutrightStagger = 100f;

        /// <summary>True for a resolved creature carrying Juggernaut; players never do.</summary>
        public static bool Holds(Character? character)
        {
            if (character == null || character.IsPlayer())
            {
                return false;
            }
            EliteController controller = character.GetComponent<EliteController>();
            return controller != null && controller.Ready && controller.Traits.Has(Mutation.Juggernaut);
        }

        /// <summary>
        /// The game's own stagger meter run without the stagger: the hit's stagger fills it as usual (status effects
        /// still modify it), and where the creature would have staggered the meter empties and the tell shows instead.
        /// Nothing else follows, so the attacker earns no stagger adrenaline: there was no stagger.
        /// </summary>
        public static void Absorb(Character creature, float damage, HitData? hit)
        {
            if (creature.m_staggerDamageFactor <= 0f)
            {
                return; // a creature the game never staggers through its meter
            }
            creature.m_seman.ModifyStagger(damage, ref damage);
            creature.m_staggerDamage += damage;
            if (creature.m_staggerDamage < creature.GetStaggerTreshold())
            {
                return;
            }
            creature.m_staggerDamage = 0f;
            if (hit == null || hit.m_staggerMultiplier < OutrightStagger)
            {
                Shrug(creature);
            }
        }

        /// <summary>Shows the tell over the creature on every machine near it, from whichever machine shrugged.</summary>
        public static void Shrug(Character creature)
        {
            DamageText text = DamageText.instance;
            if (text != null && ZRoutedRpc.instance != null)
            {
                text.ShowText(DamageText.TextType.Immune, creature.GetTopPoint(), Tell);
            }
        }

        /// <summary>
        /// True when the push is the recoil of the attack it is making itself (an attack's <c>m_recoilPushback</c>,
        /// straight back from its facing): its own move, not a knockback, so it keeps it and fights as its kind does.
        /// </summary>
        public static bool OwnRecoil(Character creature, Vector3 dir, float force)
        {
            Attack? attack = creature is Humanoid humanoid ? humanoid.m_currentAttack : null;
            return attack != null && attack.m_recoilPushback != 0f && force == attack.m_recoilPushback
                && dir == -creature.transform.forward;
        }
    }
}
