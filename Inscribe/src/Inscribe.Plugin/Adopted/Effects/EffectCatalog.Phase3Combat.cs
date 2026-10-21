// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
using static ExtraPotions.Inscribe.Adopted.Effects.EffectParamKind;
using static ExtraPotions.Inscribe.Adopted.Effects.EffectPolarity;
using static ExtraPotions.Inscribe.Adopted.Effects.EffectRoute;
using static ExtraPotions.Inscribe.Adopted.Effects.EffectScope;

namespace ExtraPotions.Inscribe.Adopted.Effects
{
    /// <summary>
    /// The Phase 3 combat effects (features/effects-phase3.md, table section "Combat"). The code lives in
    /// <c>Effects/Combat3</c>: weapon numbers and per-swing changes on the attacker's client, on-hit effects carried by the
    /// hit or dealt as the attacker's own secondary hits, and what only the target's owner can apply (paralysis,
    /// resistance bypass, longer damage over time) read there from the hit or the attacker's player ZDO.
    /// </summary>
    internal static partial class EffectCatalog
    {
        static partial void RegisterPhase3Combat()
        {
            RegisterCombatWeapon();
            RegisterCombatOnHit();
            RegisterCombatPlayer();
        }

        // "This weapon": item-local, the cap applies to the item's own sum.
        private static void RegisterCombatWeapon()
        {
            Add("added_damage", Hook, ItemLocal, ValueTypes.Flat, DamageType, Raise, null, E, "postfix ItemData.GetDamage: X more of the param type, after quality");
            Add("attack_speed", Hook, ItemLocal, ValueTypes.Percent, None, Raise, 15, M, "this weapon's swing animation (owner's animator, synced) and burst timing X% faster");
            Add("cast_speed", Hook, ItemLocal, ValueTypes.Percent, None, Raise, 25, M, "this staff's cast animation (owner's animator, synced) and burst timing X% faster");
            Add("knockback_dealt", Hook, ItemLocal, ValueTypes.Percent, None, Raise, 100, E, "attacker-side Character.Damage: this weapon's push force +X%");
            Add("heavy_hand", Hook, ItemLocal, ValueTypes.Percent, None, Raise, 60, M, "trade-off: this weapon's hits stagger +X% (attacker side), its swing X/3 % slower (synced animator)");
        }

        private static void RegisterCombatOnHit()
        {
            Add("chain_lightning", Hook, ItemLocal, ValueTypes.Percent, None, Raise, 100, M, "15% per hit: X% of the hit as lightning to 3 more enemies within 8 m (attacker's hits, networked visual)");
            Add("explosive_shot", Hook, ItemLocal, ValueTypes.Percent, None, Raise, 100, M, "postfix Projectile.OnHit: X% of the shot to every other enemy within 3 m (networked visual)");
            Add("paralyze", Hook, ItemLocal, ValueTypes.Flat, None, Raise, 3, M, "status effect ECF_Paralyze carried by the hit, X s, applied by the target's owner; not bosses or players");
            Add("penetration", Hook, ItemLocal, ValueTypes.Percent, None, Raise, 50, M, "target's owner: X% of each resistance bypassed (attacker's ZDO value ecf_pen, set per swing)");
        }

        private static void RegisterCombatPlayer()
        {
            Add("crit_chance", Hook, PlayerGlobal, ValueTypes.Percent, None, Raise, 25, E, "attacker-side Character.Damage: X% chance a hit is critical");
            Add("crit_damage", Hook, PlayerGlobal, ValueTypes.Percent, None, Raise, 100, E, "a critical hit deals x(1.5 + X/100)");
            Add("dot_duration", Hook, PlayerGlobal, ValueTypes.Percent, None, Raise, 100, M, "target's owner: burning, poison and frost from you last X% longer (player ZDO ecf_dot)");
            Add("glass_cannon", Hook, PlayerGlobal, ValueTypes.Percent, None, Raise, 25, E, "trade-off: +X% all damage dealt (aggregate ModifyAttack), body armor -X% (postfix Player.GetBodyArmor)");
        }
    }
}
