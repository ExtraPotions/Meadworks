// Inscribe - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original; same setting model as Ember.Core, Kindle.Core and Wildblood.Core (expDARE), without the World scope.
using System;
using System.Collections.Generic;

namespace ExtraPotions.Inscribe.Config
{
    /// <summary>
    /// Inscribe's mirror of Crucible's <c>ConfigScope</c> (Inscribe.Core references nothing beyond netstandard).
    /// Local: per player. ServerAuthoritative: the server's value when Inscribe runs there; the player's in single player.
    /// Cheat: server-bound, safe value while blocked (design 4 and 5).
    /// </summary>
    public enum SettingScope { Local, ServerAuthoritative, Cheat }

    /// <summary>
    /// One configuration entry from design section 4: where it lives, its scope, default, range or choices, the safe
    /// value of a cheat, and the pure rule that turns any stored value into one Inscribe acts on.
    /// </summary>
    public abstract class InscribeSetting
    {
        private static readonly IReadOnlyList<string> NoChoices = Array.Empty<string>();

        private protected InscribeSetting(string section, string key, SettingScope scope, string description, IReadOnlyList<string>? choices)
        {
            if (string.IsNullOrWhiteSpace(section) || section.Trim() != section) throw new ArgumentException("Expected a trimmed section name.", nameof(section));
            if (string.IsNullOrWhiteSpace(key) || key.Trim() != key) throw new ArgumentException("Expected a trimmed key.", nameof(key));
            if (!Enum.IsDefined(typeof(SettingScope), scope)) throw new ArgumentException("Unknown scope.", nameof(scope));
            if (string.IsNullOrWhiteSpace(description)) throw new ArgumentException("Expected a description.", nameof(description));
            Section = section;
            Key = key;
            Scope = scope;
            Description = description;
            Choices = choices ?? NoChoices;
        }

        public string Section { get; }

        public string Key { get; }

        /// <summary><c>Section.Key</c>.</summary>
        public string Id => Section + "." + Key;

        public SettingScope Scope { get; }

        public string Description { get; }

        /// <summary>Enum names in declaration order for choice settings; empty otherwise.</summary>
        public IReadOnlyList<string> Choices { get; }

        public abstract Type ValueType { get; }

        public abstract object DefaultValue { get; }

        /// <summary>The value a blocked cheat evaluates to; null for other scopes.</summary>
        public abstract object? SafeValue { get; }

        /// <summary>Range bounds for numbers; null when the setting has no range.</summary>
        public abstract object? Minimum { get; }

        public abstract object? Maximum { get; }

        /// <summary>Normalizes a boxed value; a null or wrongly typed value gives the default.</summary>
        public abstract object NormalizeBoxed(object? value);

        public override string ToString() => Id + " (" + Scope + ")";

        public static InscribeSetting<bool> Toggle(string section, string key, bool defaultValue, SettingScope scope, string description, bool? safe = null) =>
            new InscribeSetting<bool>(section, key, scope, description, defaultValue, safe, null, v => v);

        public static InscribeSetting<int> Integer(string section, string key, int defaultValue, int min, int max, SettingScope scope, string description)
        {
            if (min > max) throw new ArgumentException("Minimum above maximum.", nameof(min));
            return new InscribeSetting<int>(section, key, scope, description, defaultValue, null, (min, max), v => Math.Min(max, Math.Max(min, v)));
        }

        public static InscribeSetting<float> Number(string section, string key, float defaultValue, float min, float max, SettingScope scope, string description)
        {
            if (!(min <= max) || float.IsInfinity(min) || float.IsInfinity(max)) throw new ArgumentException("Expected a finite range.", nameof(min));
            return new InscribeSetting<float>(section, key, scope, description, defaultValue, null, (min, max),
                v => float.IsNaN(v) || float.IsInfinity(v) ? defaultValue : Math.Min(max, Math.Max(min, v)));
        }

        public static InscribeSetting<TEnum> Choice<TEnum>(string section, string key, TEnum defaultValue, SettingScope scope, string description) where TEnum : struct, Enum =>
            new InscribeSetting<TEnum>(section, key, scope, description, defaultValue, null, null,
                v => Enum.IsDefined(typeof(TEnum), v) ? v : defaultValue, Enum.GetNames(typeof(TEnum)));

        /// <summary>A text setting; <paramref name="normalize"/> maps any stored text (null included) to the canonical text.</summary>
        public static InscribeSetting<string> Text(string section, string key, string defaultValue, SettingScope scope, string description, Func<string, string> normalize)
        {
            if (normalize == null) throw new ArgumentNullException(nameof(normalize));
            return new InscribeSetting<string>(section, key, scope, description, defaultValue, null, null, normalize);
        }
    }

    public sealed class InscribeSetting<T> : InscribeSetting
    {
        private readonly Func<T, T> normalize;
        private readonly bool hasSafe;
        private readonly (T Min, T Max)? range;

        internal InscribeSetting(string section, string key, SettingScope scope, string description, T defaultValue, object? safe, (T, T)? range,
            Func<T, T> normalize, IReadOnlyList<string>? choices = null)
            : base(section, key, scope, description, choices)
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
