// Saga - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: the skill book's page data (design H1 to H3, section 11). Lines carry C05 keys and invariant
// arguments; the plugin formats them in the game language and renders them through the Crucible.UI kit (KitStatSheet).
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ExtraPotions.Saga.Config;
using ExtraPotions.Saga.Skills;

namespace ExtraPotions.Saga.Progression
{
    public enum LineKind { Heading, Perk, Locked, Note, Breakdown }

    /// <summary>One row of a skill page: a text key with its arguments, its kind, and for a locked row the level that unlocks it.</summary>
    public sealed class PageLine
    {
        internal PageLine(string key, LineKind kind, int unlockLevel, params string[] args)
        {
            Key = key;
            Kind = kind;
            UnlockLevel = unlockLevel;
            Args = Array.AsReadOnly(args);
        }

        public string Key { get; }
        public LineKind Kind { get; }

        /// <summary>The level a milestone unlocks at; 0 for rows that are not milestones.</summary>
        public int UnlockLevel { get; }
        public IReadOnlyList<string> Args { get; }

        public override string ToString() => Kind + " " + Key + "(" + string.Join(", ", Args) + ")";
    }

    /// <summary>A bonus the game's status effects add to a skill level (a power, a mead, an inscription), by its source's display name.</summary>
    public readonly struct LevelBonus
    {
        public LevelBonus(string source, float levels)
        {
            Source = source ?? "";
            Levels = levels;
        }

        public string Source { get; }
        public float Levels { get; }
    }

    /// <summary>The settings the pages show (design 4.4 to 4.9); <see cref="Defaults"/> reads the settings table.</summary>
    public sealed class BookValues
    {
        public DefenseValues Defense = DefenseValues.Defaults();
        public float SneakSpeedAt100, SneakVisibilityFactor;
        public float StaminaRefundAt100, AxeWearAt100, CleanSplitAt100, ReplantingAt100, WoodFindAt0, WoodFindAt100;
        public float SeamChanceAt0, SeamChanceAt100, SeamWindowAt0, SeamWindowAt100, ExtraOreAt100, PickaxeWearAt100, MineFindAt0, MineFindAt100;
        public int ReadTheRockLevel, EchoLevel, UnbrokenLevel;
        public float ExtraYieldAt100, SweepRadiusAt100;
        public int SweepLevel;
        public float StrikeWindowAt0, StrikeWindowAt100, BigOneAt100, BonusItemAt100, BaitSaverAt100, CastDistanceAt100;
        public int SpeciesSenseLevel, SizeSenseLevel, WaterSenseLevel, GraceLevel, LegendaryLevel, DoubleBonusLevel;

        public static BookValues Defaults() => new BookValues
        {
            SneakSpeedAt100 = SagaSettings.Sneak.SpeedAt100.Default,
            SneakVisibilityFactor = SagaSettings.Sneak.VisibilitySkillFactor.Default,
            StaminaRefundAt100 = SagaSettings.Woodcutting.StaminaRefundAt100.Default,
            AxeWearAt100 = SagaSettings.Woodcutting.AxeWearReductionAt100.Default,
            CleanSplitAt100 = SagaSettings.Woodcutting.CleanSplitChanceAt100.Default,
            ReplantingAt100 = SagaSettings.Woodcutting.ReplantingAt100.Default,
            WoodFindAt0 = SagaSettings.Woodcutting.FindChanceAt0.Default,
            WoodFindAt100 = SagaSettings.Woodcutting.FindChanceAt100.Default,
            SeamChanceAt0 = SagaSettings.Mining.SeamChanceAt0.Default,
            SeamChanceAt100 = SagaSettings.Mining.SeamChanceAt100.Default,
            SeamWindowAt0 = SagaSettings.Mining.SeamWindowAt0.Default,
            SeamWindowAt100 = SagaSettings.Mining.SeamWindowAt100.Default,
            ExtraOreAt100 = SagaSettings.Mining.ExtraOreChanceAt100.Default,
            PickaxeWearAt100 = SagaSettings.Mining.PickaxeWearReductionAt100.Default,
            MineFindAt0 = SagaSettings.Mining.FindChanceAt0.Default,
            MineFindAt100 = SagaSettings.Mining.FindChanceAt100.Default,
            ReadTheRockLevel = SagaSettings.Mining.ReadTheRockLevel.Default,
            EchoLevel = SagaSettings.Mining.EchoLevel.Default,
            UnbrokenLevel = SagaSettings.Mining.UnbrokenLevel.Default,
            ExtraYieldAt100 = SagaSettings.Foraging.ExtraYieldChanceAt100.Default,
            SweepRadiusAt100 = SagaSettings.Foraging.SweepRadiusAt100.Default,
            SweepLevel = SagaSettings.Foraging.SweepLevel.Default,
            StrikeWindowAt0 = SagaSettings.Fishing.StrikeWindowAt0.Default,
            StrikeWindowAt100 = SagaSettings.Fishing.StrikeWindowAt100.Default,
            BigOneAt100 = SagaSettings.Fishing.BigOneChanceAt100.Default,
            BonusItemAt100 = SagaSettings.Fishing.BonusItemChanceAt100.Default,
            BaitSaverAt100 = SagaSettings.Fishing.BaitSaverAt100.Default,
            CastDistanceAt100 = SagaSettings.Fishing.CastDistanceAt100.Default,
            SpeciesSenseLevel = SagaSettings.Bites.SpeciesSenseLevel.Default,
            SizeSenseLevel = SagaSettings.Bites.SizeSenseLevel.Default,
            WaterSenseLevel = SagaSettings.Bites.WaterSenseLevel.Default,
            GraceLevel = SagaSettings.Fishing.GraceLevel.Default,
            LegendaryLevel = SagaSettings.Fishing.LegendaryLevel.Default,
            DoubleBonusLevel = SagaSettings.Fishing.DoubleBonusLevel.Default,
        };
    }

    public static class SkillBook
    {
        public const string HeadingPerks = "page_perks";
        public const string HeadingMilestones = "page_milestones";
        public const string BreakdownBase = "breakdown_base";
        public const string BreakdownBonus = "breakdown_bonus";
        public const string BreakdownEffective = "breakdown_effective";

        /// <summary>Every perk row key with one argument, every milestone key with one (its level), every note without.</summary>
        public static readonly string[] PerkKeys =
        {
            "perk_max_health", "perk_food_health", "perk_damage_reduction", "perk_regeneration", "perk_poise", "perk_parry_window",
            "perk_block_stamina", "perk_dodge_stamina", "perk_sneak_speed", "perk_sneak_visibility", "perk_stamina_refund", "perk_axe_wear",
            "perk_clean_split", "perk_replanting", "perk_wood_find", "perk_seam_chance", "perk_seam_window", "perk_extra_ore",
            "perk_pickaxe_wear", "perk_mine_find", "perk_extra_yield", "perk_sweep_radius", "perk_strike_window", "perk_big_one",
            "perk_bonus_item", "perk_bait_saver", "perk_cast_distance",
        };

        public static readonly string[] MilestoneKeys =
        {
            "milestone_riposte", "milestone_shield_wall", "milestone_hardened", "milestone_last_stand", "milestone_read_the_rock",
            "milestone_echo", "milestone_unbroken", "milestone_sweep", "milestone_species_sense", "milestone_size_sense",
            "milestone_water_sense", "milestone_grace", "milestone_legendary", "milestone_double_bonus",
        };

        public static readonly string[] NoteKeys =
        {
            "note_defense_experience", "note_sneak_experience", "note_wood_experience", "note_mine_experience", "note_forage_experience",
            "note_fish_experience",
        };

        private static string F(float value) => value.ToString("0.##", CultureInfo.InvariantCulture);

        private sealed class Builder
        {
            private readonly List<PageLine> lines = new List<PageLine>();
            private readonly float level;
            private bool milestones;

            public Builder(float level)
            {
                this.level = level;
                lines.Add(new PageLine(HeadingPerks, LineKind.Heading, 0));
            }

            public Builder Perk(string key, float value)
            {
                lines.Add(new PageLine(key, LineKind.Perk, 0, F(value)));
                return this;
            }

            public Builder Percent(string key, float percentAt100) => Perk(key, SkillCurve.Share(percentAt100, level) * 100f);

            public Builder Milestone(string key, int unlock)
            {
                if (unlock > SkillCurve.MaxLevel) return this;
                if (!milestones)
                {
                    milestones = true;
                    lines.Add(new PageLine(HeadingMilestones, LineKind.Heading, 0));
                }
                lines.Add(new PageLine(key, SkillCurve.Reached(level, unlock) ? LineKind.Perk : LineKind.Locked, unlock,
                    unlock.ToString(CultureInfo.InvariantCulture)));
                return this;
            }

            public IReadOnlyList<PageLine> Note(string key)
            {
                lines.Add(new PageLine(key, LineKind.Note, 0));
                return lines.AsReadOnly();
            }
        }

        /// <summary>A Saga skill's page at a level: what each perk gives now, the milestones reached or locked, how it trains.</summary>
        public static IReadOnlyList<PageLine> Page(SagaSkill skill, float level, BookValues v)
        {
            if (v == null) throw new ArgumentNullException(nameof(v));
            var b = new Builder(level);
            var d = v.Defense;
            switch (skill)
            {
                case SagaSkill.Defense:
                    return b.Perk("perk_max_health", DefenseRules.BonusHealth(d, level))
                        .Percent("perk_food_health", d.FoodHealthAt100)
                        .Percent("perk_damage_reduction", d.DamageReductionAt100)
                        .Percent("perk_regeneration", d.RegenerationAt100)
                        .Percent("perk_poise", d.PoiseAt100)
                        .Perk("perk_parry_window", DefenseRules.ParryWindow(d, level))
                        .Percent("perk_block_stamina", d.BlockStaminaReductionAt100)
                        .Percent("perk_dodge_stamina", d.DodgeStaminaReductionAt100)
                        .Milestone("milestone_riposte", d.RiposteLevel)
                        .Milestone("milestone_shield_wall", d.ShieldWallLevel)
                        .Milestone("milestone_hardened", d.HardenedLevel)
                        .Milestone("milestone_last_stand", d.LastStandLevel)
                        .Note("note_defense_experience");
                case SagaSkill.Sneak:
                    return b.Perk("perk_sneak_speed", SkillCurve.Lerp(1f, v.SneakSpeedAt100, level))
                        .Perk("perk_sneak_visibility", v.SneakVisibilityFactor)
                        .Note("note_sneak_experience");
                case SagaSkill.WoodCutting:
                    return b.Percent("perk_stamina_refund", v.StaminaRefundAt100)
                        .Percent("perk_axe_wear", v.AxeWearAt100)
                        .Percent("perk_clean_split", v.CleanSplitAt100)
                        .Percent("perk_replanting", v.ReplantingAt100)
                        .Perk("perk_wood_find", SkillCurve.Lerp(v.WoodFindAt0, v.WoodFindAt100, level))
                        .Note("note_wood_experience");
                case SagaSkill.Pickaxes:
                    return b.Perk("perk_seam_chance", SkillCurve.Lerp(v.SeamChanceAt0, v.SeamChanceAt100, level))
                        .Perk("perk_seam_window", SkillCurve.Lerp(v.SeamWindowAt0, v.SeamWindowAt100, level))
                        .Percent("perk_extra_ore", v.ExtraOreAt100)
                        .Percent("perk_pickaxe_wear", v.PickaxeWearAt100)
                        .Perk("perk_mine_find", SkillCurve.Lerp(v.MineFindAt0, v.MineFindAt100, level))
                        .Milestone("milestone_read_the_rock", v.ReadTheRockLevel)
                        .Milestone("milestone_echo", v.EchoLevel)
                        .Milestone("milestone_unbroken", v.UnbrokenLevel)
                        .Note("note_mine_experience");
                case SagaSkill.Foraging:
                    return b.Percent("perk_extra_yield", v.ExtraYieldAt100)
                        .Perk("perk_sweep_radius", v.SweepRadiusAt100 * SkillCurve.Factor(level))
                        .Milestone("milestone_sweep", v.SweepLevel)
                        .Note("note_forage_experience");
                case SagaSkill.Fishing:
                    return b.Perk("perk_strike_window", SkillCurve.Lerp(v.StrikeWindowAt0, v.StrikeWindowAt100, level))
                        .Percent("perk_big_one", v.BigOneAt100)
                        .Percent("perk_bonus_item", v.BonusItemAt100)
                        .Percent("perk_bait_saver", v.BaitSaverAt100)
                        .Percent("perk_cast_distance", v.CastDistanceAt100)
                        .Milestone("milestone_species_sense", v.SpeciesSenseLevel)
                        .Milestone("milestone_size_sense", v.SizeSenseLevel)
                        .Milestone("milestone_water_sense", v.WaterSenseLevel)
                        .Milestone("milestone_grace", v.GraceLevel)
                        .Milestone("milestone_legendary", v.LegendaryLevel)
                        .Milestone("milestone_double_bonus", v.DoubleBonusLevel)
                        .Note("note_fish_experience");
                default:
                    throw new ArgumentException("Unknown skill.", nameof(skill));
            }
        }

        /// <summary>
        /// The effective level breakdown (design H3): the base level, each status-effect bonus with its source, then the
        /// effective level (never below 0), as the game's own skill level modifiers add them.
        /// </summary>
        public static IReadOnlyList<PageLine> Breakdown(float baseLevel, IEnumerable<LevelBonus> bonuses)
        {
            var lines = new List<PageLine> { new PageLine(BreakdownBase, LineKind.Breakdown, 0, F(baseLevel)) };
            var total = baseLevel;
            foreach (var bonus in bonuses ?? Array.Empty<LevelBonus>())
            {
                if (bonus.Levels == 0f || float.IsNaN(bonus.Levels)) continue;
                total += bonus.Levels;
                lines.Add(new PageLine(BreakdownBonus, LineKind.Breakdown, 0, bonus.Source, bonus.Levels.ToString("+0.##;-0.##", CultureInfo.InvariantCulture)));
            }
            lines.Add(new PageLine(BreakdownEffective, LineKind.Breakdown, 0, F(Math.Max(0f, total))));
            return lines.AsReadOnly();
        }
    }

    /// <summary>Skill gain notices (design H5, SNEAKer's Display Exp Gained generalised).</summary>
    public static class SkillNotices
    {
        public static bool ShouldShow(SkillGainNotices mode, bool sagaSkill) =>
            mode == SkillGainNotices.All || (mode == SkillGainNotices.SagaSkills && sagaSkill);
    }
}
