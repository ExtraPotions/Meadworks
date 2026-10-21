// Wildblood - ExtraPotions Meadworks.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original; same binding model as Ember.Plugin and Kindle.Plugin (expDARE), without Locked Player Settings.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using BepInEx.Configuration;
using ExtraPotions.Meadworks.Crucible;
using ExtraPotions.Wildblood.Config;
using ExtraPotions.Wildblood.Loot;

namespace ExtraPotions.Wildblood.Runtime
{
    /// <summary>
    /// Wildblood's configuration surface (B02a, design section 4). Every setting of <see cref="WildbloodSettings.All"/> is
    /// bound through <see cref="MeadworksConfig.Bind{T}(ConfigFile,string,string,T,string,ConfigScope,AcceptableValueBase)"/>
    /// with its scope (cheats through <see cref="MeadworksConfig.BindCheat{T}(ConfigFile,string,string,T,string,AcceptableValueBase)"/>).
    /// World settings retain their ServerAuthoritative C01 fallback and register that same entry with CS4.
    /// The world's readable value wins through the existing typed WorldSettings resolver. Main thread only.
    /// </summary>
    public sealed class WildbloodConfig
    {
        private const string Product = Plugin.Guid;

        private static readonly HashSet<WildbloodSetting> Shortcuts = BuildShortcuts();

        private static HashSet<WildbloodSetting> BuildShortcuts()
        {
            // Initialize the catalog before its nested holders (Mono initializes the containing type eagerly).
            _ = WildbloodSettings.All;
            return new HashSet<WildbloodSetting> { WildbloodSettings.Display.LoreWindowKey, WildbloodSettings.Recap.RecapKey };
        }

        private readonly ConfigFile file;
        private readonly Dictionary<WildbloodSetting, ConfigEntryBase> entries = new Dictionary<WildbloodSetting, ConfigEntryBase>();
        private readonly Dictionary<WildbloodSetting, WorldSettingBinding> worldBindings = new Dictionary<WildbloodSetting, WorldSettingBinding>();

        private WildbloodConfig(ConfigFile file)
        {
            this.file = file;
            var save = file.SaveOnConfigSet;
            file.SaveOnConfigSet = false;
            try
            {
                foreach (var setting in WildbloodSettings.All) BindSetting(setting);
            }
            catch
            {
                ReleaseWorldBindings();
                throw;
            }
            finally
            {
                file.SaveOnConfigSet = save;
            }
            try { file.Save(); }
            catch { ReleaseWorldBindings(); throw; }
        }

        /// <summary>Binds every Wildblood setting on the plugin's config file and tracks blocked options (C03).</summary>
        public static WildbloodConfig Bind(ConfigFile file, MeadworksLog log)
        {
            if (file == null) throw new ArgumentNullException(nameof(file));
            if (log == null) throw new ArgumentNullException(nameof(log));
            var config = new WildbloodConfig(file);
            foreach (var pair in config.entries.Where(p => p.Key.Scope != SettingScope.Local))
                MeadworksBlockedOptions.Track(Product, pair.Value, Map(pair.Key.Scope));
            log.Verbose("Configuration bound: " + Count(SettingScope.Local) + " Local, " + Count(SettingScope.ServerAuthoritative)
                + " ServerAuthoritative, " + Count(SettingScope.World) + " World, " + Count(SettingScope.Cheat) + " Cheat.");
            return config;
        }

        public static ConfigScope Map(SettingScope scope)
        {
            switch (scope)
            {
                case SettingScope.Local: return ConfigScope.Local;
                case SettingScope.ServerAuthoritative: return ConfigScope.ServerAuthoritative;
                case SettingScope.World: return ConfigScope.ServerAuthoritative;
                case SettingScope.Cheat: return ConfigScope.Cheat;
                default: throw new ArgumentException("Unknown scope.", nameof(scope));
            }
        }

        public ConfigEntryBase Entry(WildbloodSetting setting) =>
            entries.TryGetValue(setting ?? throw new ArgumentNullException(nameof(setting)), out var entry) ? entry : throw new ArgumentException("Not a Wildblood setting.", nameof(setting));

        /// <summary>The value Wildblood acts on now. Shortcut settings use <see cref="Shortcut"/>.</summary>
        public T Get<T>(WildbloodSetting<T> setting)
        {
            if (Shortcuts.Contains(setting)) throw new ArgumentException(setting.Id + " is a keyboard shortcut; use Shortcut.", nameof(setting));
            var entry = (ConfigEntry<T>)Entry(setting);
            switch (setting.Scope)
            {
                case SettingScope.Cheat:
                    return WildbloodCheats.Effective(setting, CheatLocked(), entry.Value);
                case SettingScope.ServerAuthoritative:
                    return setting.Normalize(MeadworksConfig.Effective(entry, Product, ConfigScope.ServerAuthoritative, setting.Default));
                case SettingScope.World:
                    var configValue = setting.Normalize(MeadworksConfig.Effective(entry, Product, ConfigScope.ServerAuthoritative, setting.Default));
                    return ResolveWorld(setting, worldBindings[setting].WorldText, configValue);
                default:
                    return setting.Normalize(entry.Value);
            }
        }

        public KeyboardShortcut Shortcut(WildbloodSetting<string> setting)
        {
            if (!Shortcuts.Contains(setting)) throw new ArgumentException(setting.Id + " is not a keyboard shortcut.", nameof(setting));
            return ((ConfigEntry<KeyboardShortcut>)Entry(setting)).Value;
        }

        // Global-key changes do not emit Config.SettingChanged; the rule owner polls this exact received state.
        internal string WorldValuesStamp
        {
            get
            {
                var stamp = new StringBuilder();
                foreach (var pair in worldBindings.OrderBy(pair => pair.Value.Key, StringComparer.Ordinal))
                {
                    string key = pair.Value.Key;
                    string? text = pair.Value.WorldText;
                    stamp.Append(key.Length).Append(':').Append(key).Append('=');
                    stamp.Append(text == null ? -1 : text.Length).Append(':');
                    if (text != null) stamp.Append(text);
                    stamp.Append(';');
                }
                return stamp.ToString();
            }
        }

        internal WorldSettingBinding WorldBinding(WildbloodSetting setting) =>
            worldBindings.TryGetValue(setting, out var binding) ? binding : throw new ArgumentException("Not a bound World setting.", nameof(setting));

        /// <summary>The drop multipliers in effect (none unless the Drop Multipliers cheat is allowed and on).</summary>
        public DropMultipliers Multipliers => WildbloodCheats.Multipliers(CheatLocked(), Get(WildbloodSettings.Cheats.DropMultipliers),
            Get(WildbloodSettings.Cheats.CreatureDropMultiplier), Get(WildbloodSettings.Cheats.BossDropMultiplier), Get(WildbloodSettings.Cheats.TrophyDropMultiplier));

        /// <summary>Whether Wildblood may act on a cheat now; tells the player why not (once per option per session).</summary>
        public bool TryUseCheat(WildbloodSetting cheat, string? label = null)
        {
            if (cheat == null || cheat.Scope != SettingScope.Cheat) throw new ArgumentException("Expected a Wildblood cheat.", nameof(cheat));
            return MeadworksBlockedOptions.TryUse(Product, cheat.Id, ConfigScope.Cheat, label);
        }

        public void Shutdown()
        {
            ReleaseWorldBindings();
            foreach (var entry in entries.Values) MeadworksBlockedOptions.Untrack(entry);
            entries.Clear();
        }

        private void ReleaseWorldBindings()
        {
            foreach (var binding in worldBindings.Values) binding.Dispose();
            worldBindings.Clear();
        }

        private static bool CheatLocked() => MeadworksAuthority.Decide(Product, ConfigScope.Cheat).Source == ConfigValueSource.Locked;

        private void BindSetting(WildbloodSetting setting)
        {
            ConfigEntryBase entry;
            if (Shortcuts.Contains(setting))
            {
                var text = (string)setting.DefaultValue;
                entry = MeadworksConfig.Bind(file, setting.Section, setting.Key, text == "" ? KeyboardShortcut.Empty : KeyboardShortcut.Deserialize(text), Describe(setting, null), Map(setting.Scope));
            }
            else
            {
                switch (setting)
                {
                    case WildbloodSetting<bool> b: entry = Bind(b, null); break;
                    case WildbloodSetting<int> i: entry = Bind(i, new AcceptableValueRange<int>((int)i.Minimum!, (int)i.Maximum!)); break;
                    case WildbloodSetting<float> f: entry = Bind(f, new AcceptableValueRange<float>((float)f.Minimum!, (float)f.Maximum!)); break;
                    case WildbloodSetting<string> s: entry = Upgrade(s, Bind(s, null)); break;
                    case WildbloodSetting<Stars.Difficulty> d: entry = Bind(d, null); break;
                    case WildbloodSetting<Stars.StarMode> m: entry = Bind(m, null); break;
                    case WildbloodSetting<Traits.ModifierCap> c: entry = Bind(c, null); break;
                    case WildbloodSetting<LootMode> l: entry = Bind(l, null); break;
                    case WildbloodSetting<KarmaMode> k: entry = Bind(k, null); break;
                    case WildbloodSetting<IconLayout> layout: entry = Bind(layout, null); break;
                    default: throw new NotSupportedException(setting.Id + ": unsupported value type " + setting.ValueType.Name + ".");
                }
            }
            entries.Add(setting, entry);
        }

        // A saved value that is exactly an earlier default moves to the current one and is written back; a customized
        // Boss Keys list without the Deep North key stays and is named once in the log.
        private static ConfigEntry<string> Upgrade(WildbloodSetting<string> setting, ConfigEntry<string> entry)
        {
            string? upgraded = DefaultUpgrades.Upgrade(setting, entry.Value);
            if (upgraded != null)
            {
                entry.Value = upgraded;
                Plugin.NativeLogger?.LogInfo(setting.Section + " / " + setting.Key + ": saved earlier default updated to \"" + upgraded + "\".");
            }
            else if (DefaultUpgrades.MissesDeepNorth(setting, entry.Value))
                Plugin.NativeLogger?.LogInfo(setting.Section + " / " + setting.Key + ": customized list kept; add " + DefaultUpgrades.DeepNorthKey
                    + " to count Kall Fimbulbringer toward the world tier.");
            return entry;
        }

        private ConfigDescription Describe(WildbloodSetting setting, AcceptableValueBase? acceptable, string? text = null)
        {
            ConfigDependency? dependency = null;
            if (ReferenceEquals(setting, WildbloodSettings.AI.Interval) ||
                ReferenceEquals(setting, WildbloodSettings.AI.Chance) ||
                ReferenceEquals(setting, WildbloodSettings.AI.Hold))
                dependency = new ConfigDependency(() => Get(WildbloodSettings.AI.Enabled), () => "Requires " + WildbloodSettings.AI.Enabled.Section + ": " + WildbloodSettings.AI.Enabled.Key + ".");
            if (ReferenceEquals(setting, WildbloodSettings.Horde.SpawnMultiplier) ||
                ReferenceEquals(setting, WildbloodSettings.Horde.HealthMultiplier) ||
                ReferenceEquals(setting, WildbloodSettings.Horde.AttackMultiplier))
                dependency = new ConfigDependency(() => Get(WildbloodSettings.Horde.Enabled), () => "Requires " + WildbloodSettings.Horde.Enabled.Section + ": " + WildbloodSettings.Horde.Enabled.Key + ".");
            if (ReferenceEquals(setting, WildbloodSettings.Display.TraitNamesAboveName))
                dependency = new ConfigDependency(() => Get(WildbloodSettings.Display.ShowTraitNames), () => "Requires " + WildbloodSettings.Display.ShowTraitNames.Section + ": " + WildbloodSettings.Display.ShowTraitNames.Key + ".");
            return new ConfigDescription(text ?? setting.Description, acceptable, dependency == null ? Array.Empty<object>() : new object[] { dependency });
        }

        private ConfigEntry<T> Bind<T>(WildbloodSetting<T> setting, AcceptableValueBase? acceptable)
        {
            var entry = setting.Scope == SettingScope.Cheat
                ? MeadworksConfig.BindCheat(file, setting.Section, setting.Key, setting.Safe, setting.Description, acceptable)
                : MeadworksConfig.Bind(file, setting.Section, setting.Key, setting.Default, Describe(setting, acceptable), Map(setting.Scope));
            if (setting.Scope == SettingScope.World)
                worldBindings.Add(setting, MeadworksWorldSettings.Register(Product, WorldSettings.Key(setting), entry, text => CanonicalWorldText(setting, text)));
            return entry;
        }

        private static string? CanonicalWorldText<T>(WildbloodSetting<T> setting, string text)
        {
            if (!WorldSettings.TryParse(text, out T parsed)) return null;
            if (parsed is float number && (float.IsNaN(number) || float.IsInfinity(number))) return null;
            var normalized = setting.Normalize(parsed);
            return normalized is IFormattable formatted
                ? formatted.ToString(null, CultureInfo.InvariantCulture)
                : Convert.ToString(normalized, CultureInfo.InvariantCulture);
        }

        // Get<T> also supports text, so dispatch the existing struct-constrained resolver by actual setting type.
        // No world/config/default precedence or value parser is duplicated here.
        private static T ResolveWorld<T>(WildbloodSetting<T> setting, string? worldText, T configValue)
        {
            object value;
            switch (setting)
            {
                case WildbloodSetting<bool> spec: value = WorldSettings.Resolve(spec, worldText, (bool)(object)configValue!).Value; break;
                case WildbloodSetting<int> spec: value = WorldSettings.Resolve(spec, worldText, (int)(object)configValue!).Value; break;
                case WildbloodSetting<float> spec: value = WorldSettings.Resolve(spec, worldText, (float)(object)configValue!).Value; break;
                case WildbloodSetting<string> spec: value = WorldSettings.ResolveText(spec, DefaultUpgrades.Upgrade(spec, worldText) ?? worldText, (string)(object)configValue!).Value; break;
                case WildbloodSetting<Stars.Difficulty> spec: value = WorldSettings.Resolve(spec, worldText, (Stars.Difficulty)(object)configValue!).Value; break;
                case WildbloodSetting<Stars.StarMode> spec: value = WorldSettings.Resolve(spec, worldText, (Stars.StarMode)(object)configValue!).Value; break;
                case WildbloodSetting<Traits.ModifierCap> spec: value = WorldSettings.Resolve(spec, worldText, (Traits.ModifierCap)(object)configValue!).Value; break;
                case WildbloodSetting<LootMode> spec: value = WorldSettings.Resolve(spec, worldText, (LootMode)(object)configValue!).Value; break;
                case WildbloodSetting<KarmaMode> spec: value = WorldSettings.Resolve(spec, worldText, (KarmaMode)(object)configValue!).Value; break;
                case WildbloodSetting<IconLayout> spec: value = WorldSettings.Resolve(spec, worldText, (IconLayout)(object)configValue!).Value; break;
                default: throw new NotSupportedException(setting.Id + ": unsupported World value type " + setting.ValueType.Name + ".");
            }
            return (T)value;
        }

        private static int Count(SettingScope scope) => WildbloodSettings.All.Count(s => s.Scope == scope);
    }
}
