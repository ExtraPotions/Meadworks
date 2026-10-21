// Edge - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: which secondary a weapon gets (design A1 to A3, A6, A12), from the documented SecondaryAttacks
// behavior (README and YAML headers: group defaults, preset: none, enabled: false examples) and the design tables.
using System;
using System.Collections.Generic;
using System.Linq;
using ExtraPotions.Edge.Config;

namespace ExtraPotions.Edge.Secondaries
{
    /// <summary>The server's ranged group choices (design 4.2).</summary>
    public sealed class RangedChoices
    {
        public RangedChoices(RangedPreset oneHandedElemental, RangedPreset fireballStaff, RangedPreset rapidStaff, RangedPreset lightningStaff,
            RangedPreset bow, RangedPreset crossbow, BombPreset bomb)
        {
            OneHandedElemental = oneHandedElemental;
            FireballStaff = fireballStaff;
            RapidStaff = rapidStaff;
            LightningStaff = lightningStaff;
            Bow = bow;
            Crossbow = crossbow;
            Bomb = bomb;
        }

        public static RangedChoices Defaults { get; } = new RangedChoices(
            EdgeSettings.Ranged.OneHandedElementalPreset.Default, EdgeSettings.Ranged.FireballStaffPreset.Default, EdgeSettings.Ranged.RapidfireStaffPreset.Default,
            EdgeSettings.Ranged.LightningStaffPreset.Default, EdgeSettings.Ranged.BowPreset.Default, EdgeSettings.Ranged.CrossbowPreset.Default,
            EdgeSettings.Ranged.BombPreset.Default);

        public RangedPreset OneHandedElemental { get; }
        public RangedPreset FireballStaff { get; }
        public RangedPreset RapidStaff { get; }
        public RangedPreset LightningStaff { get; }
        public RangedPreset Bow { get; }
        public RangedPreset Crossbow { get; }
        public BombPreset Bomb { get; }

        public RangedChoices With(RangedPreset? oneHandedElemental = null, RangedPreset? fireballStaff = null, RangedPreset? rapidStaff = null,
            RangedPreset? lightningStaff = null, RangedPreset? bow = null, RangedPreset? crossbow = null, BombPreset? bomb = null) =>
            new RangedChoices(oneHandedElemental ?? OneHandedElemental, fireballStaff ?? FireballStaff, rapidStaff ?? RapidStaff,
                lightningStaff ?? LightningStaff, bow ?? Bow, crossbow ?? Crossbow, bomb ?? Bomb);
    }

    /// <summary>One per-prefab YAML entry, reduced to what selection needs (D03b adds the attack shape fields).</summary>
    public sealed class PrefabEntry
    {
        public PrefabEntry(string prefab, string? preset, bool enabled)
        {
            Prefab = prefab ?? "";
            Preset = preset;
            Enabled = enabled;
        }

        public string Prefab { get; }

        /// <summary>A catalog key, <c>none</c> (keep the weapon's own secondary) or null (automatic).</summary>
        public string? Preset { get; }

        /// <summary><c>enabled: false</c> keeps an example in the file without effect.</summary>
        public bool Enabled { get; }
    }

    public enum ResolutionKind { EdgePreset, KeepOwn }

    public enum ResolutionReason { Automatic, PrefabEntry, PrefabNone, ForeignSecondary, GroupOff, NoFamily, ModuleOff }

    public readonly struct PresetResolution
    {
        private PresetResolution(ResolutionKind kind, string presetKey, ResolutionReason reason)
        {
            Kind = kind;
            PresetKey = presetKey;
            Reason = reason;
        }

        public ResolutionKind Kind { get; }

        /// <summary>The catalog key for <see cref="ResolutionKind.EdgePreset"/>; empty otherwise.</summary>
        public string PresetKey { get; }

        public ResolutionReason Reason { get; }

        internal static PresetResolution Preset(string key, ResolutionReason reason) => new PresetResolution(ResolutionKind.EdgePreset, key, reason);

        internal static PresetResolution Own(ResolutionReason reason) => new PresetResolution(ResolutionKind.KeepOwn, "", reason);

        public override string ToString() => Kind == ResolutionKind.EdgePreset ? PresetKey + " (" + Reason + ")" : "own secondary (" + Reason + ")";
    }

    public static class PresetSelection
    {
        public const string NonePreset = "none";

        /// <summary>
        /// The secondary of one weapon prefab. Order: module switch; the first enabled prefab entry (preset key or none);
        /// a secondary another mod gave the weapon stays (A12); then the automatic preset of its family (blood magic, then
        /// ranged with the server's group choices, then melee). Unknown or non-attack presets in an entry and duplicate
        /// enabled entries are reported in <paramref name="warnings"/> and fall through.
        /// </summary>
        public static PresetResolution Resolve(WeaponFacts weapon, RangedChoices choices, IEnumerable<PrefabEntry> entries, bool foreignSecondary,
            bool moduleEnabled, ICollection<string>? warnings = null)
        {
            if (weapon == null) throw new ArgumentNullException(nameof(weapon));
            if (choices == null) throw new ArgumentNullException(nameof(choices));
            if (!moduleEnabled) return PresetResolution.Own(ResolutionReason.ModuleOff);

            var enabled = (entries ?? Enumerable.Empty<PrefabEntry>())
                .Where(e => e != null && e.Enabled && string.Equals(e.Prefab, weapon.PrefabName, StringComparison.OrdinalIgnoreCase)).ToList();
            if (enabled.Count > 1)
                warnings?.Add(weapon.PrefabName + ": " + enabled.Count + " enabled entries; the first is used.");
            var entry = enabled.FirstOrDefault();
            if (entry != null && !string.IsNullOrWhiteSpace(entry.Preset))
            {
                var key = entry.Preset!.Trim();
                if (string.Equals(key, NonePreset, StringComparison.OrdinalIgnoreCase)) return PresetResolution.Own(ResolutionReason.PrefabNone);
                if (PresetCatalog.TryGet(key, out var info) && info.Group != PresetGroup.SummonQuality)
                    return PresetResolution.Preset(info.Key, ResolutionReason.PrefabEntry);
                warnings?.Add(weapon.PrefabName + ": preset \"" + key + "\" is not a secondary attack preset; the automatic preset is used.");
            }

            if (foreignSecondary) return PresetResolution.Own(ResolutionReason.ForeignSecondary);
            return Automatic(weapon, choices);
        }

        private static PresetResolution Automatic(WeaponFacts weapon, RangedChoices choices)
        {
            switch (WeaponFamilies.Blood(weapon))
            {
                case BloodFamily.Summon: return PresetResolution.Preset("summonEmpower", ResolutionReason.Automatic);
                case BloodFamily.Shield: return PresetResolution.Preset("shieldConvert", ResolutionReason.Automatic);
            }
            var ranged = WeaponFamilies.Ranged(weapon);
            if (ranged == RangedFamily.Bomb)
            {
                var bomb = choices.Bomb == BombPreset.Auto
                    ? (weapon.BombHasArea ? "overchargedBomb" : "stickyDetonator")
                    : PresetCatalog.Key(choices.Bomb);
                return bomb.Length == 0 ? PresetResolution.Own(ResolutionReason.GroupOff) : PresetResolution.Preset(bomb, ResolutionReason.Automatic);
            }
            if (ranged != RangedFamily.None)
            {
                var key = PresetCatalog.Key(Choice(ranged, choices));
                return key.Length == 0 ? PresetResolution.Own(ResolutionReason.GroupOff) : PresetResolution.Preset(key, ResolutionReason.Automatic);
            }
            var melee = MeleeKey(WeaponFamilies.Melee(weapon));
            return melee.Length == 0 ? PresetResolution.Own(ResolutionReason.NoFamily) : PresetResolution.Preset(melee, ResolutionReason.Automatic);
        }

        private static RangedPreset Choice(RangedFamily family, RangedChoices choices)
        {
            switch (family)
            {
                case RangedFamily.OneHandedElemental: return choices.OneHandedElemental;
                case RangedFamily.FireballStaff: return choices.FireballStaff;
                case RangedFamily.RapidStaff: return choices.RapidStaff;
                case RangedFamily.ReloadStaff: return choices.LightningStaff;
                case RangedFamily.Bow: return choices.Bow;
                case RangedFamily.Crossbow: return choices.Crossbow;
                default: return RangedPreset.Off;
            }
        }

        /// <summary>Design A3: one melee preset per weapon family.</summary>
        public static string MeleeKey(MeleeFamily family)
        {
            switch (family)
            {
                case MeleeFamily.Knives: return "sneakAmbush";
                case MeleeFamily.OneHandedSword: return "riftTrail";
                case MeleeFamily.TwoHandedSword: return "cleavingThrust";
                case MeleeFamily.OneHandedClub: return "launchSlam";
                case MeleeFamily.Unarmed: return "knockbackChain";
                case MeleeFamily.Sledge: return "aftershock";
                case MeleeFamily.Battleaxe: return "impactBurst";
                case MeleeFamily.Polearm: return "spinningSweep";
                case MeleeFamily.Spear: return "spearRain";
                case MeleeFamily.OneHandedAxe: return "boomerang";
                case MeleeFamily.Pickaxe: return "fractureLine";
                case MeleeFamily.Farming: return "harvestSweep";
                default: return "";
            }
        }
    }
}
