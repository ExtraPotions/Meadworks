// Thing - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original; same setting model as Tempest.Core, Keel.Core and Ember.Core (expDARE), without the Cheat scope.
using System;
using System.Collections.Generic;

namespace ExtraPotions.Thing.Config
{
    /// <summary>
    /// Thing's mirror of Crucible's <c>ConfigScope</c> (Thing.Core references nothing beyond netstandard). Local: per player,
    /// lockable by the server. ServerAuthoritative: the server's value when Thing runs there; admins amend it. Thing has no
    /// Cheat settings (design section 5): its actions are admin powers gated by admin identity.
    /// </summary>
    public enum SettingScope { Local, ServerAuthoritative }

    /// <summary>
    /// One configuration entry from design section 4: where it lives, its scope, default, range or choices, whether it is a
    /// keyboard shortcut (bound as BepInEx <c>KeyboardShortcut</c> by the plugin, stored here as its serialized text), and
    /// the pure rule that turns any stored value into one Thing acts on.
    /// </summary>
    public abstract class ThingSetting
    {
        private static readonly IReadOnlyList<string> NoChoices = Array.Empty<string>();

        private protected ThingSetting(string section, string key, SettingScope scope, string description, IReadOnlyList<string>? choices, bool hotkey)
        {
            if (string.IsNullOrWhiteSpace(section) || section.Trim() != section) throw new ArgumentException("Expected a trimmed section name.", nameof(section));
            if (string.IsNullOrWhiteSpace(key) || key.Trim() != key) throw new ArgumentException("Expected a trimmed key.", nameof(key));
            if (!Enum.IsDefined(typeof(SettingScope), scope)) throw new ArgumentException("Unknown scope.", nameof(scope));
            if (string.IsNullOrWhiteSpace(description)) throw new ArgumentException("Expected a description.", nameof(description));
            if (hotkey && scope != SettingScope.Local) throw new ArgumentException(section + "." + key + ": a keyboard shortcut is a Local setting.", nameof(hotkey));
            Section = section;
            Key = key;
            Scope = scope;
            Description = description;
            Choices = choices ?? NoChoices;
            Hotkey = hotkey;
        }

        public string Section { get; }

        public string Key { get; }

        /// <summary><c>Section.Key</c>, the form the Locked Player Settings list uses.</summary>
        public string Id => Section + "." + Key;

        public SettingScope Scope { get; }

        public string Description { get; }

        /// <summary>Enum names in declaration order for choice settings; empty otherwise.</summary>
        public IReadOnlyList<string> Choices { get; }

        /// <summary>A keyboard shortcut; the value is BepInEx's serialized shortcut text (empty: unassigned).</summary>
        public bool Hotkey { get; }

        /// <summary>Only Local settings can be named in Locked Player Settings.</summary>
        public bool Lockable => Scope == SettingScope.Local;

        public abstract Type ValueType { get; }

        public abstract object DefaultValue { get; }

        /// <summary>Range bounds for numbers; null when the setting has no range.</summary>
        public abstract object? Minimum { get; }

        public abstract object? Maximum { get; }

        /// <summary>Normalizes a boxed value; a null or wrongly typed value gives the default.</summary>
        public abstract object NormalizeBoxed(object? value);

        public override string ToString() => Id + " (" + Scope + ")";

        public static ThingSetting<bool> Toggle(string section, string key, bool defaultValue, SettingScope scope, string description) =>
            new ThingSetting<bool>(section, key, scope, description, defaultValue, null, v => v);

        public static ThingSetting<int> Integer(string section, string key, int defaultValue, int min, int max, SettingScope scope, string description)
        {
            if (min > max) throw new ArgumentException("Minimum above maximum.", nameof(min));
            return new ThingSetting<int>(section, key, scope, description, defaultValue, (min, max), v => Math.Min(max, Math.Max(min, v)));
        }

        public static ThingSetting<TEnum> Choice<TEnum>(string section, string key, TEnum defaultValue, SettingScope scope, string description) where TEnum : struct, Enum =>
            new ThingSetting<TEnum>(section, key, scope, description, defaultValue, null,
                v => Enum.IsDefined(typeof(TEnum), v) ? v : defaultValue, Enum.GetNames(typeof(TEnum)));

        /// <summary>A text setting; <paramref name="normalize"/> maps any stored text (null included) to the canonical text.</summary>
        public static ThingSetting<string> Text(string section, string key, string defaultValue, SettingScope scope, string description, Func<string, string> normalize)
        {
            if (normalize == null) throw new ArgumentNullException(nameof(normalize));
            return new ThingSetting<string>(section, key, scope, description, defaultValue, null, normalize);
        }

        /// <summary>A Local keyboard shortcut (C07); the default is BepInEx's serialized text, empty for unassigned.</summary>
        public static ThingSetting<string> Shortcut(string section, string key, string defaultValue, SettingScope scope, string description) =>
            new ThingSetting<string>(section, key, scope, description, defaultValue, null, v => (v ?? "").Trim(), null, hotkey: true);
    }

    public sealed class ThingSetting<T> : ThingSetting
    {
        private readonly Func<T, T> normalize;
        private readonly (T Min, T Max)? range;

        internal ThingSetting(string section, string key, SettingScope scope, string description, T defaultValue, (T, T)? range,
            Func<T, T> normalize, IReadOnlyList<string>? choices = null, bool hotkey = false)
            : base(section, key, scope, description, choices, hotkey)
        {
            this.normalize = normalize ?? throw new ArgumentNullException(nameof(normalize));
            this.range = range;
            if (!EqualityComparer<T>.Default.Equals(normalize(defaultValue), defaultValue))
                throw new ArgumentException(Id + ": the default is outside its own rule.", nameof(defaultValue));
            Default = defaultValue;
        }

        public T Default { get; }

        /// <summary>Clamps numbers, replaces undefined choices and invalid texts.</summary>
        public T Normalize(T value) => normalize(value);

        public override Type ValueType => typeof(T);

        public override object DefaultValue => Default!;

        public override object? Minimum => range.HasValue ? range.Value.Min : null;

        public override object? Maximum => range.HasValue ? range.Value.Max : null;

        public override object NormalizeBoxed(object? value) => value is T typed ? Normalize(typed)! : Default!;
    }
}
