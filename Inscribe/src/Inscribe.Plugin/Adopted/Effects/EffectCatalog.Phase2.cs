// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.11.0 at a0e33b9e74be2141e6ef38909d1d77de482cd7ac.
// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
using ExtraPotions.Inscribe.Adopted.Rules;
using static ExtraPotions.Inscribe.Adopted.Effects.EffectParamKind;
using static ExtraPotions.Inscribe.Adopted.Effects.EffectPolarity;
using static ExtraPotions.Inscribe.Adopted.Effects.EffectRoute;
using static ExtraPotions.Inscribe.Adopted.Effects.EffectScope;

namespace ExtraPotions.Inscribe.Adopted.Effects
{
    /// <summary>
    /// The Phase 2 effects of affixes.md section 3: every easy and medium effect that is not a
    /// loot-find effect (those are Loot's, registered in <see cref="EffectCatalog"/>). Fields as the registry table;
    /// scope is ours: "this weapon / this shield / this item" is item-local, everything else sums per player.
    /// </summary>
    internal static partial class EffectCatalog
    {
        private const HookDifficulty E = HookDifficulty.Easy;
        private const HookDifficulty M = HookDifficulty.Medium;

        private static void RegisterPhase2()
        {
            RegisterAggregateP2();
            RegisterMovementP2();
            RegisterDefenseP2();
            RegisterOffenseP2();
            RegisterSustainP2();
            RegisterUtilityP2();
            RegisterSharedStatsP2();
            RegisterItemLocalP2();
            RegisterWeaponLocalP2();
        }

        private static void RegisterAggregateP2()
        {
            Add("noise_made", Aggregate, PlayerGlobal, ValueTypes.Percent, None, Lower, 75, E, "aggregate SE: noise modifier");
            Add("stealth", Aggregate, PlayerGlobal, ValueTypes.Percent, None, Raise, 75, E, "aggregate SE: stealth modifier (harder to see while sneaking)");
            Add("stagger_taken", Aggregate, PlayerGlobal, ValueTypes.Percent, None, Lower, 75, E, "aggregate SE: stagger modifier");
            Add("surprise_bonus", Aggregate, PlayerGlobal, ValueTypes.Percent, None, Raise, null, E, "aggregate SE ModifyAttack: sneak-attack multiplier");
            Add("stagger_power", Aggregate, PlayerGlobal, ValueTypes.Percent, None, Raise, null, E, "aggregate SE ModifyAttack: stagger multiplier of your hits");
            Add("coin_damage", Aggregate, PlayerGlobal, ValueTypes.Percent, None, Raise, null, E, "aggregate SE ModifyAttack: per 999 coins carried");
            Add("resist_modifier", Aggregate, PlayerGlobal, ValueTypes.Flag, Element, Raise, null, E, "aggregate SE ModifyDamageMods: Resistant to the element");
            Add("freeze_immunity", Aggregate, PlayerGlobal, ValueTypes.Flag, None, Raise, null, E, "never Freezing: the environment update's Freezing becomes Cold");
            Add("health_recovery_flat", Aggregate, PlayerGlobal, ValueTypes.Flat, None, Raise, null, E, "aggregate SE tick: heal X every 10 s");
            Add("health_for_regen", Hook, PlayerGlobal, ValueTypes.Flat, None, Raise, null, E, "composite: +X max health, -X% health regeneration");
            Add("eitr_for_regen", Hook, PlayerGlobal, ValueTypes.Percent, None, Raise, null, E, "composite: +X% eitr regeneration, -X/2 % max eitr");
        }

        private static void RegisterMovementP2()
        {
            Add("move_speed_sprint", Aggregate, PlayerGlobal, ValueTypes.Percent, None, Raise, 30, E, "aggregate SE ModifySpeed while running");
            Add("move_speed_encumbered", Aggregate, PlayerGlobal, ValueTypes.Percent, None, Raise, 50, E, "aggregate SE ModifySpeed while encumbered");
            Add("move_speed_after_dodge", Aggregate, PlayerGlobal, ValueTypes.Percent, None, Raise, 40, E, "aggregate SE ModifySpeed for 5 s after a dodge");
            Add("move_speed_paved", Aggregate, PlayerGlobal, ValueTypes.Percent, None, Raise, 30, M, "aggregate SE ModifySpeed on paved or path terrain paint");
            Add("swimmer", Aggregate, PlayerGlobal, ValueTypes.Percent, None, Raise, 50, E, "aggregate SE: swim speed and swim stamina");
            Add("slow_fall", Aggregate, PlayerGlobal, ValueTypes.Flag, None, Raise, null, E, "aggregate SE: max fall speed, no fall damage");
            Add("slope_penalty", Hook, PlayerGlobal, ValueTypes.Percent, None, Lower, 75, M, "Character.ApplySlide: less slide-back on steep slopes");
            Add("terrain_slow", Hook, PlayerGlobal, ValueTypes.Percent, None, Lower, 75, M, "Character.ApplyLiquidResistance and the Tared slow");
            Add("heat_resist", Hook, PlayerGlobal, ValueTypes.Percent, None, Raise, null, E, "postfix Player.GetEquipmentHeatResistanceModifier");
        }

        private static void RegisterDefenseP2()
        {
            Add("damage_taken", Hook, PlayerGlobal, ValueTypes.Percent, DamageType, Lower, 60, E, "prefix Character.RPC_Damage on the local player: scale the param types");
            Add("ranged_damage_taken", Hook, PlayerGlobal, ValueTypes.Percent, None, Lower, 60, E, "prefix Character.RPC_Damage: projectile hits");
            Add("avoid_hit", Hook, PlayerGlobal, ValueTypes.Percent, None, Raise, 25, E, "prefix Character.RPC_Damage: chance to discard the hit");
            Add("knockback_taken", Hook, PlayerGlobal, ValueTypes.Percent, None, Lower, 75, E, "prefix Character.ApplyPushback on the local player");
            Add("thorns", Hook, PlayerGlobal, ValueTypes.Percent, None, Raise, null, M, "melee damage taken returned to the attacker (damage RPC to its owner)");
            Add("calm_ward", Hook, PlayerGlobal, ValueTypes.Flat, None, Raise, null, M, "ward after 10 s without damage, absorbs the next X");
            Add("debuff_decay", Hook, PlayerGlobal, ValueTypes.Percent, None, Raise, 75, M, "burning, poison and frost on you run out faster");
            Add("frost_slow_taken", Hook, PlayerGlobal, ValueTypes.Percent, None, Lower, 100, M, "SE_Frost.ModifySpeed on the local player");
            Add("stagger_recovery", Hook, PlayerGlobal, ValueTypes.Percent, None, Lower, 75, M, "own stagger animation plays faster");
            Add("combo_finisher", Hook, PlayerGlobal, ValueTypes.Percent, None, Raise, null, M, "third combo hit: 2 s stagger immunity, -X% damage taken");
            Add("dodge_fury", Hook, PlayerGlobal, ValueTypes.Percent, None, Raise, null, M, "dodging through a melee hit: +X% damage for 10 s");
            Add("parry_restore", Hook, PlayerGlobal, ValueTypes.Flat, Resource, Raise, null, M, "perfect block restores X of the resource");
        }

        private static void RegisterOffenseP2()
        {
            Add("slayer", Hook, PlayerGlobal, ValueTypes.Percent, CreatureFamily, Raise, null, E, "attacker-side prefix Character.Damage: target family");
            Add("staggered_target_damage", Hook, PlayerGlobal, ValueTypes.Percent, None, Raise, null, E, "attacker-side prefix Character.Damage: staggered target");
            Add("low_health_opener", Hook, PlayerGlobal, ValueTypes.Percent, None, Raise, null, M, "attacker-side: first hit on a target below 20% health");
            Add("exploit_stagger", Hook, PlayerGlobal, ValueTypes.Percent, None, Raise, null, M, "attacker-side: chance a hit on a staggered target is a sneak attack");
            Add("on_hit_slow", Hook, PlayerGlobal, ValueTypes.Percent, None, Raise, null, M, "own status effect carried by the hit, applied by the target's owner");
            Add("stagger_duration_dealt", Hook, PlayerGlobal, ValueTypes.Percent, None, Raise, null, M, "target's owner slows the stagger animation (player ZDO value)");
            Add("on_kill_restore", Hook, PlayerGlobal, ValueTypes.Flat, Resource, Raise, null, M, "kill attribution: restore X to the killer");
            Add("leech", Hook, PlayerGlobal, ValueTypes.Percent, Resource, Raise, null, M, "attacker-side: restore X% of the outgoing hit");
        }

        private static void RegisterSustainP2()
        {
            Add("mead_burst", Hook, PlayerGlobal, ValueTypes.Flag, None, Raise, null, M, "a healing mead heals at once");
            Add("mead_cooldown", Hook, PlayerGlobal, ValueTypes.Percent, None, Lower, 50, M, "shorter restoring-mead status (the mead cooldown)");
            Add("rain_shield", Hook, PlayerGlobal, ValueTypes.Flag, None, Raise, null, M, "the environment update never applies Wet from rain");
            Add("ignore_wet", Hook, PlayerGlobal, ValueTypes.Flag, None, Raise, null, M, "the Wet status keeps its regeneration at 100%");
            Add("cold_immunity", Hook, PlayerGlobal, ValueTypes.Flag, None, Raise, null, M, "the environment update never applies Cold");
        }

        private static void RegisterUtilityP2()
        {
            Add("rest_comfort", Hook, PlayerGlobal, ValueTypes.Flat, None, Raise, 3, E, "comfort level +X (Player.UpdateBaseValue)");
            Add("skill_loss", Hook, PlayerGlobal, ValueTypes.Percent, None, Lower, 75, E, "prefix Skills.LowerAllSkills: smaller factor");
            Add("food_duration", Hook, PlayerGlobal, ValueTypes.Percent, None, Raise, 100, E, "postfix Player.EatFood: the new food lasts longer");
            Add("forsaken_cooldown", Hook, PlayerGlobal, ValueTypes.Percent, None, Lower, 50, E, "postfix Player.ActivateGuardianPower: shorter cooldown");
        }

        // Read by another peer from the player's own ZDO (PlayerStats): taming on the creature's owner, sailing on the
        // ship's owner, yields on the resource's owner, stagger length on the creature's owner, visuals on every client.
        private static void RegisterSharedStatsP2()
        {
            Add("light_aura", Hook, PlayerGlobal, ValueTypes.Flag, None, Raise, null, M, "a light on the player, drawn by every client");
            Add("demist_radius", Hook, PlayerGlobal, ValueTypes.Percent, None, Raise, 100, M, "the wearer's demister range, on every client");
            Add("taming_speed", Hook, PlayerGlobal, ValueTypes.Percent, None, Raise, 100, M, "prefix Tameable.DecreaseRemainingTime on the creature's owner");
            Add("sail_speed", Hook, PlayerGlobal, ValueTypes.Percent, None, Raise, 50, M, "postfix Ship.GetSailForce for the steering player");
            Add("yield_mining", Hook, PlayerGlobal, ValueTypes.Flat, None, Raise, null, M, "rock broken by your pickaxe hit drops X more (resource owner)");
            Add("yield_lumber", Hook, PlayerGlobal, ValueTypes.Flat, None, Raise, null, M, "tree or log felled by your chop drops X more (resource owner)");
            Add("yield_pickable", Hook, PlayerGlobal, ValueTypes.Percent, None, Raise, 100, M, "prefix Pickable.RPC_Pick: chance of double yield");
        }

        private static void RegisterItemLocalP2()
        {
            Add("item_unbreakable", Hook, ItemLocal, ValueTypes.Flag, None, Raise, null, M, "this item's durability is kept full while equipped");
            Add("item_zero_weight", Hook, ItemLocal, ValueTypes.Flag, None, Raise, null, E, "postfix ItemData.GetWeight: 0");
            Add("item_no_move_penalty", Hook, ItemLocal, ValueTypes.Flag, None, Raise, null, E, "postfix Player.GetEquipmentMovementModifier: refund this item's penalty");
            Add("block_steadfast", Hook, ItemLocal, ValueTypes.Flag, None, Raise, null, M, "blocking with this shield: no knockback or stagger");
            Add("perfect_block_window", Hook, ItemLocal, ValueTypes.Flat, None, Raise, 150, M, "Humanoid.BlockAttack: longer perfect-block window");
            Add("lone_blade", Hook, ItemLocal, ValueTypes.Percent, None, Raise, null, M, "off-hand empty: block and parry force from this weapon");
            Add("summon_damage", Hook, ItemLocal, ValueTypes.Percent, None, Raise, null, M, "creatures this staff summons deal more (tagged at spawn)");
            Add("summon_health", Hook, ItemLocal, ValueTypes.Percent, None, Raise, null, M, "creatures this staff summons have more health");
        }

        private static void RegisterWeaponLocalP2()
        {
            Add("attack_health_cost", Hook, ItemLocal, ValueTypes.Percent, None, Lower, 60, E, "postfix Attack.GetAttackHealth for this staff");
            Add("attack_reach", Hook, ItemLocal, ValueTypes.Percent, None, Raise, null, E, "prefix Attack.Start on the per-swing clone: range");
            Add("attack_arc", Hook, ItemLocal, ValueTypes.Flat, None, Raise, null, E, "prefix Attack.Start on the per-swing clone: angle");
            Add("projectile_velocity", Hook, ItemLocal, ValueTypes.Percent, None, Raise, null, E, "prefix Projectile.Setup: launch velocity");
            Add("draw_speed", Hook, ItemLocal, ValueTypes.Percent, None, Raise, 50, M, "postfix Humanoid.GetAttackDrawPercentage");
            Add("draw_move_penalty", Hook, ItemLocal, ValueTypes.Percent, None, Lower, null, M, "postfix Humanoid.GetAttackSpeedFactorMovement");
            Add("multishot", Hook, ItemLocal, ValueTypes.Flag, None, Raise, null, M, "per-shot clone: three projectiles, three ammo");
            Add("twincast", Hook, ItemLocal, ValueTypes.Flag, None, Raise, null, M, "per-cast clone: projectiles x2, eitr x2");
            Add("ammo_save", Hook, ItemLocal, ValueTypes.Percent, None, Raise, 50, M, "postfix Attack.UseAmmo: chance to refund");
            Add("rune_edge", Hook, ItemLocal, ValueTypes.Percent, None, Raise, null, M, "per-swing clone: half the stamina as eitr, +X% damage");
            Add("blood_price", Hook, ItemLocal, ValueTypes.Flag, None, Raise, null, M, "per-swing clone: stamina cost paid in health");
        }
    }
}
