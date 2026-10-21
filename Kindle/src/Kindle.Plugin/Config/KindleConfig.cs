// Kindle - ExtraPotions Meadworks crafting conveniences.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;
using ExtraPotions.Kindle.Config;
using ExtraPotions.Meadworks.Crucible;
using Jotunn.Extensions;

namespace ExtraPotions.Kindle.Runtime
{
    /// <summary>
    /// Kindle's configuration surface (K02, design section 4). Every setting of <see cref="KindleSettings.All"/> is bound
    /// through <see cref="MeadworksConfig.Bind{T}(ConfigFile,string,string,T,string,ConfigScope,AcceptableValueBase)"/>
    /// with its scope (cheats through <see cref="MeadworksConfig.BindCheat{T}"/>). Values are read at use time:
    /// <list type="bullet">
    /// <item>Local: always the player's own value; server compatibility carriers do not override it.</item>
    /// <item>ServerAuthoritative: <see cref="MeadworksConfig.Effective{T}"/> (the server's value when Kindle runs there).</item>
    /// <item>Cheat: the authority gate's decision; a blocked cheat reads its safe value (<see cref="KindleCheats"/>).</item>
    /// </list>
    /// ServerAuthoritative and cheat entries are kept read-only in configuration managers while blocked
    /// (<see cref="MeadworksBlockedOptions.Track"/>). Main thread only.
    /// </summary>
    public sealed class KindleConfig
    {
        /// <summary>Stored legacy carrier retained for wire/config compatibility; it cannot override local settings.</summary>
        public const string LockedValuesKey = "Locked Player Values";

        private const string Product = Plugin.Guid;
        private const float RefreshSeconds = 0.5f;

        private static readonly HashSet<KindleSetting> Shortcuts = BuildShortcuts();

        private static HashSet<KindleSetting> BuildShortcuts()
        {
            // Initialize the catalog before its nested setting holders. Mono eagerly initializes
            // the containing type when a holder creates its normalization delegate.
            _ = KindleSettings.All;
            return new HashSet<KindleSetting>
            {
                KindleSettings.RecipeList.SearchKey, KindleSettings.RecipeList.FavouriteRecipeKey, KindleSettings.Salvage.SalvageKey,
                KindleSettings.VirtualWorkbench.Key,
            };
        }

        private readonly ConfigFile file;
        private readonly Dictionary<KindleSetting, ConfigEntryBase> entries = new Dictionary<KindleSetting, ConfigEntryBase>();
        private readonly Dictionary<ConfigEntryBase, KindleSetting> settingsByEntry = new Dictionary<ConfigEntryBase, KindleSetting>();
        private readonly ConfigurationManagerAttributes nearbyTag = new ConfigurationManagerAttributes();
        private float nextRefresh;
        public event EventHandler? Changed;
        private void OnSettingChanged(object sender, SettingChangedEventArgs args) => Changed?.Invoke(this, EventArgs.Empty);

        private KindleConfig(ConfigFile file, MeadworksLog log)
        {
            this.file = file;
            var save = file.SaveOnConfigSet;
            file.SaveOnConfigSet = false;
            try
            {
                foreach (var setting in KindleSettings.All) BindSetting(setting);
                // Use the existing manager attribute convention; keep dependency and server-lock tags separate.
                var nearby = entries[KindleSettings.Tracker.CountNearbyChests].Description;
                typeof(ConfigDescription).GetField("<Tags>k__BackingField", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    ?.SetValue(nearby, nearby.Tags.Concat(new object[] { nearbyTag }).ToArray());
                // Keep the old wire/config key without publishing new overrides or changing its stored bytes.
                file.BindConfig(KindleSettings.Server.Section, LockedValuesKey, "",
                    "Retained compatibility carrier. Client-local settings always use their own values.", synced: true,
                    configAttributes: new ConfigurationManagerAttributes { Browsable = false, ReadOnly = true });
                MeadworksConfig.EnsureSchema(file, KindleDefaultsMigration.Schema,
                    new[] { new MigrationStep(0, 1, () => MigrateDefaults(log)) });
            }
            finally
            {
                file.SaveOnConfigSet = save;
            }
            file.Save();
            file.SettingChanged += OnSettingChanged;
        }

        /// <summary>Binds every Kindle setting on the plugin's config file and starts tracking blocked options.</summary>
        public static KindleConfig Bind(ConfigFile file, MeadworksLog log)
        {
            if (file == null) throw new ArgumentNullException(nameof(file));
            if (log == null) throw new ArgumentNullException(nameof(log));
            var config = new KindleConfig(file, log);
            foreach (var pair in config.entries.Where(p => p.Key.Scope != SettingScope.Local))
                MeadworksBlockedOptions.Track(Product, pair.Value, Map(pair.Key.Scope));
            config.Refresh();
            log.Verbose("Configuration bound: " + Count(SettingScope.Local) + " Local, " + Count(SettingScope.ServerAuthoritative)
                + " ServerAuthoritative, " + Count(SettingScope.Cheat) + " Cheat.");
            return config;
        }

        public static ConfigScope Map(SettingScope scope)
        {
            switch (scope)
            {
                case SettingScope.Local: return ConfigScope.Local;
                case SettingScope.ServerAuthoritative: return ConfigScope.ServerAuthoritative;
                case SettingScope.Cheat: return ConfigScope.Cheat;
                default: throw new ArgumentException("Unknown scope.", nameof(scope));
            }
        }

        public ConfigEntryBase Entry(KindleSetting setting) =>
            entries.TryGetValue(setting ?? throw new ArgumentNullException(nameof(setting)), out var entry) ? entry : throw new ArgumentException("Not a Kindle setting.", nameof(setting));

        /// <summary>The value Kindle acts on now (see the class summary). Shortcut settings use <see cref="Shortcut"/>.</summary>
        public T Get<T>(KindleSetting<T> setting)
        {
            if (Shortcuts.Contains(setting)) throw new ArgumentException(setting.Id + " is a keyboard shortcut; use Shortcut.", nameof(setting));
            var entry = (ConfigEntry<T>)Entry(setting);
            switch (setting.Scope)
            {
                case SettingScope.Cheat:
                    return KindleCheats.Effective(setting, CheatLocked(), entry.Value);
                case SettingScope.ServerAuthoritative:
                    return setting.Normalize(MeadworksConfig.Effective(entry, Product, ConfigScope.ServerAuthoritative, setting.Default));
                default:
                    return setting.Normalize(entry.Value);
            }
        }

        /// <summary>The player's own keyboard shortcut; server settings never replace local input.</summary>
        public KeyboardShortcut Shortcut(KindleSetting<string> setting)
        {
            if (!Shortcuts.Contains(setting)) throw new ArgumentException(setting.Id + " is not a keyboard shortcut.", nameof(setting));
            var entry = (ConfigEntry<KeyboardShortcut>)Entry(setting);
            return entry.Value;
        }

        /// <summary>Gamepad Sort Button as a Crucible pad button; null when empty (off). An unknown name reads the default.</summary>
        public PadButton? GamepadSortButton
        {
            get
            {
                var setting = KindleSettings.RecipeList.GamepadSortButton;
                var text = Get(setting);
                if (text.Length == 0) return null;
                if (GamepadMap.TryParse(text, out var button)) return button;
                return GamepadMap.TryParse(setting.Default, out button) ? button : (PadButton?)null;
            }
        }

        /// <summary>Craft Speed in effect: 1.0 whenever the gate blocks cheats (vanilla server, server without Kindle, pending).</summary>
        public float CraftSpeed => Get(KindleSettings.Batch.CraftSpeed);

        /// <summary>Instant Crafting in effect: off whenever the gate blocks cheats.</summary>
        public bool InstantCrafting => Get(KindleSettings.Batch.InstantCrafting);

        /// <summary>Cart Workbench in effect: off whenever the gate blocks cheats.</summary>
        public bool CartWorkbench => Get(KindleSettings.Cart.CartWorkbench);

        /// <summary>Compatibility accessor. Local settings cannot be locked by a server.</summary>
        public LockList Locks => LockList.Empty;

        /// <summary>Compatibility accessor; no server can force a Local setting.</summary>
        public bool IsLocked(KindleSetting setting) => false;

        /// <summary>
        /// Whether Kindle may act on a cheat now; tells the player why not (once per option per session, through the
        /// Crucible notifier). <paramref name="label"/> is the localized option name.
        /// </summary>
        public bool TryUseCheat(KindleSetting cheat, string? label = null)
        {
            if (cheat == null || cheat.Scope != SettingScope.Cheat) throw new ArgumentException("Expected a Kindle cheat.", nameof(cheat));
            return MeadworksBlockedOptions.TryUse(Product, cheat.Id, ConfigScope.Cheat, label);
        }

        /// <summary>
        /// Tells the player that a setting is blocked when they try to change it (once per option per session): a
        /// ServerAuthoritative or cheat setting the gate blocks. True when the
        /// change may go ahead.
        /// </summary>
        public bool TryChange(KindleSetting setting, string? label = null)
        {
            if (setting == null) throw new ArgumentNullException(nameof(setting));
            if (setting == KindleSettings.Tracker.CountNearbyChests && !KindleTracker.NearbyAvailable)
            {
                if (MessageHud.instance != null && MeadworksHeadless.AllowsUi)
                    MessageHud.instance.ShowMessage(MessageHud.MessageType.Center, KindleText.Text("needs_burrow"));
                return false;
            }
            if (setting.Scope != SettingScope.Local) return MeadworksBlockedOptions.TryEdit(Product, setting.Id, Map(setting.Scope), label);
            return true;
        }

        /// <summary>Called from the plugin's Update to refresh optional integration availability.</summary>
        public void Tick(float now)
        {
            if (now < nextRefresh) return;
            nextRefresh = now + RefreshSeconds;
            Refresh();
        }

        public void Shutdown()
        {
            file.SettingChanged -= OnSettingChanged;
            Changed = null;
            foreach (var entry in entries.Values.Where(e => settingsByEntry[e].Scope != SettingScope.Local)) MeadworksBlockedOptions.Untrack(entry);
        }

        private void Refresh()
        {
            nearbyTag.ReadOnly = KindleTracker.NearbyAvailable ? (bool?)null : true;
        }

        /// <summary>Moves stored values that still equal an old input default to the new default (once, by schema).</summary>
        private void MigrateDefaults(MeadworksLog log)
        {
            var moved = new List<string>();
            foreach (var change in KindleDefaultsMigration.Changes)
            {
                var entry = entries[change.Key];
                var stored = entry is ConfigEntry<KeyboardShortcut> shortcut ? shortcut.Value.Serialize() : entry.BoxedValue as string;
                var next = KindleDefaultsMigration.Migrate(change.Key, stored);
                if (next == null) continue;
                entry.BoxedValue = entry is ConfigEntry<KeyboardShortcut> ? (object)KeyboardShortcut.Deserialize(next) : next;
                moved.Add(change.Key.Id);
            }
            if (moved.Count > 0)
                log.Info("Moved unchanged old input defaults to the new defaults: " + string.Join(", ", moved) + ". Customized values were kept.");
        }

        private static bool CheatLocked() => MeadworksAuthority.Decide(Product, ConfigScope.Cheat).Source == ConfigValueSource.Locked;

        private void BindSetting(KindleSetting setting)
        {
            ConfigEntryBase entry;
            if (ReferenceEquals(setting, KindleSettings.Server.LockedPlayerSettings))
                entry = MeadworksConfig.BindHidden(file, setting.Section, setting.Key, (string)setting.DefaultValue,
                    setting.Description, ConfigScope.ServerAuthoritative);
            else if (Shortcuts.Contains(setting))
                entry = MeadworksConfig.Bind(file, setting.Section, setting.Key, KeyboardShortcut.Deserialize((string)setting.DefaultValue), Describe(setting, null), Map(setting.Scope));
            else
            {
                switch (setting)
                {
                    case KindleSetting<bool> b: entry = Bind(b, null); break;
                    case KindleSetting<int> i: entry = Bind(i, new AcceptableValueRange<int>((int)i.Minimum!, (int)i.Maximum!)); break;
                    case KindleSetting<float> f: entry = Bind(f, new AcceptableValueRange<float>((float)f.Minimum!, (float)f.Maximum!)); break;
                    case KindleSetting<string> s: entry = Bind(s, null); break;
                    case KindleSetting<Recipes.RecipeView> v: entry = Bind(v, null); break;
                    case KindleSetting<TrackerFont> t: entry = Bind(t, null); break;
                    case KindleSetting<SalvageRounding> r: entry = Bind(r, null); break;
                    default: throw new NotSupportedException(setting.Id + ": unsupported value type " + setting.ValueType.Name + ".");
                }
            }
            entries.Add(setting, entry);
            settingsByEntry.Add(entry, setting);
        }

        private ConfigDescription Describe(KindleSetting setting, AcceptableValueBase? acceptable, string? text = null)
        {
            ConfigDependency? dependency = null;
            if (ReferenceEquals(setting, KindleSettings.Tracker.MaxTracked) ||
                ReferenceEquals(setting, KindleSettings.Tracker.CountNearbyChests) ||
                ReferenceEquals(setting, KindleSettings.Tracker.HideInCombat) ||
                ReferenceEquals(setting, KindleSettings.Tracker.HideWithMap) ||
                ReferenceEquals(setting, KindleSettings.Tracker.Font) ||
                ReferenceEquals(setting, KindleSettings.Tracker.FontSize) ||
                ReferenceEquals(setting, KindleSettings.Tracker.HaveColour) ||
                ReferenceEquals(setting, KindleSettings.Tracker.MissingColour) ||
                ReferenceEquals(setting, KindleSettings.Tracker.ReadyColour) ||
                ReferenceEquals(setting, KindleSettings.Tracker.BackgroundOpacity))
                dependency = new ConfigDependency(() => Get(KindleSettings.Tracker.Enabled), () => "Requires " + KindleSettings.Tracker.Enabled.Section + ": " + KindleSettings.Tracker.Enabled.Key + ".");
            if (ReferenceEquals(setting, KindleSettings.RecipeList.FavouritesFirst) ||
                ReferenceEquals(setting, KindleSettings.RecipeList.FavouriteRecipeKey))
                dependency = new ConfigDependency(() => Get(KindleSettings.RecipeList.Favourites), () => "Requires " + KindleSettings.RecipeList.Favourites.Section + ": " + KindleSettings.RecipeList.Favourites.Key + ".");
            if (ReferenceEquals(setting, KindleSettings.RecipeList.SearchKey))
                dependency = new ConfigDependency(() => Get(KindleSettings.RecipeList.Search), () => "Requires " + KindleSettings.RecipeList.Search.Section + ": " + KindleSettings.RecipeList.Search.Key + ".");
            if (ReferenceEquals(setting, KindleSettings.RecipeList.GamepadSortButton))
                dependency = new ConfigDependency(() => Get(KindleSettings.RecipeList.GamepadControls), () => "Requires " + KindleSettings.RecipeList.GamepadControls.Section + ": " + KindleSettings.RecipeList.GamepadControls.Key + ".");
            if (ReferenceEquals(setting, KindleSettings.Batch.MaxAmount))
                dependency = new ConfigDependency(() => Get(KindleSettings.Batch.Enabled), () => "Requires " + KindleSettings.Batch.Enabled.Section + ": " + KindleSettings.Batch.Enabled.Key + ".");
            if (ReferenceEquals(setting, KindleSettings.VirtualWorkbench.Key))
                dependency = new ConfigDependency(() => Get(KindleSettings.VirtualWorkbench.Enabled), () => "Requires " + KindleSettings.VirtualWorkbench.Enabled.Section + ": " + KindleSettings.VirtualWorkbench.Enabled.Key + ".");
            if (ReferenceEquals(setting, KindleSettings.Salvage.SalvageKey))
                dependency = new ConfigDependency(() => Get(KindleSettings.Salvage.Enabled), () => "Requires " + KindleSettings.Salvage.Enabled.Section + ": " + KindleSettings.Salvage.Enabled.Key + ".");
            return new ConfigDescription(text ?? setting.Description, acceptable, dependency == null ? Array.Empty<object>() : new object[] { dependency });
        }

        private ConfigEntry<T> Bind<T>(KindleSetting<T> setting, AcceptableValueBase? acceptable) =>
            setting.Scope == SettingScope.Cheat
                ? MeadworksConfig.BindCheat(file, setting.Section, setting.Key, setting.Safe, setting.Description, acceptable)
                : MeadworksConfig.Bind(file, setting.Section, setting.Key, setting.Default, Describe(setting, acceptable), Map(setting.Scope));

        private static int Count(SettingScope scope) => KindleSettings.All.Count(s => s.Scope == scope);
    }
}
