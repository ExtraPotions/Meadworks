// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Mantle.Trinkets
{
    public enum TrinketPreset { Native, Conservative, Custom }

    public sealed class TrinketRow
    {
        public TrinketRow(string prefabId, float threshold, float duration, float killFraction,
            IReadOnlyDictionary<string, float> effects)
        {
            PrefabId = prefabId;
            Threshold = threshold;
            Duration = duration;
            KillFraction = killFraction;
            var copy = new Dictionary<string, float>(StringComparer.Ordinal);
            if (effects != null) foreach (var pair in effects) copy.Add(pair.Key, pair.Value);
            Effects = new ReadOnlyDictionary<string, float>(copy);
        }

        public string PrefabId { get; }
        public float Threshold { get; }
        public float Duration { get; }
        public float KillFraction { get; }
        public IReadOnlyDictionary<string, float> Effects { get; }
        public TrinketRow WithKillFraction(float fraction) => new TrinketRow(PrefabId, Threshold, Duration, fraction, Effects);
    }

    public sealed class TrinketTable
    {
        public static IReadOnlyList<string> NativeIds { get; } = Array.AsReadOnly(new[]
        {
            "TrinketBronzeHealth", "TrinketBronzeStamina", "TrinketChitinSwim", "TrinketIronHealth",
            "TrinketIronStamina", "TrinketSilverDamage", "TrinketSilverResist", "TrinketBlackDamageHealth",
            "TrinketBlackStamina", "TrinketCarapaceEitr", "TrinketScaleStaminaDamage", "TrinketFlametalEitr",
            "TrinketFlametalStaminaHealth", "TrinketBloodGoldHealth", "TrinketBloodGoldStamina"
        });

        private static readonly HashSet<string> nativeIds = new HashSet<string>(NativeIds, StringComparer.Ordinal);

        private TrinketTable(Dictionary<string, TrinketRow> rows)
        {
            Rows = new ReadOnlyDictionary<string, TrinketRow>(rows);
        }

        public IReadOnlyDictionary<string, TrinketRow> Rows { get; }
        public bool TryGet(string prefabId, out TrinketRow row)
        {
            row = null;
            return prefabId != null && Rows.TryGetValue(prefabId, out row);
        }

        public static bool TryCreateNative(IEnumerable<TrinketRow> rows, out TrinketTable table, out string error) =>
            TryCreate(rows, null, out table, out error);

        public static bool TryCreateCustom(IEnumerable<TrinketRow> rows, TrinketTable native,
            out TrinketTable table, out string error)
        {
            if (native == null)
            {
                table = null;
                error = "Native trinket data is unavailable.";
                return false;
            }
            return TryCreate(rows, native, out table, out error);
        }

        public static TrinketTable ForPreset(TrinketPreset preset, TrinketTable native, TrinketTable custom = null)
        {
            if (native == null) return null;
            if (preset == TrinketPreset.Custom) return custom ?? native;
            if (preset != TrinketPreset.Conservative) return native;
            var rows = new Dictionary<string, TrinketRow>(StringComparer.Ordinal);
            foreach (var pair in native.Rows) rows.Add(pair.Key, pair.Value.WithKillFraction(0.05f));
            return new TrinketTable(rows);
        }

        private static bool TryCreate(IEnumerable<TrinketRow> source, TrinketTable native,
            out TrinketTable table, out string error)
        {
            table = null;
            error = null;
            if (source == null) { error = "A complete trinket table is required."; return false; }
            var rows = new Dictionary<string, TrinketRow>(StringComparer.Ordinal);
            foreach (var row in source)
            {
                if (row == null || row.PrefabId == null || !nativeIds.Contains(row.PrefabId))
                { error = "Unknown native trinket prefab."; return false; }
                if (rows.ContainsKey(row.PrefabId))
                { error = "Duplicate trinket prefab: " + row.PrefabId; return false; }
                if (!InRange(row.Threshold, 1f, 200f) || !InRange(row.Duration, 0.1f, 300f) ||
                    !InRange(row.KillFraction, 0f, 0.10f) || (native == null && row.KillFraction != 0f))
                { error = "Invalid threshold, duration or kill fraction: " + row.PrefabId; return false; }
                if (row.Effects.Count == 0 || row.Effects.Count > 24)
                { error = "Native effect fields are required: " + row.PrefabId; return false; }
                TrinketRow original = null;
                if (native != null && (!native.TryGet(row.PrefabId, out original) || original.Effects.Count != row.Effects.Count))
                { error = "Effect fields must match the native trinket: " + row.PrefabId; return false; }
                foreach (var effect in row.Effects)
                {
                    if (!ValidEffect(effect.Key, effect.Value) || (original != null && !original.Effects.ContainsKey(effect.Key)))
                    { error = "Invalid or unknown effect field: " + row.PrefabId + "/" + effect.Key; return false; }
                }
                rows.Add(row.PrefabId, row);
            }
            if (rows.Count != NativeIds.Count)
            { error = "A trinket table must contain all 15 native prefabs."; return false; }
            table = new TrinketTable(rows);
            return true;
        }

        private static bool InRange(float value, float minimum, float maximum) =>
            KillGain.Finite(value) && value >= minimum && value <= maximum;

        private static bool ValidEffect(string field, float value)
        {
            switch (field)
            {
                case "m_healthUpFront": case "m_staminaUpFront": case "m_eitrUpFront":
                    return InRange(value, 0f, 200f);
                case "m_skillLevelModifier": case "m_skillLevelModifier2":
                    return InRange(value, 0f, 30f);
                case "m_addArmor":
                    return InRange(value, 0f, 40f);
                case "m_healthRegenMultiplier": case "m_staminaRegenMultiplier": case "m_eitrRegenMultiplier":
                    return InRange(value, 1f, 2f);
                case "m_timedBlockBonus":
                    return InRange(value, 0f, 1f);
                case "m_runStaminaDrainModifier": case "m_attackStaminaUseModifier":
                case "m_blockStaminaUseModifier": case "m_swimStaminaUseModifier":
                    return InRange(value, -1f, 0f);
                case "m_speedModifier": case "m_swimSpeedModifier":
                    return InRange(value, 0f, 0.5f);
                case "m_percentigeDamageModifiers.m_blunt": case "m_percentigeDamageModifiers.m_slash": case "m_percentigeDamageModifiers.m_pierce":
                    return InRange(value, 0f, 1f);
                case "resistance.Blunt": case "resistance.Slash": case "resistance.Pierce":
                    return ValidResistance(value);
                default:
                    return false;
            }
        }

        private static bool ValidResistance(float value) => InRange(value, 0f, 8f) && value == (int)value;
    }
}
