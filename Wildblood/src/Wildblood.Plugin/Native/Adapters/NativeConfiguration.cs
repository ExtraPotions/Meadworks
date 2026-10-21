// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;
using ExtraPotions.Wildblood.Config;
using ExtraPotions.Wildblood.Runtime;
using ExtraPotions.Wildblood.Integration;
using ExtraPotions.Meadworks.Crucible;

namespace ExtraPotions.Wildblood.Adopted.Config
{
    /// <summary>Source-shaped reads of Wildblood's existing C01 settings, without another configuration binder.</summary>
    public sealed class NativeEntry<T>
    {
        readonly Func<T> read;
        readonly Action<EventHandler, bool> subscribe;
        internal NativeEntry(Func<T> read, Action<EventHandler, bool> subscribe) { this.read = read; this.subscribe = subscribe; }
        public T Value => read();
        public ConfigEntry<T>? Raw { get; internal set; }
        public event EventHandler SettingChanged { add => subscribe(value, true); remove => subscribe(value, false); }
    }

    public sealed class NativeConfiguration : IDisposable
    {
        readonly WildbloodConfig settings;
        readonly Dictionary<Tuple<ConfigEntryBase, EventHandler>, EventHandler<SettingChangedEventArgs>> handlers = new Dictionary<Tuple<ConfigEntryBase, EventHandler>, EventHandler<SettingChangedEventArgs>>();
        public NativeConfiguration(WildbloodConfig settings) { this.settings = settings ?? throw new ArgumentNullException(nameof(settings)); }
        static string Key(string value) => new string(value.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());

        public NativeEntry<T> Bind<T>(string section, string key, T defaultValue, string description) => Bind<T>(section, key);
        public NativeEntry<T> Bind<T>(string section, string key, T defaultValue, ConfigDescription description) => Bind<T>(section, key);

        NativeEntry<T> Bind<T>(string section, string key)
        {
            if (key == "Log diagnostics")
                return new NativeEntry<T>(() => (T)(object)MeadworksLogging.Enabled(ExtraPotions.Wildblood.Runtime.Plugin.Guid, LogSeverity.Verbose), (_, __) => { });
            if (key.EndsWith(" star color", StringComparison.Ordinal))
            {
                var name = key.Substring(0, key.Length - " star color".Length);
                if (!Enum.TryParse(name, true, out ExtraPotions.Wildblood.Traits.Mutation mutation))
                    throw new InvalidOperationException("Unknown native palette mutation: " + name);
                return Entry(WildbloodSettings.Display.Palette,
                    () => (T)(object)ExtraPotions.Wildblood.Traits.MutationPalette.Parse(settings.Get(WildbloodSettings.Display.Palette))[mutation]);
            }
            WildbloodSetting spec;
            switch (key)
            {
                case "Stolen item icon size": case "Devoured creature icon size": spec = WildbloodSettings.Display.IconSize; break;
                case "Boss damage board": spec = WildbloodSettings.Bosses.DamageBoard; break;
                case "Boss damage board seconds": spec = WildbloodSettings.Display.DamageBoardSeconds; break;
                case "Show world tier under minimap": spec = WildbloodSettings.Display.ShowKarmaUnderMinimap; break;
                case "Boss hints": spec = WildbloodSettings.Display.BossHints; break;
                default:
                    var matches = WildbloodSettings.All.Where(row => Key(row.Key) == Key(key)).ToArray();
                    if (matches.Length != 1) throw new InvalidOperationException("Unmapped or ambiguous native Wildblood setting: " + section + "/" + key);
                    spec = matches[0]; break;
            }
            return Entry(spec, () => Read<T>(spec));
        }

        T Read<T>(WildbloodSetting spec)
        {
            object value;
            if (typeof(T) == typeof(KeyboardShortcut) && spec is WildbloodSetting<string> shortcut)
                value = settings.Shortcut(shortcut);
            else if (typeof(T) == typeof(float) && spec is WildbloodSetting<int> integer)
                value = (float)settings.Get(integer);
            else if (spec is WildbloodSetting<T> typed)
                value = settings.Get(typed)!;
            else throw new InvalidOperationException("Native Wildblood setting type mismatch: " + spec.Id);
            if (value is bool flag)
            {
                if (spec == WildbloodSettings.Display.BossHints)
                    flag &= settings.Get(WildbloodSettings.Bosses.BossHints);
                if (spec.Section == WildbloodSettings.Display.Section || spec.Section == WildbloodSettings.Recap.Section
                    || spec == WildbloodSettings.Bosses.DamageBoard)
                    flag &= ExtraPotions.Wildblood.Runtime.Plugin.NativeRulesReady && ExtraPotions.Wildblood.Runtime.Plugin.AllowsUi
                        && WildbloodModules.Active(spec.Section == WildbloodSettings.Recap.Section ? WildbloodModule.Recap : WildbloodModule.Display);
                value = flag;
            }
            return (T)value;
        }

        NativeEntry<T> Entry<T>(WildbloodSetting spec, Func<T> read)
        {
            var actual = settings.Entry(spec);
            return new NativeEntry<T>(read, (handler, add) => {
                var id = Tuple.Create(actual, handler);
                if (add && !handlers.ContainsKey(id))
                {
                    EventHandler<SettingChangedEventArgs> wrapper = (sender, args) => { if (ReferenceEquals(args.ChangedSetting, actual)) handler(sender, args); };
                    actual.ConfigFile.SettingChanged += wrapper; handlers.Add(id, wrapper);
                }
                else if (!add && handlers.TryGetValue(id, out var wrapper))
                { actual.ConfigFile.SettingChanged -= wrapper; handlers.Remove(id); }
            }) { Raw = actual as ConfigEntry<T> };
        }

        public void Dispose()
        {
            foreach (var row in handlers) row.Key.Item1.ConfigFile.SettingChanged -= row.Value;
            handlers.Clear();
        }
    }
}
