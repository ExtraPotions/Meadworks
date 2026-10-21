// Cairn - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original; same setting model as Edge.Core, Wildblood.Core and Ember.Core (expDARE), plus the C01 hidden flag.
using System;
using System.Collections.Generic;

namespace ExtraPotions.Cairn.Config
{
    /// <summary>
    /// Cairn's mirror of Crucible's <c>ConfigScope</c> (Cairn.Core references nothing beyond netstandard).
    /// Local: per player. ServerAuthoritative: the server's value when Cairn runs there. Cheat: server-bound, safe value
    /// while blocked.
    /// </summary>
    public enum SettingScope { Local, ServerAuthoritative, Cheat }

    /// <summary>
    /// One configuration entry from design section 4: where it lives, its scope, default, range or choices, the safe value
    /// of a cheat, whether it is a product-managed hidden entry (C01 <c>BindHidden</c>), and the pure rule that turns any
    /// stored value into one Cairn acts on.
    /// </summary>
    public abstract class CairnSetting
    {
        private static readonly IReadOnlyList<string> NoChoices = Array.Empty<string>();

        private protected CairnSetting(string section, string key, SettingScope scope, string description, IReadOnlyList<string>? choices, bool hidden)
        {
            if (string.IsNullOrWhiteSpace(section) || section.Trim() != section) throw new ArgumentException("Expected a trimmed section name.", nameof(section));
            if (string.IsNullOrWhiteSpace(key) || key.Trim() != key) throw new ArgumentException("Expected a trimmed key.", nameof(key));
            if (!Enum.IsDefined(typeof(SettingScope), scope)) throw new ArgumentException("Unknown scope.", nameof(scope));
            if (string.IsNullOrWhiteSpace(description)) throw new ArgumentException("Expected a description.", nameof(description));
            if (hidden && scope == SettingScope.Cheat) throw new ArgumentException(section + "." + key + ": a cheat cannot be hidden; cheats stay visible for review.", nameof(hidden));
            Section = section;
            Key = key;
            Scope = scope;
            Description = description;
            Choices = choices ?? NoChoices;
            Hidden = hidden;
        }

        public string Section { get; }

        public string Key { get; }

        /// <summary><c>Section.Key</c>.</summary>
        public string Id => Section + "." + Key;

        public SettingScope Scope { get; }

        public string Description { get; }

        /// <summary>Enum names in declaration order for choice settings; empty otherwise.</summary>
        public IReadOnlyList<string> Choices { get; }

        /// <summary>Product-managed entry that configuration UIs do not list (bound through C01 <c>BindHidden</c>).</summary>
        public bool Hidden { get; }

        public abstract Type ValueType { get; }

        public abstract object DefaultValue { get; }

        /// <summary>The value a blocked cheat evaluates to; null for other scopes.</summary>
        public abstract object? SafeValue { get; }

        /// <summary>Range bounds for numbers; null when the setting has no range.</summary>
        public abstract object? Minimum { get; }

        public abstract object? Maximum { get; }

        /// <summary>Normalizes a boxed value; a null or wrongly typed value gives the default.</summary>
        public abstract object NormalizeBoxed(object? value);

        public override string ToString() => Id + " (" + Scope + (Hidden ? ", hidden" : "") + ")";

        public static CairnSetting<bool> Toggle(string section, string key, bool defaultValue, SettingScope scope, string description, bool? safe = null, bool hidden = false) =>
            new CairnSetting<bool>(section, key, scope, description, defaultValue, safe, null, v => v, null, hidden);

        public static CairnSetting<int> Integer(string section, string key, int defaultValue, int min, int max, SettingScope scope, string description)
        {
            if (min > max) throw new ArgumentException("Minimum above maximum.", nameof(min));
            return new CairnSetting<int>(section, key, scope, description, defaultValue, null, (min, max), v => Math.Min(max, Math.Max(min, v)));
        }

        public static CairnSetting<float> Number(string section, string key, float defaultValue, float min, float max, SettingScope scope, string description)
        {
            if (!(min <= max) || float.IsInfinity(min) || float.IsInfinity(max)) throw new ArgumentException("Expected a finite range.", nameof(min));
            return new CairnSetting<float>(section, key, scope, description, defaultValue, null, (min, max),
                v => float.IsNaN(v) || float.IsInfinity(v) ? defaultValue : Math.Min(max, Math.Max(min, v)));
        }

        public static CairnSetting<TEnum> Choice<TEnum>(string section, string key, TEnum defaultValue, SettingScope scope, string description) where TEnum : struct, Enum =>
            new CairnSetting<TEnum>(section, key, scope, description, defaultValue, null, null,
                v => Enum.IsDefined(typeof(TEnum), v) ? v : defaultValue, Enum.GetNames(typeof(TEnum)));

        /// <summary>A text setting; <paramref name="normalize"/> maps any stored text (null included) to the canonical text.</summary>
        public static CairnSetting<string> Text(string section, string key, string defaultValue, SettingScope scope, string description, Func<string, string> normalize, bool hidden = false)
        {
            if (normalize == null) throw new ArgumentNullException(nameof(normalize));
            return new CairnSetting<string>(section, key, scope, description, defaultValue, null, null, normalize, null, hidden);
        }
    }

    public sealed class CairnSetting<T> : CairnSetting
    {
        private readonly Func<T, T> normalize;
        private readonly bool hasSafe;
        private readonly (T Min, T Max)? range;

        internal CairnSetting(string section, string key, SettingScope scope, string description, T defaultValue, object? safe, (T, T)? range,
            Func<T, T> normalize, IReadOnlyList<string>? choices = null, bool hidden = false)
            : base(section, key, scope, description, choices, hidden)
        {
            this.normalize = normalize ?? throw new ArgumentNullException(nameof(normalize));
            this.range = range;
            if (!EqualityComparer<T>.Default.Equals(normalize(defaultValue), defaultValue))
                throw new ArgumentException(Id + ": the default is outside its own rule.", nameof(defaultValue));
            Default = defaultValue;
            if (scope == SettingScope.Cheat)
            {
                if (safe == null) throw new ArgumentException(Id + ": a cheat needs a safe value.", nameof(safe));
                if (!EqualityComparer<T>.Default.Equals(defaultValue, (T)safe))
                    throw new ArgumentException(Id + ": a cheat must default to its safe value.", nameof(defaultValue));
                Safe = (T)safe;
                hasSafe = true;
            }
            else
            {
                if (safe != null) throw new ArgumentException(Id + ": only cheats have a safe value.", nameof(safe));
                Safe = defaultValue;
            }
        }

        public T Default { get; }

        /// <summary>The safe value for a cheat; the default for other scopes.</summary>
        public T Safe { get; }

        /// <summary>Clamps numbers, replaces undefined choices, non-finite numbers and invalid texts with the default.</summary>
        public T Normalize(T value) => normalize(value);

        public override Type ValueType => typeof(T);

        public override object DefaultValue => Default!;

        public override object? SafeValue => hasSafe ? Safe : null;

        public override object? Minimum => range.HasValue ? range.Value.Min : null;

        public override object? Maximum => range.HasValue ? range.Value.Max : null;

        public override object NormalizeBoxed(object? value) => value is T typed ? Normalize(typed)! : Default!;
    }
}
