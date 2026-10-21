// Saga - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original. Reimplemented from ProgressivePowers 0.3.6 behavior only (README, the user's config and YAML
// files as data; no license, no code, DLL never opened): the seven Forsaken powers, their seven features each with
// design 4.3.2 defaults, and the effect catalog names of the Powers.yaml schema. Kall Fimbulbringer (Deep North, Valheim
// 1.0.20) is a Meadworks-original eighth power with the same shape.
using System;
using System.Collections.Generic;
using System.Linq;

namespace ExtraPotions.Saga.Powers
{
    /// <summary>
    /// The Forsaken bosses in game order; the names are the YAML boss names of design 4.3.3. New bosses are only ever
    /// appended: the value sets the setting section number, and older Saga versions skip names they do not know.
    /// </summary>
    public enum Boss { Eikthyr, TheElder, Bonemass, Moder, Yagluth, Queen, Fader, Kall }

    /// <summary>The value a power feature takes and its design 4.3.1 range.</summary>
    public enum FeatureKind { Fraction, CarryWeight, Armor, SkillLevels, HealthTick, Resistance, Toggle }

    /// <summary>The game's damage modifiers (<c>HitData.DamageModifier</c>, same names in the same order).</summary>
    public enum Resistance { Normal, Resistant, Weak, Immune, Ignore, VeryResistant, VeryWeak, SlightlyResistant, SlightlyWeak }

    /// <summary>Effect names of the advanced power definitions (Powers.yaml schema, design A9).</summary>
    public enum PowerEffect
    {
        RunStaminaCost, JumpStaminaCost, SwimStaminaCost, DodgeStaminaCost, AttackStaminaCost, BlockStaminaCost, BlockStaminaFlat,
        SneakStaminaCost, ToolStaminaCost, MovementSpeed, SwimSpeed, JumpHeight, JumpDistance, MaxFallSpeed, WindMovement,
        WindRunStaminaCost, FallDamage, HealthRegen, StaminaRegen, EitrRegen, Armor, ArmorMultiplier, CarryWeight, Noise, Stealth,
        AdrenalineGain, StaggerTaken, TimedBlockBonus, ColdResistance, SailingPower, DoubleImpactDamage, TamingBoost,
        Resistance, DamageDealt, SkillLevel, SkillGain, AttackDamage, HealthTick,
    }

    /// <summary>One effect with its value: a number, a resistance modifier or a toggle, and the damage type or skill it targets.</summary>
    public readonly struct EffectValue : IEquatable<EffectValue>
    {
        public EffectValue(PowerEffect effect, float number, string target = "", Resistance modifier = Resistance.Normal, bool flag = false)
        {
            Effect = effect;
            Number = number;
            Target = target ?? "";
            Modifier = modifier;
            Flag = flag;
        }

        public PowerEffect Effect { get; }
        public float Number { get; }

        /// <summary>Damage type (Resistance, DamageDealt) or skill (SkillLevel, SkillGain, AttackDamage); empty otherwise.</summary>
        public string Target { get; }
        public Resistance Modifier { get; }
        public bool Flag { get; }

        public bool Equals(EffectValue other) => Effect == other.Effect && Number.Equals(other.Number) && Target == other.Target && Modifier == other.Modifier && Flag == other.Flag;
        public override bool Equals(object? obj) => obj is EffectValue other && Equals(other);
        public override int GetHashCode() => ((int)Effect * 397) ^ Number.GetHashCode() ^ Target.GetHashCode();
        public override string ToString() => Effect + (Target.Length > 0 ? "." + Target : "") + "=" +
            (Effect == PowerEffect.Resistance ? Modifier.ToString() : Effect == PowerEffect.ColdResistance || Effect == PowerEffect.SailingPower
                || Effect == PowerEffect.DoubleImpactDamage || Effect == PowerEffect.TamingBoost ? (Flag ? "true" : "false")
                : Number.ToString("R", System.Globalization.CultureInfo.InvariantCulture));
    }

    /// <summary>One of a power's seven features: its number, setting name, value kind, default and the effects a value makes.</summary>
    public sealed class PowerFeature
    {
        private readonly Func<object, EffectValue[]> effects;

        internal PowerFeature(Boss power, int index, string name, FeatureKind kind, object defaultValue, Func<object, EffectValue[]> effects)
        {
            Power = power;
            Index = index;
            Name = name;
            Kind = kind;
            Default = defaultValue;
            this.effects = effects;
        }

        public Boss Power { get; }

        /// <summary>1 to 7: the mastery level that unlocks it.</summary>
        public int Index { get; }
        public string Name { get; }
        public FeatureKind Kind { get; }
        public object Default { get; }

        /// <summary>Setting key, <c>Feature N - name</c>.</summary>
        public string Key => "Feature " + Index + " - " + Name;

        /// <summary>
        /// The effects of a value. A number of 0, a resistance of Normal or a toggle of false switches the feature off (no
        /// effects); a value of the wrong type uses the default.
        /// </summary>
        public IReadOnlyList<EffectValue> Effects(object? value)
        {
            var v = value != null && value.GetType() == Default.GetType() ? value : Default;
            if (v is float f && (f == 0f || float.IsNaN(f) || float.IsInfinity(f))) return Array.Empty<EffectValue>();
            if (v is Resistance r && r == Resistance.Normal) return Array.Empty<EffectValue>();
            if (v is bool b && !b) return Array.Empty<EffectValue>();
            return effects(v);
        }

        public override string ToString() => Power + " " + Key;
    }

    public static class PowerCatalog
    {
        /// <summary>The boss names of design 4.3.3 (and of ProgressivePowers' Levels.yaml), in game order.</summary>
        public static string Name(Boss boss) => boss.ToString();

        /// <summary>The name in setting sections (<c>4 - Power N - &lt;name&gt;</c>).</summary>
        public static string DisplayName(Boss boss) => boss == Boss.TheElder ? "The Elder" : boss == Boss.Queen ? "The Queen" : boss == Boss.Kall ? "Kall Fimbulbringer" : boss.ToString();

        /// <summary>
        /// The boss's creature prefab: kill credit is given for a dead creature with this prefab name. Kall fights in three
        /// phases (FrozenKing, FrozenKing_p2, FrozenKing_p3); only the final phase is a kill.
        /// </summary>
        public static string Prefab(Boss boss)
        {
            switch (boss)
            {
                case Boss.Eikthyr: return "Eikthyr";
                case Boss.TheElder: return "gd_king";
                case Boss.Bonemass: return "Bonemass";
                case Boss.Moder: return "Dragon";
                case Boss.Yagluth: return "GoblinKing";
                case Boss.Queen: return "SeekerQueen";
                case Boss.Fader: return "Fader";
                case Boss.Kall: return "FrozenKing_p3";
                default: throw new ArgumentException("Unknown boss.", nameof(boss));
            }
        }

        /// <summary>A boss by its YAML name (case-insensitive) or creature prefab; false for any other text.</summary>
        public static bool TryParse(string? text, out Boss boss)
        {
            foreach (Boss b in Enum.GetValues(typeof(Boss)))
                if (string.Equals(text?.Trim(), Name(b), StringComparison.OrdinalIgnoreCase) || string.Equals(text?.Trim(), Prefab(b), StringComparison.Ordinal))
                {
                    boss = b;
                    return true;
                }
            boss = Boss.Eikthyr;
            return false;
        }

        public static string Section(Boss boss) => "4 - Power " + ((int)boss + 1) + " - " + DisplayName(boss);

        private static PowerFeature Number(Boss p, int i, string name, FeatureKind kind, float value, PowerEffect effect, string target = "") =>
            new PowerFeature(p, i, name, kind, value, v => new[] { new EffectValue(effect, (float)v, target) });

        private static PowerFeature Resist(Boss p, int i, string type, Resistance value) =>
            new PowerFeature(p, i, type + " resistance", FeatureKind.Resistance, value, v => new[] { new EffectValue(PowerEffect.Resistance, 0f, type, (Resistance)v) });

        private static PowerFeature Toggle(Boss p, int i, string name, PowerEffect effect) =>
            new PowerFeature(p, i, name, FeatureKind.Toggle, true, v => new[] { new EffectValue(effect, 0f, "", Resistance.Normal, true) });

        private const FeatureKind F = FeatureKind.Fraction;

        private static readonly PowerFeature[] features =
        {
            Number(Boss.Eikthyr, 1, "Run stamina cost", F, -0.3f, PowerEffect.RunStaminaCost),
            Number(Boss.Eikthyr, 2, "Jump stamina cost", F, -0.3f, PowerEffect.JumpStaminaCost),
            Number(Boss.Eikthyr, 3, "Swim stamina cost", F, -0.3f, PowerEffect.SwimStaminaCost),
            Number(Boss.Eikthyr, 4, "Movement speed", F, 0.03f, PowerEffect.MovementSpeed),
            Number(Boss.Eikthyr, 5, "Stamina regeneration", F, 0.15f, PowerEffect.StaminaRegen),
            Number(Boss.Eikthyr, 6, "Fall damage", F, -0.35f, PowerEffect.FallDamage),
            Number(Boss.Eikthyr, 7, "Dodge stamina cost", F, -0.25f, PowerEffect.DodgeStaminaCost),

            Number(Boss.TheElder, 1, "Health regeneration", F, 0.15f, PowerEffect.HealthRegen),
            Number(Boss.TheElder, 2, "Chopping damage", F, 0.3f, PowerEffect.DamageDealt, "Chop"),
            Number(Boss.TheElder, 3, "Pickaxe damage", F, 0.3f, PowerEffect.DamageDealt, "Pickaxe"),
            Number(Boss.TheElder, 4, "Carry weight", FeatureKind.CarryWeight, 75f, PowerEffect.CarryWeight),
            Number(Boss.TheElder, 5, "Woodcutting skill", FeatureKind.SkillLevels, 12f, PowerEffect.SkillLevel, "WoodCutting"),
            Number(Boss.TheElder, 6, "Pickaxes skill", FeatureKind.SkillLevels, 12f, PowerEffect.SkillLevel, "Pickaxes"),
            Number(Boss.TheElder, 7, "Regeneration tick", FeatureKind.HealthTick, 1f, PowerEffect.HealthTick),

            Resist(Boss.Bonemass, 1, "Blunt", Resistance.SlightlyResistant),
            Resist(Boss.Bonemass, 2, "Slash", Resistance.SlightlyResistant),
            Resist(Boss.Bonemass, 3, "Pierce", Resistance.SlightlyResistant),
            Number(Boss.Bonemass, 4, "Block stamina cost", F, -0.5f, PowerEffect.BlockStaminaCost),
            Resist(Boss.Bonemass, 5, "Poison", Resistance.Resistant),
            Number(Boss.Bonemass, 6, "Armor", FeatureKind.Armor, 6f, PowerEffect.Armor),
            Number(Boss.Bonemass, 7, "Stagger taken", F, -0.25f, PowerEffect.StaggerTaken),

            Resist(Boss.Moder, 1, "Frost", Resistance.Resistant),
            Toggle(Boss.Moder, 2, "Cold immunity", PowerEffect.ColdResistance),
            Toggle(Boss.Moder, 3, "Sailing tailwind", PowerEffect.SailingPower),
            Number(Boss.Moder, 4, "Carry weight", FeatureKind.CarryWeight, 150f, PowerEffect.CarryWeight),
            Number(Boss.Moder, 5, "Movement speed", F, 0.05f, PowerEffect.MovementSpeed),
            new PowerFeature(Boss.Moder, 6, "Wind at your back", F, 0.25f, v => new[]
            {
                new EffectValue(PowerEffect.WindMovement, (float)v), new EffectValue(PowerEffect.WindRunStaminaCost, -(float)v),
            }),
            Number(Boss.Moder, 7, "Swim speed", F, 0.25f, PowerEffect.SwimSpeed),

            Resist(Boss.Yagluth, 1, "Lightning", Resistance.Resistant),
            Number(Boss.Yagluth, 2, "Melee damage", F, 0.05f, PowerEffect.DamageDealt, "Melee"),
            Number(Boss.Yagluth, 3, "Farming skill", FeatureKind.SkillLevels, 12f, PowerEffect.SkillLevel, "Farming"),
            Number(Boss.Yagluth, 4, "Elemental damage", F, 0.05f, PowerEffect.DamageDealt, "Elemental"),
            Number(Boss.Yagluth, 5, "Skill gain rate", F, 0.15f, PowerEffect.SkillGain, "All"),
            Number(Boss.Yagluth, 6, "Armor multiplier", F, 0.1f, PowerEffect.ArmorMultiplier),
            Number(Boss.Yagluth, 7, "All damage", F, 0.05f, PowerEffect.DamageDealt, "All"),

            Resist(Boss.Queen, 1, "Poison", Resistance.Resistant),
            Number(Boss.Queen, 2, "Sneak stamina cost", F, -0.5f, PowerEffect.SneakStaminaCost),
            Number(Boss.Queen, 3, "Eitr regeneration", F, 0.5f, PowerEffect.EitrRegen),
            Number(Boss.Queen, 4, "Stealth", F, 0.25f, PowerEffect.Stealth),
            Number(Boss.Queen, 5, "Noise", F, -0.25f, PowerEffect.Noise),
            Number(Boss.Queen, 6, "Elemental Magic skill", FeatureKind.SkillLevels, 12f, PowerEffect.SkillLevel, "ElementalMagic"),
            Number(Boss.Queen, 7, "Blood Magic skill", FeatureKind.SkillLevels, 12f, PowerEffect.SkillLevel, "BloodMagic"),

            Resist(Boss.Fader, 1, "Fire", Resistance.Resistant),
            Number(Boss.Fader, 2, "Adrenaline gain", F, 0.5f, PowerEffect.AdrenalineGain),
            Number(Boss.Fader, 3, "Stagger taken", F, -0.25f, PowerEffect.StaggerTaken),
            Number(Boss.Fader, 4, "Attack stamina cost", F, -0.2f, PowerEffect.AttackStaminaCost),
            Number(Boss.Fader, 5, "Armor", FeatureKind.Armor, 8f, PowerEffect.Armor),
            Number(Boss.Fader, 6, "Timed block bonus", F, 0.5f, PowerEffect.TimedBlockBonus),
            Number(Boss.Fader, 7, "Health regeneration", F, 0.2f, PowerEffect.HealthRegen),

            // Kall Fimbulbringer: master of the endless winter. Frost first, then enduring and wielding it, the last Forsaken's might at 7.
            Resist(Boss.Kall, 1, "Frost", Resistance.VeryResistant),
            Number(Boss.Kall, 2, "Frost damage", F, 0.15f, PowerEffect.DamageDealt, "Frost"),
            Number(Boss.Kall, 3, "Health regeneration", F, 0.15f, PowerEffect.HealthRegen),
            Number(Boss.Kall, 4, "Movement speed", F, 0.05f, PowerEffect.MovementSpeed),
            Number(Boss.Kall, 5, "Armor", FeatureKind.Armor, 10f, PowerEffect.Armor),
            Number(Boss.Kall, 6, "Stamina regeneration", F, 0.2f, PowerEffect.StaminaRegen),
            Number(Boss.Kall, 7, "All damage", F, 0.05f, PowerEffect.DamageDealt, "All"),
        };

        /// <summary>All 56 features, power by power, feature 1 to 7.</summary>
        public static IReadOnlyList<PowerFeature> Features { get; } = Array.AsReadOnly(features);

        public static IReadOnlyList<PowerFeature> For(Boss boss) => features.Where(f => f.Power == boss).ToList();

        /// <summary>Design 4.3.1: the range of a numeric feature kind; null for resistances and toggles.</summary>
        public static (float Min, float Max)? Range(FeatureKind kind)
        {
            switch (kind)
            {
                case FeatureKind.Fraction: return (-1f, 5f);
                case FeatureKind.CarryWeight: return (0f, 1000f);
                case FeatureKind.Armor: return (0f, 200f);
                case FeatureKind.SkillLevels: return (0f, 100f);
                case FeatureKind.HealthTick: return (-50f, 50f);
                default: return null;
            }
        }
    }
}
