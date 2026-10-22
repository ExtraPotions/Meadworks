// Barter - ExtraPotions Meadworks trader overhaul.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Same shape as Kindle's KindleSetting (original code by the same author), reduced to what Barter needs.
using System;
using System.Collections.Generic;

namespace ExtraPotions.Barter.Config
{
    /// <summary>
    /// Barter's mirror of Crucible's <c>ConfigScope</c> (Barter.Core references nothing beyond netstandard; the plugin maps one to one).
    /// Local: the player's own. ServerAuthoritative: the server's value wins in multiplayer. Barter 0.1.0 has no cheat.
    /// </summary>
    public enum SettingScope { Local, ServerAuthoritative }

    /// <summary>One configuration entry: where it lives, its scope, default, range, and the pure rule that turns any stored value into one Barter acts on.</summary>
    public abstract class BarterSetting
    {
        private protected BarterSetting(string section, string key, SettingScope scope, string description)
        {
            if (string.IsNullOrWhiteSpace(section) || section.Trim() != section) throw new ArgumentException("Expected a trimmed section name.", nameof(section));
            if (string.IsNullOrWhiteSpace(key) || key.Trim() != key) throw new ArgumentException("Expected a trimmed key.", nameof(key));
            if (!Enum.IsDefined(typeof(SettingScope), scope)) throw new ArgumentException("Unknown scope.", nameof(scope));
            if (string.IsNullOrWhiteSpace(description)) throw new ArgumentException("Expected a description.", nameof(description));
            Section = section;
            Key = key;
            Scope = scope;
            Description = description;
        }

        public string Section { get; }

        public string Key { get; }

        /// <summary><c>Section.Key</c>.</summary>
        public string Id => Section + "." + Key;

        public SettingScope Scope { get; }

        public string Description { get; }

        public abstract Type ValueType { get; }

        public abstract object DefaultValue { get; }

        /// <summary>Range bounds for numbers; null when the setting has no range.</summary>
        public abstract object? Minimum { get; }

        public abstract object? Maximum { get; }

        /// <summary>Normalizes a boxed value; a null or wrongly typed value gives the default.</summary>
        public abstract object NormalizeBoxed(object? value);

        public override string ToString() => Id + " (" + Scope + ")";

        public static BarterSetting<bool> Toggle(string section, string key, bool defaultValue, SettingScope scope, string description) =>
            new BarterSetting<bool>(section, key, scope, description, defaultValue, null, v => v);

        public static BarterSetting<int> Integer(string section, string key, int defaultValue, int min, int max, SettingScope scope, string description)
        {
            if (min > max) throw new ArgumentException("Minimum above maximum.", nameof(min));
            return new BarterSetting<int>(section, key, scope, description, defaultValue, (min, max), v => Math.Min(max, Math.Max(min, v)));
        }

        public static BarterSetting<float> Number(string section, string key, float defaultValue, float min, float max, SettingScope scope, string description)
        {
            if (!(min <= max) || float.IsInfinity(min) || float.IsInfinity(max)) throw new ArgumentException("Expected a finite range.", nameof(min));
            return new BarterSetting<float>(section, key, scope, description, defaultValue, (min, max),
                v => float.IsNaN(v) || float.IsInfinity(v) ? defaultValue : Math.Min(max, Math.Max(min, v)));
        }

        /// <summary>A text setting; <paramref name="normalize"/> maps any stored text (null included) to the canonical text.</summary>
        public static BarterSetting<string> Text(string section, string key, string defaultValue, SettingScope scope, string description, Func<string, string> normalize)
        {
            if (normalize == null) throw new ArgumentNullException(nameof(normalize));
            return new BarterSetting<string>(section, key, scope, description, defaultValue, null, normalize);
        }
    }

    public sealed class BarterSetting<T> : BarterSetting
    {
        private readonly Func<T, T> normalize;
        private readonly (T Min, T Max)? range;

        internal BarterSetting(string section, string key, SettingScope scope, string description, T defaultValue, (T, T)? range, Func<T, T> normalize)
            : base(section, key, scope, description)
        {
            this.normalize = normalize ?? throw new ArgumentNullException(nameof(normalize));
            this.range = range;
            if (!EqualityComparer<T>.Default.Equals(normalize(defaultValue), defaultValue))
                throw new ArgumentException(Id + ": the default is outside its own rule.", nameof(defaultValue));
            Default = defaultValue;
        }

        public T Default { get; }

        /// <summary>Clamps numbers; replaces non-finite numbers with the default and canonicalizes text.</summary>
        public T Normalize(T value) => normalize(value);

        public override Type ValueType => typeof(T);

        public override object DefaultValue => Default!;

        public override object? Minimum => range.HasValue ? range.Value.Min : null;

        public override object? Maximum => range.HasValue ? range.Value.Max : null;

        public override object NormalizeBoxed(object? value) => value is T typed ? Normalize(typed)! : Default!;
    }
}
