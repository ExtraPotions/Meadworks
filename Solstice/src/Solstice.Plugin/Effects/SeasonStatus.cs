// Solstice - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: the season status effect and warm-clothes overheat on the local player. Icons are borrowed from the game.
using System;
using System.Collections.Generic;
using ExtraPotions.Solstice.Calendar;
using ExtraPotions.Solstice.Config;
using ExtraPotions.Solstice.Effects;
using ExtraPotions.Solstice.Localization;
using ExtraPotions.Solstice.Visuals;
using UnityEngine;

namespace ExtraPotions.Solstice.Runtime
{
    /// <summary>Season stats for the local player. Skill modifiers are added to the game's own values.</summary>
    public sealed class SolsticeSeasonStatus : SE_Stats
    {
        public readonly Dictionary<Skills.SkillType, float> Raise = new Dictionary<Skills.SkillType, float>();
        public readonly Dictionary<Skills.SkillType, float> Levels = new Dictionary<Skills.SkillType, float>();

        public override void ModifyRaiseSkill(Skills.SkillType skill, ref float value)
        {
            if (Raise.TryGetValue(skill, out var extra)) value += extra;
        }

        public override void ModifySkillLevel(Skills.SkillType skill, ref float level)
        {
            if (Levels.TryGetValue(skill, out var extra)) level += extra;
        }
    }

    /// <summary>
    /// Keeps the season status and the overheat effect on the local player. A dedicated server has no local player, so it does nothing.
    /// The live effect is updated in place. Main thread only.
    /// </summary>
    public static class SeasonStatus
    {
        const string SeasonKey = "SolsticeSeason";
        const string OverheatKey = "SolsticeOverheat";

        static SolsticeSeasonStatus? seasonTemplate;
        static SE_Stats? overheatTemplate;
        static SolsticeSeasonStatus? seasonLive;
        static SE_Stats? overheatLive;
        static Season? cachedSeason;
        static SolsticeSeasonStatus? cachedEffect;
        static string cachedSeasonName = "";
        static int cachedDay = -1;
        static int cachedLength = -1;
        static string coolingText = "";
        static WinterCrops? coolingFoods;

        /// <summary>Warm-clothes heat the meter should approach: 0, 0.4 for one piece, 1 when the overheat effect is on.</summary>
        public static float ShownHeat { get; private set; }

        public static void Tick()
        {
            var player = Player.m_localPlayer;
            if (player == null || player.m_isLoading || player.m_nview == null || !player.m_nview.IsValid())
            {
                ShownHeat = 0f;
                return;
            }
            var seman = player.GetSEMan();
            if (seman == null) return;
            bool effects = SolsticeSeasons.Effects != null && SolsticeSeasons.Current.HasValue && SolsticeSeasons.View.Available;
            HoldSeason(player, seman, effects);
            HoldOverheat(player, seman, effects);
        }

        public static void Shutdown()
        {
            ReleaseLive();
            if (seasonTemplate != null) UnityEngine.Object.Destroy(seasonTemplate);
            if (overheatTemplate != null) UnityEngine.Object.Destroy(overheatTemplate);
            seasonTemplate = null;
            overheatTemplate = null;
        }

        /// <summary>Drops the live effects and the name cache. The next tick localizes the new effect.</summary>
        public static void ReleaseLive()
        {
            var player = Player.m_localPlayer;
            var seman = player != null ? player.GetSEMan() : null;
            if (seman != null)
            {
                if (seasonLive != null) seman.RemoveStatusEffect(seasonLive.NameHash());
                if (overheatLive != null) seman.RemoveStatusEffect(overheatLive.NameHash());
            }
            seasonLive = null;
            overheatLive = null;
            cachedSeason = null;
            cachedEffect = null;
            cachedSeasonName = "";
            cachedDay = -1;
            cachedLength = -1;
            ShownHeat = 0f;
        }

        static void HoldSeason(Player player, SEMan seman, bool effects)
        {
            var season = SolsticeSeasons.Current;
            bool on = effects && season.HasValue && SolsticeSeasons.SeasonStatusEnabled
                && ClimateRules.StatusApplies(true, player.InInterior(), AshOrDeep(player));
            if (!on)
            {
                Drop(seman, ref seasonLive);
                cachedSeason = null;
                cachedEffect = null;
                return;
            }
            var template = SeasonTemplate();
            if (!Worn(seman, seasonLive)) seasonLive = seman.AddStatusEffect(template) as SolsticeSeasonStatus;
            if (seasonLive == null) return;
            bool sameEffect = ReferenceEquals(cachedEffect, seasonLive);
            if (!sameEffect || cachedSeason != season)
            {
                Apply(seasonLive, SeasonStatTable.Defaults(season!.Value));
                cachedEffect = seasonLive;
                cachedSeason = season;
            }
            var view = SolsticeSeasons.View;
            string seasonName = SolsticeText.Text(SolsticeTextKeys.SeasonName(season!.Value));
            if (SeasonRebuild.Text(sameEffect, seasonName, cachedSeasonName, view.DayInSeason, cachedDay, view.DaysInSeason, cachedLength))
            {
                seasonLive.m_name = seasonName;
                seasonLive.m_tooltip = SolsticeText.Format(SolsticeTextKeys.StatusSeasonTip, view.DayInSeason, view.DaysInSeason);
                cachedSeasonName = seasonName;
                cachedDay = view.DayInSeason;
                cachedLength = view.DaysInSeason;
            }
            seasonLive.m_ttl = 0f;
            var mark = SeasonMarks.For(season.Value);
            if (mark == null && season.Value == Season.Winter) mark = SeasonMarks.Mark(Season.Winter);
            if (mark != null) seasonLive.m_icon = mark;
            else if (seasonLive.m_icon == null || SeasonMarks.Owns(seasonLive.m_icon))
                seasonLive.m_icon = Borrow("Rested", "Cold", "Freezing");
        }

        static void HoldOverheat(Player player, SEMan seman, bool effects)
        {
            var season = SolsticeSeasons.Current;
            var stats = season.HasValue ? SolsticeSeasons.EffectsOf(season.Value) : null;
            bool sheltered = player.InInterior() || player.InShelter() || EnvMan.IsCold() || EnvMan.IsFreezing();
            int warm = WarmClothes(player);
            bool cooling = Cooling(player);
            bool seasonOverheats = stats != null && stats.OverheatIn2WarmClothes;
            var settings = Plugin.Settings;
            bool meter = settings != null && settings.Get(SolsticeSettings.Visuals.HeatMeter);
            ShownHeat = HeatMeter.Target(meter && effects && SolsticeSeasons.WarmOverheat, seasonOverheats, sheltered, warm, cooling);
            bool on = effects && stats != null && SolsticeSeasons.WarmOverheat
                && ClimateRules.Overheating(true, seasonOverheats, sheltered, warm, cooling);
            if (!on)
            {
                Drop(seman, ref overheatLive);
                return;
            }
            if (!Worn(seman, overheatLive)) overheatLive = seman.AddStatusEffect(OverheatTemplate()) as SE_Stats;
            if (overheatLive == null) return;
            overheatLive.m_staminaRegenMultiplier = ClimateRules.OverheatRegen;
            overheatLive.m_eitrRegenMultiplier = ClimateRules.OverheatRegen;
            overheatLive.m_name = SolsticeText.Text(SolsticeTextKeys.StatusOverheat);
            overheatLive.m_tooltip = SolsticeText.Text(SolsticeTextKeys.StatusOverheatTip);
            overheatLive.m_ttl = 0f;
            var heat = SeasonMarks.Heat();
            if (heat != null) overheatLive.m_icon = heat;
        }

        static void Apply(SolsticeSeasonStatus effect, SeasonStatModifiers table)
        {
            effect.m_tickInterval = table.TickInterval;
            effect.m_healthPerTickMinHealthPercentage = table.HealthPerTickMinHealthPercentage;
            effect.m_healthPerTick = table.HealthPerTick;
            effect.m_runStaminaDrainModifier = table.RunStaminaDrainModifier;
            effect.m_jumpStaminaUseModifier = table.JumpStaminaUseModifier;
            effect.m_attackStaminaUseModifier = table.AttackStaminaUseModifier;
            effect.m_blockStaminaUseModifier = table.BlockStaminaUseModifier;
            effect.m_blockStaminaUseFlatValue = table.BlockStaminaUseFlatValue;
            effect.m_dodgeStaminaUseModifier = table.DodgeStaminaUseModifier;
            effect.m_swimStaminaUseModifier = table.SwimStaminaUseModifier;
            effect.m_homeItemStaminaUseModifier = table.HomeItemStaminaUseModifier;
            effect.m_sneakStaminaUseModifier = table.SneakStaminaUseModifier;
            effect.m_runStaminaUseModifier = table.RunStaminaUseModifier;
            effect.m_healthRegenMultiplier = table.HealthRegenMultiplier;
            effect.m_staminaRegenMultiplier = table.StaminaRegenMultiplier;
            effect.m_eitrRegenMultiplier = table.EitrRegenMultiplier;
            effect.m_noiseModifier = table.NoiseModifier;
            effect.m_stealthModifier = table.StealthModifier;
            effect.m_addMaxCarryWeight = table.AddMaxCarryWeight;
            effect.m_speedModifier = table.SpeedModifier;
            effect.m_swimSpeedModifier = table.SwimSpeedModifier;
            effect.m_maxMaxFallSpeed = table.MaxMaxFallSpeed;
            effect.m_fallDamageModifier = table.FallDamageModifier;
            effect.m_adrenalineModifier = table.AdrenalineModifier;
            effect.m_staggerModifier = table.StaggerModifier;
            effect.m_timedBlockBonus = table.TimedBlockBonus;
            effect.m_mods = new List<HitData.DamageModPair>();
            foreach (var pair in table.DamageModifiers)
            {
                if (Enum.TryParse(pair.Key, out HitData.DamageType type) && Enum.TryParse(pair.Value, out HitData.DamageModifier modifier))
                    effect.m_mods.Add(new HitData.DamageModPair { m_type = type, m_modifier = modifier });
            }
            effect.Raise.Clear();
            effect.Levels.Clear();
            foreach (var pair in table.RaiseSkills)
                if (Enum.TryParse(pair.Key, out Skills.SkillType skill)) effect.Raise[skill] = pair.Value;
            foreach (var pair in table.SkillLevels)
                if (Enum.TryParse(pair.Key, out Skills.SkillType skill)) effect.Levels[skill] = pair.Value;
        }

        static bool AshOrDeep(Player player)
        {
            var biome = player.GetCurrentBiome();
            return biome == Heightmap.Biome.AshLands || biome == Heightmap.Biome.DeepNorth;
        }

        static int WarmClothes(Player player)
        {
            var inventory = player.GetInventory();
            if (inventory == null) return 0;
            var items = inventory.m_inventory;
            if (items == null) return 0;
            int count = 0;
            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];
                if (item == null || !item.m_equipped) continue;
                var mods = item?.m_shared?.m_damageModifiers;
                if (mods == null) continue;
                foreach (var mod in mods)
                {
                    if (mod.m_type == HitData.DamageType.Frost && Warm(mod.m_modifier))
                    {
                        count++;
                        break;
                    }
                }
            }
            return count;
        }

        static bool Warm(HitData.DamageModifier modifier) =>
            modifier == HitData.DamageModifier.SlightlyResistant || modifier == HitData.DamageModifier.Resistant
            || modifier == HitData.DamageModifier.VeryResistant || modifier == HitData.DamageModifier.Immune;

        static bool Cooling(Player player)
        {
            if (player.m_foods == null) return false;
            string text = SolsticeSeasons.CoolingFoods ?? "";
            if (coolingFoods == null || !string.Equals(text, coolingText, StringComparison.Ordinal))
            {
                coolingText = text;
                coolingFoods = WinterCrops.Parse(text);
            }
            var names = coolingFoods;
            foreach (var food in player.m_foods)
            {
                var item = food?.m_item;
                if (item == null) continue;
                if (names.Survives(false, new[] { item.m_shared?.m_name, item.m_dropPrefab != null ? item.m_dropPrefab.name : null })) return true;
            }
            return false;
        }

        static SolsticeSeasonStatus SeasonTemplate()
        {
            if (seasonTemplate == null)
            {
                seasonTemplate = ScriptableObject.CreateInstance<SolsticeSeasonStatus>();
                seasonTemplate.name = SeasonKey;
                seasonTemplate.m_name = SeasonKey;
                seasonTemplate.m_ttl = 0f;
                seasonTemplate.hideFlags = HideFlags.DontUnloadUnusedAsset;
            }
            return seasonTemplate;
        }

        static SE_Stats OverheatTemplate()
        {
            if (overheatTemplate == null)
            {
                overheatTemplate = ScriptableObject.CreateInstance<SE_Stats>();
                overheatTemplate.name = OverheatKey;
                overheatTemplate.m_name = OverheatKey;
                overheatTemplate.m_staminaRegenMultiplier = ClimateRules.OverheatRegen;
                overheatTemplate.m_eitrRegenMultiplier = ClimateRules.OverheatRegen;
                overheatTemplate.m_ttl = 0f;
                overheatTemplate.hideFlags = HideFlags.DontUnloadUnusedAsset;
            }
            return overheatTemplate;
        }

        static Sprite? Borrow(params string[] names)
        {
            var db = ObjectDB.instance;
            if (db == null) return null;
            foreach (var name in names)
            {
                var effect = db.GetStatusEffect(name.GetStableHashCode());
                if (effect != null && effect.m_icon != null) return effect.m_icon;
            }
            return null;
        }

        static bool Worn(SEMan seman, StatusEffect? effect)
        {
            if (effect == null) return false;
            var list = seman.GetStatusEffects();
            if (list == null) return false;
            for (int i = 0; i < list.Count; i++)
                if (ReferenceEquals(list[i], effect)) return true;
            return false;
        }

        static void Drop<T>(SEMan seman, ref T? live) where T : StatusEffect
        {
            if (live == null) return;
            seman.RemoveStatusEffect(live.NameHash());
            live = null;
        }
    }
}
