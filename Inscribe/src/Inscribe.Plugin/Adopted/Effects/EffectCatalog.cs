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
    /// The registered effects of this build: the 34 Phase 1 ids of affixes.md section 6 (this file) and the Phase 2 ids
    /// (EffectCatalog.Phase2.cs, plus the four loot-find ids Loot applies), with the fields of the
    /// registry table in affixes.md section 3 (which is authoritative). Owned by the effects area: add a line here when
    /// an effect is built. A registered id is a promise that the code implements it.
    /// </summary>
    internal static partial class EffectCatalog
    {
        public static void RegisterAll()
        {
            RegisterRegen();
            RegisterMovement();
            RegisterStaminaCosts();
            RegisterSkillsAndCombat();
            RegisterPools();
            RegisterItemLocal();
            RegisterWeaponCosts();
            RegisterFieldWrites();
            RegisterLootFind();
            RegisterPhase2();
            RegisterPhase3();
        }

        private static void Add(string id, EffectRoute route, EffectScope scope, ValueTypes values, EffectParamKind param,
            EffectPolarity polarity, float? cap, string description)
        {
            EffectRegistry.Register(new EffectDef(id, route, scope, values, param, polarity, cap, HookDifficulty.Easy, 1,
                description));
        }

        private static void Add(string id, EffectRoute route, EffectScope scope, ValueTypes values, EffectParamKind param,
            EffectPolarity polarity, float? cap, HookDifficulty hook, string description)
        {
            EffectRegistry.Register(new EffectDef(id, route, scope, values, param, polarity, cap, hook, 2, description));
        }

        private static void RegisterRegen()
        {
            Add("health_recovery", Aggregate, PlayerGlobal, ValueTypes.Percent, None, Raise, 150, "aggregate SE: health regeneration multiplier");
            Add("stamina_recovery", Aggregate, PlayerGlobal, ValueTypes.Percent, None, Raise, 100, "aggregate SE: stamina regeneration multiplier");
            Add("eitr_recovery", Aggregate, PlayerGlobal, ValueTypes.Percent, None, Raise, 100, "aggregate SE: eitr regeneration multiplier");
        }

        private static void RegisterMovement()
        {
            Add("move_speed", Aggregate, PlayerGlobal, ValueTypes.Percent, None, Raise, 25, "aggregate SE: all ground movement");
            Add("move_speed_sneak", Aggregate, PlayerGlobal, ValueTypes.Percent, None, Raise, 50, "aggregate SE: movement while crouched");
            Add("jump_height", Aggregate, PlayerGlobal, ValueTypes.Percent, None, Raise, 50, "aggregate SE: jump modifier");
            Add("fall_damage_taken", Aggregate, PlayerGlobal, ValueTypes.Percent, None, Lower, 80, "aggregate SE: fall damage modifier");
            Add("carry_capacity", Aggregate, PlayerGlobal, ValueTypes.Flat, None, Raise, null, "aggregate SE: max carry weight");
        }

        private static void RegisterStaminaCosts()
        {
            Add("run_stamina_cost", Aggregate, PlayerGlobal, ValueTypes.Percent, None, Lower, 60, "aggregate SE: run stamina drain");
            Add("jump_stamina_cost", Aggregate, PlayerGlobal, ValueTypes.Percent, None, Lower, 60, "aggregate SE: jump stamina");
            Add("attack_stamina_cost", Aggregate, PlayerGlobal, ValueTypes.Percent, None, Lower, 60, "aggregate SE: attack stamina");
            Add("block_stamina_cost", Aggregate, PlayerGlobal, ValueTypes.Percent, None, Lower, 60, "aggregate SE: block stamina");
            Add("dodge_stamina_cost", Aggregate, PlayerGlobal, ValueTypes.Percent, None, Lower, 60, "aggregate SE: dodge stamina");
            Add("home_item_stamina_cost", Aggregate, PlayerGlobal, ValueTypes.Percent, None, Lower, 60, "aggregate SE: build/repair/till stamina");
        }

        private static void RegisterSkillsAndCombat()
        {
            Add("skill_level", Aggregate, PlayerGlobal, ValueTypes.Flat, Skill, Raise, null, "aggregate SE override ModifySkillLevel");
            Add("skill_gain", Aggregate, PlayerGlobal, ValueTypes.Percent, Skill, Raise, 100, "aggregate SE override ModifyRaiseSkill");
            Add("parry_bonus", Aggregate, PlayerGlobal, ValueTypes.Percent, None, Raise, null, "aggregate SE: timed block bonus");
            Add("damage_dealt", Aggregate, PlayerGlobal, ValueTypes.Percent, DamageType, Raise, null, "aggregate SE ModifyAttack: scale a damage group");
            Add("night_damage", Aggregate, PlayerGlobal, ValueTypes.Percent, None, Raise, null, "aggregate SE ModifyAttack while it is night");
        }

        private static void RegisterPools()
        {
            Add("max_health", Hook, PlayerGlobal, ValueTypes.Flat, None, Raise, null, "postfix Player.GetTotalFoodValue (hp)");
            Add("max_stamina", Hook, PlayerGlobal, ValueTypes.Flat, None, Raise, null, "postfix Player.GetTotalFoodValue (stamina)");
            Add("max_eitr", Hook, PlayerGlobal, ValueTypes.Flat, None, Raise, null, "postfix Player.GetTotalFoodValue (eitr)");
        }

        private static void RegisterItemLocal()
        {
            Add("item_armor", Hook, ItemLocal, ValueTypes.Percent, None, Raise, null, "postfix ItemData.GetArmor on this item");
            Add("item_block", Hook, ItemLocal, ValueTypes.Percent, None, Raise, null, "postfix ItemData.GetBaseBlockPower on this item");
            Add("item_deflection", Hook, ItemLocal, ValueTypes.Percent, None, Raise, null, "postfix ItemData.GetDeflectionForce on this item");
            Add("item_durability", Hook, ItemLocal, ValueTypes.Percent, None, Raise, null, "postfix ItemData.GetMaxDurability on this item");
            Add("item_lighten", Hook, ItemLocal, ValueTypes.Percent, None, Lower, null, "postfix ItemData.GetWeight on this item");
            Add("brand_damage", Hook, ItemLocal, ValueTypes.Percent, DamageType, Raise, null, "postfix ItemData.GetDamage: add X% of base damage as a type");
        }

        // "this bow / this staff / this crossbow": item-local, the cap applies to the item's own sum.
        private static void RegisterWeaponCosts()
        {
            Add("attack_eitr_cost", Hook, ItemLocal, ValueTypes.Percent, None, Lower, 60, "postfix Attack.GetAttackEitr on the weapon");
            Add("draw_stamina_cost", Hook, ItemLocal, ValueTypes.Percent, None, Lower, 60, "postfix ItemData.GetDrawStaminaDrain on this bow");
            Add("reload_speed", Hook, ItemLocal, ValueTypes.Percent, None, Raise, 50, "postfix ItemData.GetWeaponLoadingTime on this crossbow");
        }

        private static void RegisterFieldWrites()
        {
            Add("pickup_radius", Hook, PlayerGlobal, ValueTypes.Percent, None, Raise, 100, "Player.m_autoPickupRange written on rebuild");
            Add("build_range", Hook, PlayerGlobal, ValueTypes.Percent, None, Raise, 100, "Player.m_maxPlaceDistance written on rebuild while the tool is in hand");
            Add("explore_radius", Hook, PlayerGlobal, ValueTypes.Percent, None, Raise, 100, "Minimap.m_exploreRadius written on rebuild");
        }

        // Loot owns these end to end (publisher on the killer's client, read on the creature's owner); Effects only
        // registers them so the YAML accepts their affixes. Their kinds are no-ops in the aggregate and the item hooks.
        private static void RegisterLootFind()
        {
            Add("find_rarity", Hook, PlayerGlobal, ValueTypes.Percent, None, Raise, 200, M, "loot: killer's rarity find (Norns' Favor), applied by Loot");
            Add("find_stones", Hook, PlayerGlobal, ValueTypes.Percent, None, Raise, 200, M, "loot: killer's stone find (Fateweaver), applied by Loot");
            Add("find_trophy", Hook, PlayerGlobal, ValueTypes.Percent, None, Raise, 200, M, "loot: killer's trophy find (Trophy Taker), applied by Loot");
            Add("find_coins", Hook, PlayerGlobal, ValueTypes.Percent, None, Raise, 200, M, "loot: killer's coin find (Hoardfinder), applied by Loot");
        }
    }
}
