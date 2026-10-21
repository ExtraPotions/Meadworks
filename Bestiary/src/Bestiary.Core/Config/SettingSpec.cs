// Bestiary - ExtraPotions Meadworks creatures and bone arsenal.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
using System;

namespace ExtraPotions.Bestiary.Config
{
    /// <summary>Crucible configuration scopes (C01, C02) as the design names them in section 4.</summary>
    public enum SettingScope
    {
        /// <summary>Personal setting; never synchronized.</summary>
        Local,

        /// <summary>The server's value binds every player in multiplayer; the player decides in single player.</summary>
        ServerAuthoritative,

        /// <summary>Bound with its safe value; the authority gate decides who may change it and reads the safe value when blocked.</summary>
        Cheat,
    }

    /// <summary>One row of design section 4: where a setting lives, who decides it, its default and range. Bestiary has no cheats (design 5); the scope exists for the shared Crucible mapping.</summary>
    public sealed class SettingSpec
    {
        public SettingSpec(string section, string key, SettingScope scope, object defaultValue, string description,
            double? minimum = null, double? maximum = null)
        {
            Section = section;
            Key = key;
            Scope = scope;
            Default = defaultValue ?? throw new ArgumentNullException(nameof(defaultValue));
            Description = description;
            Minimum = minimum;
            Maximum = maximum;
        }

        public string Section { get; }
        public string Key { get; }
        public string Id => Section + "." + Key;
        public SettingScope Scope { get; }

        /// <summary>Default value; for a cheat its safe value.</summary>
        public object Default { get; }

        public Type ValueType => Default.GetType();
        public double? Minimum { get; }
        public double? Maximum { get; }
        public string Description { get; }

        /// <summary>Keeps a number inside the range; a non-finite number reads the default. Other types pass through.</summary>
        public T Clamp<T>(T value)
        {
            switch (value)
            {
                case float f:
                    if (float.IsNaN(f) || float.IsInfinity(f)) return (T)Default;
                    return (T)(object)(float)Limit(f);
                case int i:
                    return (T)(object)(int)Limit(i);
                default:
                    return value;
            }
        }

        private double Limit(double value)
        {
            if (Minimum.HasValue && value < Minimum.Value) value = Minimum.Value;
            if (Maximum.HasValue && value > Maximum.Value) value = Maximum.Value;
            return value;
        }
    }
}
