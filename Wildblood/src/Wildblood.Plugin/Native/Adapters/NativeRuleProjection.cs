// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Linq;
using ExtraPotions.Wildblood.Config;
using ExtraPotions.Wildblood.Integration;
using ExtraPotions.Wildblood.Runtime;
using CoreStarMode = ExtraPotions.Wildblood.Stars.StarMode;
namespace ExtraPotions.Wildblood.Adopted.Rules {
    /// <summary>Applies effective C01 settings to a freshly parsed source rule set; advanced YAML remains its source.</summary>
    internal static class NativeRuleProjection {
        // Received C01 values do not mutate ConfigEntry.Value or raise Config.SettingChanged.
        // Compare only inputs projected into this source snapshot at the existing bounded rule poll.
        internal static string InputsStamp(WildbloodConfig settings) {
            var stamp = new System.Text.StringBuilder();
            foreach (object value in new object[] {
                WildbloodModules.Active(WildbloodModule.Stars),
                WildbloodModules.Active(WildbloodModule.Traits),
                WildbloodModules.Active(WildbloodModule.Bosses),
                WildbloodModules.Active(WildbloodModule.Loot),
                settings.Get(WildbloodSettings.Stars.Difficulty),
                settings.Get(WildbloodSettings.Stars.StarMode),
                settings.Get(WildbloodSettings.Stars.MaxStars),
                settings.Get(WildbloodSettings.Stars.RespectEnemyLevelUpRate),
                settings.Get(WildbloodSettings.Traits.MaxMutations),
                settings.Get(WildbloodSettings.Traits.LargeStarPower),
                settings.Get(WildbloodSettings.WorldTier.Enabled),
                settings.Get(WildbloodSettings.WorldTier.BossKeys),
                settings.Get(WildbloodSettings.WorldTier.Override),
                settings.Get(WildbloodSettings.WorldTier.Announce),
                settings.Get(WildbloodSettings.Bosses.BossStars),
                settings.Get(WildbloodSettings.Bosses.Aspects),
                settings.Get(WildbloodSettings.Bosses.AltarShiftSeconds),
                settings.Get(WildbloodSettings.Loot.Mode),
                settings.Get(WildbloodSettings.Loot.AmountPerStar),
                settings.Get(WildbloodSettings.Loot.AmountPerStarBosses),
                settings.Get(WildbloodSettings.Loot.ExtraRollChance),
                settings.Get(WildbloodSettings.Loot.MaxExtraRolls),
                settings.Get(WildbloodSettings.Loot.MultiplyTrophies)
            }) {
                string text = Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? "";
                stamp.Append(text.Length).Append(':').Append(text).Append(';');
            }
            return stamp.ToString();
        }
        internal static void Apply(RuleSet rules, WildbloodConfig settings) {
            rules.LockToServer = true;
            rules.Difficulty = (Difficulty)Enum.Parse(typeof(Difficulty), settings.Get(WildbloodSettings.Stars.Difficulty).ToString());
            RuleParser.PadStarPower(rules);
            var mode = settings.Get(WildbloodSettings.Stars.StarMode);
            rules.CreatureStars = WildbloodModules.Active(WildbloodModule.Stars) && mode == CoreStarMode.Wildblood;
            rules.MaxStars = settings.Get(WildbloodSettings.Stars.MaxStars);
            rules.RespectLevelUpRate = settings.Get(WildbloodSettings.Stars.RespectEnemyLevelUpRate);
            rules.MaxMutations = settings.Get(WildbloodSettings.Traits.MaxMutations);
            if (!WildbloodModules.Active(WildbloodModule.Traits))
                foreach (Traits.Mutation mutation in Enum.GetValues(typeof(Traits.Mutation))) rules.MutationEnabled[mutation] = false;
            var largePower = settings.Get(WildbloodSettings.Traits.LargeStarPower);
            rules.Defaults.LargeStarPower = largePower;
            foreach (var biome in rules.Biomes.Values) biome.LargeStarPower = largePower;
            rules.Tiers.Enabled = WildbloodModules.Active(WildbloodModule.Stars) && settings.Get(WildbloodSettings.WorldTier.Enabled);
            rules.Tiers.BossKeys = NameList.Parse(settings.Get(WildbloodSettings.WorldTier.BossKeys)).ToList();
            rules.Tiers.Override = settings.Get(WildbloodSettings.WorldTier.Override);
            rules.Tiers.Announce = settings.Get(WildbloodSettings.WorldTier.Announce);
            rules.Boss.Enabled = WildbloodModules.Active(WildbloodModule.Bosses) && settings.Get(WildbloodSettings.Bosses.BossStars);
            rules.Boss.Aspects.Enabled = WildbloodModules.Active(WildbloodModule.Bosses) && settings.Get(WildbloodSettings.Bosses.Aspects);
            rules.Boss.Aspects.ShiftSeconds = settings.Get(WildbloodSettings.Bosses.AltarShiftSeconds);
            rules.Boss.Aspects.ShiftHours = null;
            rules.Loot.Mode = WildbloodModules.Active(WildbloodModule.Loot)
                ? (LootMode)Enum.Parse(typeof(LootMode), settings.Get(WildbloodSettings.Loot.Mode).ToString()) : LootMode.Vanilla;
            rules.Loot.AmountPerStar = settings.Get(WildbloodSettings.Loot.AmountPerStar);
            rules.Loot.AmountPerStarBosses = settings.Get(WildbloodSettings.Loot.AmountPerStarBosses);
            rules.Loot.ExtraRollChance = PercentList.Parse(settings.Get(WildbloodSettings.Loot.ExtraRollChance)).ToArray();
            rules.Loot.MaxExtraRolls = settings.Get(WildbloodSettings.Loot.MaxExtraRolls);
            rules.Loot.MultiplyTrophies = settings.Get(WildbloodSettings.Loot.MultiplyTrophies);
            // Multiplier cheats read the live C02 gate at the drop seam; YAML cannot bypass that gate.
            rules.Loot.GlobalMultiplier = rules.Loot.BossMultiplier = 1f;
        }
    }
}
