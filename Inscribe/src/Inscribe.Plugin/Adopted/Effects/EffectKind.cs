// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.11.0 at a0e33b9e74be2141e6ef38909d1d77de482cd7ac.
// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
using System;
using System.Collections.Generic;
using ExtraPotions.Inscribe.Adopted.Core;
using ExtraPotions.Inscribe.Adopted.Rules;

namespace ExtraPotions.Inscribe.Adopted.Effects
{
    /// <summary>
    /// What the code does with a channel, one value per registered effect id. Resolved once per rules generation into
    /// <see cref="ChannelPlan"/>, so no rebuild and no hook ever compares effect ids as strings.
    /// </summary>
    internal enum EffectKind
    {
        Unknown,

        // aggregate status effect: native SE_Stats fields
        MoveSpeed,
        CarryCapacity,
        HealthRecovery,
        StaminaRecovery,
        EitrRecovery,
        RunStaminaCost,
        JumpStaminaCost,
        AttackStaminaCost,
        BlockStaminaCost,
        DodgeStaminaCost,
        HomeItemStaminaCost,
        FallDamageTaken,
        JumpHeight,
        ParryBonus,
        NoiseMade,
        Stealth,
        StaggerTaken,
        Swimmer,
        SlowFall,

        // aggregate status effect: our own overrides
        MoveSpeedSneak,
        SkillLevel,
        SkillGain,
        DamageDealt,
        NightDamage,
        MoveSpeedSprint,
        MoveSpeedEncumbered,
        MoveSpeedAfterDodge,
        MoveSpeedPaved,
        SurpriseBonus,
        StaggerPower,
        CoinDamage,
        ResistModifier,
        HealthRecoveryFlat,

        // player-global hooks and field writes (summed with the aggregate, applied elsewhere)
        MaxHealth,
        MaxStamina,
        MaxEitr,
        PickupRadius,
        BuildRange,
        ExploreRadius,
        HealthForRegen,
        EitrForRegen,

        // player-global hooks, Phase 2: the local player's own client (Effects/*Hooks, *Patches)
        SlopePenalty,
        TerrainSlow,
        HeatResist,
        RestComfort,
        SkillLoss,
        FoodDuration,
        ForsakenCooldown,
        DamageTaken,
        RangedDamageTaken,
        AvoidHit,
        KnockbackTaken,
        Slayer,
        StaggeredTargetDamage,
        LowHealthOpener,
        ExploitStagger,
        OnHitSlow,
        ComboFinisher,
        DodgeFury,
        OnKillRestore,
        Leech,
        Thorns,
        CalmWard,
        DebuffDecay,
        FrostSlowTaken,
        StaggerRecovery,
        ParryRestore,
        MeadBurst,
        MeadCooldown,
        RainShield,
        IgnoreWet,
        ColdImmunity,
        FreezeImmunity,

        // player-global hooks, Phase 3 survival (effects-phase3.md): the local player's own client
        MeadDuration,
        MeadPotency,
        MeadSave,
        FoodValues,
        FoodRegen,
        RestedDuration,
        TrinketDuration,
        AdrenalineGain,
        BlockRestore,
        BossDamageTaken,
        BurningTaken,
        BurningDecay,
        AirJump,

        // player-global, Phase 3 combat (Effects/Combat3): crits on the attacker's client
        CritChance,
        CritDamage,
        GlassCannon,

        // player-global hooks, Phase 3 gathering and crafting (effects-phase3.md): the local player's own client
        BaitSave,
        FishSize,
        ReelStamina,
        CraftSave,
        CraftExtra,
        TraderDiscount,

        // player-global, published on the player's own ZDO for other peers (PlayerStats): the creature's, ship's,
        // pickable's, rock's owner or every client reads them
        StaggerDurationDealt,
        LightAura,
        DemistRadius,
        TamingSpeed,
        SailSpeed,
        YieldMining,
        YieldLumber,
        YieldPickable,
        DotDuration,
        ButcherYield,
        ShipDamageTaken,

        // loot-kind: applied by Loot (killer-stat ZDO plumbing, Loot/FindPublisher + KillerStats), no-op here
        FindRarity,
        FindStones,
        FindTrophy,
        FindCoins,

        // external (api.md section 3): registered by another mod through the API, rolled, shown and summed here but
        // applied by that mod (it reads the totals); a no-op in the aggregate, like the loot-find kinds
        ExternalGlobal,

        // item-local hooks (never summed across items); keep ItemArmor first and SummonHealth last
        ItemArmor,
        ItemBlock,
        ItemDeflection,
        ItemDurability,
        ItemLighten,
        BrandDamage,
        DrawStaminaCost,
        AttackEitrCost,
        ReloadSpeed,
        ItemUnbreakable,
        ItemZeroWeight,
        ItemNoMovePenalty,
        AttackHealthCost,
        AttackReach,
        AttackArc,
        ProjectileVelocity,
        DrawSpeed,
        DrawMovePenalty,
        Multishot,
        Twincast,
        AmmoSave,
        RuneEdge,
        BloodPrice,
        LoneBlade,
        BlockSteadfast,
        PerfectBlockWindow,
        AddedDamage,
        AttackSpeed,
        CastSpeed,
        ChainLightning,
        ExplosiveShot,
        Paralyze,
        KnockbackDealt,
        Penetration,
        HeavyHand,
        ChopDamage,
        PickaxeDamage,
        FreeBuild,
        Throwable,
        Recall,
        Apportation,
        ExternalItem,
        SummonDamage,
        SummonHealth,
    }

    /// <summary>Effect id → <see cref="EffectKind"/>. Every id in <see cref="EffectCatalog"/> names a kind (checked at Awake).</summary>
    internal static class EffectKinds
    {
        /// <summary>
        /// The kind of an effect id: an external effect's (<see cref="EffectKind.ExternalGlobal"/> or
        /// <see cref="EffectKind.ExternalItem"/> by its scope), else the id in PascalCase (<c>move_speed</c> →
        /// <see cref="EffectKind.MoveSpeed"/>).
        /// </summary>
        public static EffectKind Of(string id)
        {
            if (EffectRegistry.IsExternal(id, out EffectScope scope))
            {
                return scope == EffectScope.ItemLocal ? EffectKind.ExternalItem : EffectKind.ExternalGlobal;
            }
            return EnumIds<EffectKind>.TryParse(id, out EffectKind kind) ? kind : EffectKind.Unknown;
        }

        /// <summary>Item-local kinds are applied by the item hooks and never summed across items.</summary>
        public static bool IsItemLocal(EffectKind kind) => kind >= EffectKind.ItemArmor;

        /// <summary>
        /// Plugin Awake: every registered effect must have a kind, or it would be registered but inert. External effects
        /// always have one (their scope's), whenever they were registered.
        /// </summary>
        public static void VerifyCatalog()
        {
            foreach (string id in EffectRegistry.AllIds)
            {
                if (Of(id) == EffectKind.Unknown)
                {
                    Core.Log.Error($"effect '{id}' is registered but has no implementation; inscriptions using it do nothing");
                }
            }
        }
    }

    /// <summary>
    /// The channel table of one rules generation, resolved to kinds: index = <see cref="ChannelDef.Index"/>. Rebuilt
    /// when the generation changes (on the next rebuild or item-cache miss, never per frame).
    /// </summary>
    internal sealed class ChannelPlan
    {
        private static ChannelPlan _current = new ChannelPlan(RuleSet.Empty);

        private ChannelPlan(RuleSet rules)
        {
            Generation = rules.Generation;
            Channels = rules.Affixes.Channels;
            Kinds = new EffectKind[Channels.Count];
            for (int i = 0; i < Kinds.Length; i++)
            {
                Kinds[i] = EffectKinds.Of(Channels[i].Effect.Id);
            }
        }

        public int Generation { get; }
        public IReadOnlyList<ChannelDef> Channels { get; }
        public EffectKind[] Kinds { get; }
        public int Count => Kinds.Length;

        /// <summary>The plan for the running rules.</summary>
        public static ChannelPlan Current
        {
            get
            {
                RuleSet rules = ActiveRules.Current;
                if (_current.Generation != rules.Generation)
                {
                    _current = new ChannelPlan(rules);
                }
                return _current;
            }
        }

        /// <summary>The channel's summed value clamped to its cap (caps are on the sum, never per item).</summary>
        public float Clamp(int channel, float sum) => Math.Min(sum, Channels[channel].Cap);
    }
}
