// Saga - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ExtraPotions.Saga.Powers;

namespace ExtraPotions.Saga.Config
{
    /// <summary>
    /// One Essential selector and the values it writes. MeadWorksDefault is each member's current default.
    /// A saved label is kept only when every member already equals that label. Otherwise the label becomes Custom and the members stay.
    /// </summary>
    public sealed class SagaPresetGroup
    {
        private static readonly IReadOnlyDictionary<SagaSetting, object> None = new Dictionary<SagaSetting, object>();
        private readonly IReadOnlyDictionary<SagaPreset, IReadOnlyDictionary<SagaSetting, object>> choices;

        internal SagaPresetGroup(SagaSetting<SagaPreset> selector, SagaSetting[] members,
            IReadOnlyDictionary<SagaPreset, IReadOnlyDictionary<SagaSetting, object>> choices)
        {
            if (selector == null) throw new ArgumentNullException(nameof(selector));
            if (members == null || members.Length == 0 || members.Any(m => m == null) || members.Distinct().Count() != members.Length)
                throw new ArgumentException(selector.Id + " needs distinct members.", nameof(members));
            if (selector.Scope != SettingScope.ServerAuthoritative)
                throw new ArgumentException(selector.Id + " is host-owned with its members.", nameof(selector));
            if (members.Any(m => m.Scope != selector.Scope || ReferenceEquals(m, selector)))
                throw new ArgumentException(selector.Id + " mixes scopes or includes itself.", nameof(members));
            Selector = selector;
            Members = members;
            this.choices = choices ?? throw new ArgumentNullException(nameof(choices));
            foreach (var preset in new[] { SagaPreset.Vanilla, SagaPreset.MeadWorksDefault, SagaPreset.Relaxed })
            {
                if (!choices.TryGetValue(preset, out var values) || values.Count != members.Length || members.Any(m => !values.ContainsKey(m)))
                    throw new ArgumentException(selector.Id + " " + preset + " does not cover its members.");
                foreach (var member in members)
                {
                    var value = values[member];
                    if (value == null || !member.ValueType.IsInstanceOfType(value) || !Equals(member.NormalizeBoxed(value), value))
                        throw new ArgumentException(member.Id + " " + preset + " is outside its range or type.");
                    if (preset == SagaPreset.MeadWorksDefault && !Equals(value, member.DefaultValue))
                        throw new ArgumentException(member.Id + " default preset moved an existing default.");
                }
            }
        }

        public SagaSetting<SagaPreset> Selector { get; }

        public IReadOnlyList<SagaSetting> Members { get; }

        public IReadOnlyDictionary<SagaSetting, object> Values(SagaPreset preset) =>
            choices.TryGetValue(preset, out var values) ? values : None;

        public bool Matches(SagaPreset preset, Func<SagaSetting, object> read) =>
            choices.TryGetValue(preset, out var values) && values.All(pair => Equals(pair.Value, read(pair.Key)));

        public SagaPreset OnLoad(SagaPreset saved, Func<SagaSetting, object> read) =>
            saved != SagaPreset.Custom && Matches(saved, read) ? saved : SagaPreset.Custom;
    }

    public static class SagaPresets
    {
        // Declaration order is not protection. One step toward Immune is stronger for the player. Ignore is not on that line.
        private static readonly Resistance[] Stronger =
        {
            Resistance.VeryWeak, Resistance.Weak, Resistance.SlightlyWeak, Resistance.Normal,
            Resistance.SlightlyResistant, Resistance.Resistant, Resistance.VeryResistant, Resistance.Immune,
        };

        private static readonly (SagaSetting<int> Setting, int Early)[] Thresholds =
        {
            (SagaSettings.Defense.RiposteLevel, 10),
            (SagaSettings.Defense.ShieldWallLevel, 25),
            (SagaSettings.Defense.HardenedLevel, 50),
            (SagaSettings.Defense.LastStandLevel, 75),
            (SagaSettings.Mining.UnbrokenLevel, 75),
            (SagaSettings.Mining.ReadTheRockLevel, 10),
            (SagaSettings.Mining.EchoLevel, 25),
            (SagaSettings.Foraging.SweepLevel, 10),
            (SagaSettings.Fishing.GraceLevel, 50),
            (SagaSettings.Fishing.LegendaryLevel, 25),
            (SagaSettings.Fishing.DoubleBonusLevel, 25),
            (SagaSettings.Bites.SpeciesSenseLevel, 10),
            (SagaSettings.Bites.SizeSenseLevel, 25),
            (SagaSettings.Bites.WaterSenseLevel, 50),
        };

        private static readonly SagaSetting<float>[] Strength =
        {
            SagaSettings.Defense.MaxHealthAt100, SagaSettings.Defense.FoodHealthAt100, SagaSettings.Defense.DamageReductionAt100,
            SagaSettings.Defense.RegenerationAt100, SagaSettings.Defense.PoiseAt100, SagaSettings.Defense.ParryWindowAt100,
            SagaSettings.Defense.BlockStaminaReductionAt100, SagaSettings.Defense.DodgeStaminaReductionAt100,
            SagaSettings.Defense.ReflexChanceAt100, SagaSettings.Defense.ShieldBashChanceAt100, SagaSettings.Defense.ThornsAt100,
            SagaSettings.Defense.AdrenalineBonusAt100, SagaSettings.Defense.ShieldWearReductionAt100, SagaSettings.Defense.KnockbackReductionAt100,
            SagaSettings.Woodcutting.FallPushAt100, SagaSettings.Woodcutting.LogSafetyAt100, SagaSettings.Woodcutting.DominoImpactAt100,
            SagaSettings.Woodcutting.CleanFellAt100, SagaSettings.Woodcutting.ReplantingAt100,
            SagaSettings.Woodcutting.StaminaRefundAt100, SagaSettings.Woodcutting.AxeWearReductionAt100, SagaSettings.Woodcutting.CleanSplitChanceAt100,
            SagaSettings.Mining.ExtraOreChanceAt100, SagaSettings.Mining.PickaxeWearReductionAt100, SagaSettings.Mining.SplashDamageAt100,
            SagaSettings.Foraging.ExtraYieldChanceAt100, SagaSettings.Foraging.SweepRadiusAt100,
            SagaSettings.Fishing.BonusItemChanceAt100, SagaSettings.Fishing.BaitSaverAt100, SagaSettings.Fishing.CastDistanceAt100, SagaSettings.Fishing.LineLengthAt100,
            SagaSettings.Bites.BiteChanceAt100,
        };

        private static readonly Lazy<IReadOnlyList<SagaPresetGroup>> groups = new Lazy<IReadOnlyList<SagaPresetGroup>>(Create);

        public static IReadOnlyList<SagaPresetGroup> Groups => groups.Value;

        /// <summary>No current Saga key is unread. The overlap list stays active, so this stays empty and startup logs nothing.</summary>
        public static readonly string[] Retired = Array.Empty<string>();

        public static readonly SagaSetting[] StillUsed =
        {
            SagaSettings.Defense.ShowCallouts, SagaSettings.Woodcutting.ShowCallouts, SagaSettings.Mining.ShowCallouts,
            SagaSettings.Foraging.ShowCallouts, SagaSettings.Fishing.ShowCallouts,
            SagaSettings.General.WeaponMasteryEnabled, SagaSettings.Progression.WeaponSwitch,
            SagaSettings.Powers.BlockPowerActivation, SagaSettings.Powers.AdvancedPowerConfig,
            SagaSettings.Sneak.SpeedAt100, SagaSettings.Sneak.VisibilitySkillFactor, SagaSettings.Cheats.MasteryOverrideLevel,
        };

        public static bool CanWrite(SagaPresetGroup group, bool remoteClient) =>
            group != null && (!remoteClient || group.Selector.Scope == SettingScope.Local);

        public static Resistance RaiseResistance(Resistance value)
        {
            if (value == Resistance.Ignore) return value;
            var index = Array.IndexOf(Stronger, value);
            return index < 0 || index == Stronger.Length - 1 ? value : Stronger[index + 1];
        }

        public static float FurtherFromZero(float value, float min, float max)
        {
            if (float.IsNaN(value) || float.IsInfinity(value)) throw new ArgumentOutOfRangeException(nameof(value));
            if (value == 0f) return 0f;
            var text = decimal.Round(DecimalOf(value) * 1.5m, 4, MidpointRounding.AwayFromZero).ToString(CultureInfo.InvariantCulture);
            var scaled = float.Parse(text, CultureInfo.InvariantCulture);
            if (scaled < min) return min;
            if (scaled > max) return max;
            return scaled;
        }

        private static decimal DecimalOf(float value) =>
            decimal.Parse(value.ToString("R", CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);

        private static IReadOnlyList<SagaPresetGroup> Create()
        {
            var experience = new SagaSetting[]
            {
                SagaSettings.Defense.ExperienceMultiplier, SagaSettings.Woodcutting.ExperienceMultiplier, SagaSettings.Mining.ExperienceMultiplier,
                SagaSettings.Fishing.ExperienceMultiplier, SagaSettings.Foraging.ExperiencePerPick, SagaSettings.Woodcutting.DiscoveryMultiplier,
                SagaSettings.Foraging.DiscoveryMultiplier, SagaSettings.Mining.DiscoveryExperience, SagaSettings.Fishing.DiscoveryExperience,
                SagaSettings.Fishing.NewSizeExperience,
            };
            var fight = new SagaSetting[]
            {
                SagaSettings.Fishing.LineTension, SagaSettings.Fishing.TensionBuildAt0, SagaSettings.Fishing.TensionBuildAt100, SagaSettings.Fishing.TensionEase,
                SagaSettings.Fishing.StrikeWindowAt0, SagaSettings.Fishing.StrikeWindowAt100, SagaSettings.Fishing.PerfectStrikeWindow,
                SagaSettings.Fishing.TiringPerThrash, SagaSettings.Fishing.ThrashesToTire, SagaSettings.Fishing.SpentReelSpeed, SagaSettings.Fishing.GraceSeconds,
            };
            var death = new SagaSetting[] { SagaSettings.Progression.SkillLossOnDeath, SagaSettings.Progression.LoseProgressOnDeath, SagaSettings.Progression.CatchUp };
            var finds = new SagaSetting[]
            {
                SagaSettings.Woodcutting.FindChanceAt0, SagaSettings.Woodcutting.FindChanceAt100, SagaSettings.Mining.FindChanceAt0, SagaSettings.Mining.FindChanceAt100,
                SagaSettings.Fishing.SnagChanceAt0, SagaSettings.Fishing.SnagChanceAt100, SagaSettings.Fishing.BigOneChanceAt100, SagaSettings.Fishing.NightBigOneBonus,
                SagaSettings.Fishing.LegendaryChance, SagaSettings.Mining.VeinChance1, SagaSettings.Mining.VeinChance2, SagaSettings.Mining.VeinChance3,
            };
            var thresholds = Thresholds.Select(row => (SagaSetting)row.Setting).ToArray();
            foreach (var row in Thresholds)
                if (row.Early >= row.Setting.Default) throw new ArgumentException(row.Setting.Id + " relaxed level is not earlier.");
            var powers = PowerCatalog.Features.Select(SagaSettings.PowerFeature).ToArray();
            var built = new[]
            {
                Make(SagaSettings.Progression.ExperiencePreset, experience,
                    Map((SagaSettings.Defense.ExperienceMultiplier, 0.5f), (SagaSettings.Woodcutting.ExperienceMultiplier, 0.5f),
                        (SagaSettings.Mining.ExperienceMultiplier, 0.5f), (SagaSettings.Fishing.ExperienceMultiplier, 0.5f),
                        (SagaSettings.Foraging.ExperiencePerPick, 1f), (SagaSettings.Woodcutting.DiscoveryMultiplier, 1f),
                        (SagaSettings.Foraging.DiscoveryMultiplier, 1f), (SagaSettings.Mining.DiscoveryExperience, 0f),
                        (SagaSettings.Fishing.DiscoveryExperience, 0f), (SagaSettings.Fishing.NewSizeExperience, 0f)),
                    Map((SagaSettings.Defense.ExperienceMultiplier, 2f), (SagaSettings.Woodcutting.ExperienceMultiplier, 2f),
                        (SagaSettings.Mining.ExperienceMultiplier, 2f), (SagaSettings.Fishing.ExperienceMultiplier, 2f),
                        (SagaSettings.Foraging.ExperiencePerPick, 6f), (SagaSettings.Woodcutting.DiscoveryMultiplier, 5f),
                        (SagaSettings.Foraging.DiscoveryMultiplier, 5f), (SagaSettings.Mining.DiscoveryExperience, 20f),
                        (SagaSettings.Fishing.DiscoveryExperience, 60f), (SagaSettings.Fishing.NewSizeExperience, 20f))),
                Make(SagaSettings.Progression.PerkThresholds, thresholds,
                    thresholds.ToDictionary(s => s, s => (object)101),
                    Thresholds.ToDictionary(row => (SagaSetting)row.Setting, row => (object)row.Early)),
                Make(SagaSettings.Progression.PerkStrength, Strength, StrengthAt(true), StrengthAt(false)),
                Make(SagaSettings.Progression.DeathPreset, death,
                    With(death, (SagaSettings.Progression.CatchUp, false)),
                    With(death, (SagaSettings.Progression.SkillLossOnDeath, 0f), (SagaSettings.Progression.LoseProgressOnDeath, false))),
                Make(SagaSettings.Progression.FindsPreset, finds,
                    finds.ToDictionary(s => s, s => (object)0f),
                    Map((SagaSettings.Woodcutting.FindChanceAt0, 5f), (SagaSettings.Woodcutting.FindChanceAt100, 30f),
                        (SagaSettings.Mining.FindChanceAt0, 1f), (SagaSettings.Mining.FindChanceAt100, 3f),
                        (SagaSettings.Fishing.SnagChanceAt0, 5f), (SagaSettings.Fishing.SnagChanceAt100, 12f),
                        (SagaSettings.Fishing.BigOneChanceAt100, 50f), (SagaSettings.Fishing.NightBigOneBonus, 100f),
                        (SagaSettings.Fishing.LegendaryChance, 2f), (SagaSettings.Mining.VeinChance1, 40f),
                        (SagaSettings.Mining.VeinChance2, 20f), (SagaSettings.Mining.VeinChance3, 10f))),
                Make(SagaSettings.Fishing.FightPreset, fight,
                    With(fight, (SagaSettings.Fishing.LineTension, false)),
                    With(fight, (SagaSettings.Fishing.TensionBuildAt0, 40f), (SagaSettings.Fishing.TensionBuildAt100, 20f),
                        (SagaSettings.Fishing.TensionEase, 80f), (SagaSettings.Fishing.StrikeWindowAt0, 1f),
                        (SagaSettings.Fishing.StrikeWindowAt100, 1.5f), (SagaSettings.Fishing.PerfectStrikeWindow, 0.35f),
                        (SagaSettings.Fishing.TiringPerThrash, 25f), (SagaSettings.Fishing.ThrashesToTire, 2),
                        (SagaSettings.Fishing.SpentReelSpeed, 80f), (SagaSettings.Fishing.GraceSeconds, 8f))),
                Make(SagaSettings.Powers.PowerStrength, powers, PowerBundle(SagaPreset.Vanilla), PowerBundle(SagaPreset.Relaxed)),
            };
            var seen = new HashSet<SagaSetting>();
            foreach (var member in built.SelectMany(group => group.Members))
                if (!seen.Add(member)) throw new ArgumentException(member.Id + " is in two presets.");
            return built;
        }

        private static Dictionary<SagaSetting, object> StrengthAt(bool vanilla)
        {
            var values = new Dictionary<SagaSetting, object>();
            foreach (var setting in Strength)
            {
                var min = (float)setting.Minimum!;
                var max = (float)setting.Maximum!;
                if (min > 0f) throw new ArgumentException(setting.Id + " cannot turn off at 0.");
                values[setting] = vanilla ? 0f : FurtherFromZero((float)setting.DefaultValue, min, max);
            }
            return values;
        }

        private static Dictionary<SagaSetting, object> PowerBundle(SagaPreset preset)
        {
            var values = new Dictionary<SagaSetting, object>();
            foreach (var feature in PowerCatalog.Features)
            {
                var setting = SagaSettings.PowerFeature(feature);
                switch (feature.Kind)
                {
                    case FeatureKind.Resistance:
                        values[setting] = preset == SagaPreset.Vanilla ? Resistance.Normal : RaiseResistance((Resistance)setting.DefaultValue);
                        break;
                    case FeatureKind.Toggle:
                        values[setting] = preset == SagaPreset.Vanilla ? false : setting.DefaultValue;
                        break;
                    default:
                        var range = PowerCatalog.Range(feature.Kind) ?? throw new InvalidOperationException(setting.Id);
                        values[setting] = preset == SagaPreset.Vanilla ? 0f : FurtherFromZero((float)setting.DefaultValue, range.Min, range.Max);
                        break;
                }
            }
            return values;
        }

        private static SagaPresetGroup Make(SagaSetting<SagaPreset> selector, SagaSetting[] members,
            IReadOnlyDictionary<SagaSetting, object> vanilla, IReadOnlyDictionary<SagaSetting, object> relaxed) =>
            new SagaPresetGroup(selector, members, new Dictionary<SagaPreset, IReadOnlyDictionary<SagaSetting, object>>
            {
                [SagaPreset.Vanilla] = vanilla,
                [SagaPreset.MeadWorksDefault] = members.ToDictionary(s => s, s => s.DefaultValue),
                [SagaPreset.Relaxed] = relaxed,
            });

        private static Dictionary<SagaSetting, object> Map(params (SagaSetting Setting, object Value)[] pairs)
        {
            var values = new Dictionary<SagaSetting, object>();
            foreach (var pair in pairs) values.Add(pair.Setting, pair.Value);
            return values;
        }

        private static Dictionary<SagaSetting, object> With(SagaSetting[] members, params (SagaSetting Setting, object Value)[] replacements)
        {
            var values = members.ToDictionary(s => s, s => s.DefaultValue);
            foreach (var pair in replacements) values[pair.Setting] = pair.Value;
            return values;
        }
    }

    /// <summary>Coalesces one update. A member edit in that update wins over the selector and writes no members.</summary>
    public sealed class SagaPresetChanges
    {
        private readonly Dictionary<SagaPresetGroup, bool> pending = new Dictionary<SagaPresetGroup, bool>();

        public void Observe(SagaSetting setting, bool remoteClient)
        {
            if (setting == null) return;
            foreach (var group in SagaPresets.Groups)
            {
                if (!SagaPresets.CanWrite(group, remoteClient)) continue;
                var member = group.Members.Contains(setting);
                if (!ReferenceEquals(setting, group.Selector) && !member) continue;
                pending[group] = member || (pending.TryGetValue(group, out var previous) && previous);
            }
        }

        public void Clear() => pending.Clear();

        public void Flush(bool remoteClient, Func<SagaSetting, object> read, Action<SagaPresetGroup, IReadOnlyDictionary<SagaSetting, object>> write)
        {
            if (read == null) throw new ArgumentNullException(nameof(read));
            if (write == null) throw new ArgumentNullException(nameof(write));
            var changes = pending.ToArray();
            pending.Clear();
            var failures = new List<Exception>();
            foreach (var change in changes)
            {
                var group = change.Key;
                if (!SagaPresets.CanWrite(group, remoteClient)) continue;
                try
                {
                    var preset = change.Value ? SagaPreset.Custom : (SagaPreset)read(group.Selector);
                    var values = new Dictionary<SagaSetting, object>(group.Values(preset)) { [group.Selector] = preset };
                    write(group, values);
                }
                catch (Exception failure) { failures.Add(failure); }
            }
            if (failures.Count > 0) throw new AggregateException("Saga preset updates failed.", failures);
        }
    }
}
