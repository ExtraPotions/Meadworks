// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
using System.Collections.Generic;
using ExtraPotions.Mantle.Embedded.YamlConfig;

namespace Mantle.Accessories
{
    /// <summary>
    /// The parsed Mantle.Accessories*.yml files: under <c>accessories:</c>, one entry per item prefab, each key optional:
    /// <c>slot</c> (ring or belt; needed to tag an item that is not one of Mantle's), <c>enabled</c>, <c>station</c>, <c>level</c> (1 to 10)
    /// and <c>cost</c> (Mantle's own items), and the stats <c>max_health</c>, <c>max_stamina</c>, <c>max_eitr</c>, <c>carry</c> (0 to 1000,
    /// flat) and <c>stamina_regen</c>, <c>move_speed</c>, <c>attack_stamina</c> (percent, -100 to 100; an attack's stamina falls with
    /// a negative number). A key left out keeps the built-in value; a value out of range is an error, which rejects the file
    /// set so the previous values stay.
    /// </summary>
    public sealed class AccessoriesModel : YamlModel
    {
        public const float MaxFlat = 1000f;
        public const float MaxPercent = 100f;

        public List<AccessoryOverride> Overrides { get; } = new List<AccessoryOverride>();

        protected override void Read(YamlNode root)
        {
            YamlNode accessories = root.Get("accessories");
            if (accessories.Kind == YamlNodeKind.Map)
            {
                foreach (KeyValuePair<string, YamlNode> entry in accessories.Entries)
                    ReadEntry(entry.Key, entry.Value);
            }
            else if (accessories.Kind != YamlNodeKind.Missing && accessories.Kind != YamlNodeKind.Null)
            {
                accessories.Error("expected one entry per item prefab, as in 'Mantle_CopperRing: { max_health: 10 }'");
            }
        }

        private void ReadEntry(string id, YamlNode node)
        {
            if (node.Kind != YamlNodeKind.Map)
            {
                if (node.Kind != YamlNodeKind.Null)
                    node.Error("an item takes slot:, enabled:, station:, level:, cost: and its stats, as in { slot: belt, carry: 150 }");
                return;
            }
            AccessoryOverride change = new AccessoryOverride { Id = id.Trim() };
            if (node.Get("slot").TryString(out string slot))
            {
                if (slot.Trim().Equals("ring", System.StringComparison.OrdinalIgnoreCase))
                    change.Slot = AccessorySlot.Ring;
                else if (slot.Trim().Equals("belt", System.StringComparison.OrdinalIgnoreCase))
                    change.Slot = AccessorySlot.Belt;
                else
                    node.Get("slot").Error("must be ring or belt");
            }
            if (node.Get("enabled").TryBool(out bool enabled))
                change.Enabled = enabled;
            if (node.Get("station").TryString(out string station) && station.Trim().Length > 0)
                change.Station = station.Trim();
            YamlNode level = node.Get("level");
            if (level.TryInt(out int levelValue))
            {
                if (levelValue >= 1 && levelValue <= 10)
                    change.Level = levelValue;
                else
                    level.Error($"must be a whole number from 1 to 10, found {levelValue}");
            }
            if (node.Get("cost").TryString(out string cost) && cost.Trim().Length > 0)
                change.Cost = cost.Trim();
            change.MaxHealth = Number(node.Get("max_health"), 0f, MaxFlat);
            change.MaxStamina = Number(node.Get("max_stamina"), 0f, MaxFlat);
            change.MaxEitr = Number(node.Get("max_eitr"), 0f, MaxFlat);
            change.Carry = Number(node.Get("carry"), 0f, MaxFlat);
            change.StaminaRegenPercent = Number(node.Get("stamina_regen"), -MaxPercent, MaxPercent);
            change.MoveSpeedPercent = Number(node.Get("move_speed"), -MaxPercent, MaxPercent);
            change.AttackStaminaPercent = Number(node.Get("attack_stamina"), -MaxPercent, MaxPercent);
            Overrides.Add(change);
        }

        private static float? Number(YamlNode node, float min, float max)
        {
            if (!node.TryFloat(out float value))
                return null;
            if (value >= min && value <= max)
                return value;
            node.Error($"must be a number from {min} to {max}, found {value}");
            return null;
        }
    }
}
