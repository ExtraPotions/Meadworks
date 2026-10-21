// Edge - ExtraPotions Meadworks. SPDX-License-Identifier: GPL-3.0-only
// Portions from SecondaryAttacks 1.2.17 (sighsorry1029), GPL-3.0, at 4375a2b17297052a773c5b143b0cae21dc655ed8:
// SecondaryAttackPresetCatalog.cs (preset keys, token keys, groups, icon prefabs, order, case-insensitive lookup and
// canonicalization) and the RangedPresetSelection/BombPresetSelection key mapping of the same file.
// Changed by expDARE, 2026: Unity-free (icon resolution moves to the plugin), Edge token keys preset_<token>_name and
// _description resolved through Crucible C05 instead of $sa_compendium_*, fallback names and descriptions dropped (Edge's
// own catalog text lives in assets/localization), Edge's RangedPreset and BombPreset choices mapped to keys.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks) for the changes.
using System;
using System.Collections.Generic;
using ExtraPotions.Edge.Config;

namespace ExtraPotions.Edge.Secondaries
{
    public enum PresetGroup { Ranged, Melee, BloodMagic, SummonQuality }

    public sealed class PresetInfo
    {
        internal PresetInfo(string key, string tokenKey, PresetGroup group, string iconPrefabName)
        {
            Key = key;
            TokenKey = tokenKey;
            Group = group;
            IconPrefabName = iconPrefabName;
        }

        /// <summary>The preset name used in YAML <c>preset:</c> fields (SecondaryAttacks' key).</summary>
        public string Key { get; }

        public string TokenKey { get; }

        public PresetGroup Group { get; }

        /// <summary>Vanilla item prefab whose icon stands for the preset in the HUD and tooltips.</summary>
        public string IconPrefabName { get; }

        public string NameKey => "preset_" + TokenKey + "_name";

        public string DescriptionKey => "preset_" + TokenKey + "_description";

        public override string ToString() => Key;
    }

    public static class PresetCatalog
    {
        public static IReadOnlyList<PresetInfo> Entries { get; } = Array.AsReadOnly(new[]
        {
            new PresetInfo("barrage", "barrage", PresetGroup.Ranged, "BowHuntsman"),
            new PresetInfo("volley", "volley", PresetGroup.Ranged, "CrossbowArbalest"),
            new PresetInfo("piercing", "piercing", PresetGroup.Ranged, "StaffLightning"),
            new PresetInfo("scatter", "scatter", PresetGroup.Ranged, "StaffClusterbomb"),
            new PresetInfo("spiral", "spiral", PresetGroup.Ranged, "StaffIceShards"),
            new PresetInfo("sentinel", "sentinel", PresetGroup.Ranged, "StaffFireball"),
            new PresetInfo("meteor", "meteor", PresetGroup.Ranged, "StaffFireball"),
            new PresetInfo("burst", "burst", PresetGroup.Ranged, "CrossbowArbalest"),
            new PresetInfo("stickyDetonator", "sticky_detonator", PresetGroup.Ranged, "BombOoze"),
            new PresetInfo("overchargedBomb", "overcharged_bomb", PresetGroup.Ranged, "BombBile"),

            new PresetInfo("sneakAmbush", "sneak_ambush", PresetGroup.Melee, "KnifeWood"),
            new PresetInfo("riftTrail", "rift_trail", PresetGroup.Melee, "SwordWood"),
            new PresetInfo("cleavingThrust", "cleaving_thrust", PresetGroup.Melee, "THSwordWood"),
            new PresetInfo("launchSlam", "launch_slam", PresetGroup.Melee, "MaceWood"),
            new PresetInfo("aftershock", "aftershock", PresetGroup.Melee, "SledgeWood"),
            new PresetInfo("knockbackChain", "knockback_chain", PresetGroup.Melee, "FistBjornClaw"),
            new PresetInfo("boomerang", "boomerang", PresetGroup.Melee, "AxeBronze"),
            new PresetInfo("impactBurst", "impact_burst", PresetGroup.Melee, "Battleaxe"),
            new PresetInfo("spearRain", "spear_rain", PresetGroup.Melee, "SpearWood"),
            new PresetInfo("spinningSweep", "spinning_sweep", PresetGroup.Melee, "AtgeirWood"),
            new PresetInfo("fractureLine", "fracture_line", PresetGroup.Melee, "PickaxeAntler"),
            new PresetInfo("harvestSweep", "harvest_sweep", PresetGroup.Melee, "Scythe"),

            new PresetInfo("summonEmpower", "summon_empower", PresetGroup.BloodMagic, "StaffSkeleton"),
            new PresetInfo("shieldConvert", "shield_convert", PresetGroup.BloodMagic, "StaffShield"),

            new PresetInfo("countByQuality", "count_by_quality", PresetGroup.SummonQuality, "StaffRedTroll"),
            new PresetInfo("levelByQuality", "level_by_quality", PresetGroup.SummonQuality, "StaffSkeleton"),
        });

        private static readonly Dictionary<string, PresetInfo> ByKey = BuildByKey();

        public static bool TryGet(string? presetKey, out PresetInfo info) => ByKey.TryGetValue(presetKey ?? "", out info!);

        /// <summary>The catalog's spelling of a key; an unknown key is returned trimmed; blank gives empty.</summary>
        public static string Canonicalize(string? presetKey)
        {
            if (string.IsNullOrWhiteSpace(presetKey)) return "";
            var trimmed = presetKey!.Trim();
            return TryGet(trimmed, out var info) ? info.Key : trimmed;
        }

        /// <summary>The catalog key of a ranged choice; empty for Off.</summary>
        public static string Key(RangedPreset preset)
        {
            switch (preset)
            {
                case RangedPreset.Barrage: return "barrage";
                case RangedPreset.Volley: return "volley";
                case RangedPreset.Piercing: return "piercing";
                case RangedPreset.Scatter: return "scatter";
                case RangedPreset.Spiral: return "spiral";
                case RangedPreset.Sentinel: return "sentinel";
                case RangedPreset.Meteor: return "meteor";
                case RangedPreset.Burst: return "burst";
                default: return "";
            }
        }

        /// <summary>The catalog key of a fixed bomb choice; empty for Auto and Off (Auto is decided per bomb).</summary>
        public static string Key(BombPreset preset)
        {
            switch (preset)
            {
                case BombPreset.StickyDetonator: return "stickyDetonator";
                case BombPreset.OverchargedBomb: return "overchargedBomb";
                default: return "";
            }
        }

        private static Dictionary<string, PresetInfo> BuildByKey()
        {
            var entries = new Dictionary<string, PresetInfo>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in Entries) entries[entry.Key] = entry;
            return entries;
        }
    }
}
