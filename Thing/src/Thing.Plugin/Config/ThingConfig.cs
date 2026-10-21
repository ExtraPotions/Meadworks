// Thing - ExtraPotions Meadworks.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original; same binding model as Tempest.Plugin and Ember.Plugin (expDARE), without cheats.
using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;
using ExtraPotions.Meadworks.Crucible;
using ExtraPotions.Thing.Admin;
using ExtraPotions.Thing.Config;
using Jotunn.Managers;
using CruciblePlugin = ExtraPotions.Meadworks.Crucible.Plugin;

namespace ExtraPotions.Thing.Runtime
{
    /// <summary>
    /// Thing's configuration surface (TG03a, design section 4). Every setting of <see cref="ThingSettings.All"/> is bound through
    /// Crucible C01 with its scope; keyboard shortcuts bind as BepInEx <see cref="KeyboardShortcut"/>. ServerAuthoritative
    /// entries are tracked as blocked options so configuration managers show them read-only where the server decides. Values
    /// are read at use time, so edits, reloads and server pushes apply at once. The server's Locked Player Settings reach
    /// clients through a hidden ServerAuthoritative carrier with the server's own values (same lock model as Ember.Plugin);
    /// a locked Local setting or shortcut reads the server's value on a remote client. Main thread only.
    /// </summary>
    public sealed class ThingConfig
    {
        /// <summary>Hidden ServerAuthoritative carrier of the server's values for the locked Local settings.</summary>
        public const string LockedValuesKey = "Locked Player Values";
        public const string AdminSettingsGrantKey = "Allow Admin Settings Editing";
        public const string AdminSettingsStatusKey = "Admin Settings Editing Status";

        private const string Product = Plugin.Guid;
        private const float RefreshSeconds = 0.5f;

        private readonly ConfigFile file;
        private readonly MeadworksLog log;
        private readonly Dictionary<ThingSetting, ConfigEntryBase> entries = new Dictionary<ThingSetting, ConfigEntryBase>();
        private readonly HashSet<ThingSetting> trackedLocks = new HashSet<ThingSetting>();
        private readonly ThingSyncReceipt syncReceipt = new ThingSyncReceipt();
        private readonly ConfigEntry<string> lockedValues;
        private readonly ConfigEntry<bool> settingsGrant, settingsStatus;
        private string? lockListText;
        private LockList lockList = LockList.Empty;
        private string? valuesText;
        private IReadOnlyDictionary<string, string> values = new Dictionary<string, string>();
        private float nextRefresh;

        private ThingConfig(ConfigFile file, MeadworksLog log)
        {
            this.file = file;
            this.log = log;
            var save = file.SaveOnConfigSet;
            file.SaveOnConfigSet = false;
            try
            {
                foreach (var setting in ThingSettings.All) BindSetting(setting);
                settingsGrant = MeadworksConfig.BindHidden(file, ThingSettings.General.Section, AdminSettingsGrantKey, false,
                    "Host-only: allow listed server admins to edit settings. This local server policy is never synchronized from clients.", ConfigScope.Local);
                settingsStatus = MeadworksConfig.BindHidden(file, ThingSettings.General.Section, AdminSettingsStatusKey, false,
                    "Managed by Thing: display-only projection of the host's settings delegation.", ConfigScope.ServerAuthoritative);
                lockedValues = MeadworksConfig.BindHidden(file, ThingSettings.General.Section, LockedValuesKey, "",
                    "Managed by Thing: the server's values for Locked Player Settings. Do not edit.", ConfigScope.ServerAuthoritative);
            }
            finally
            {
                file.SaveOnConfigSet = save;
            }
            file.Save();
            SynchronizationManager.OnConfigurationSynchronized += OnSynchronized;
        }

        public ConfigFile File => file;
        public bool AllowAdminSettingsEditing => CruciblePlugin.IsReady && CruciblePlugin.Session.Role == SessionRole.RemoteClient
            ? ServerBinds() && settingsStatus.Value : settingsGrant.Value;
        public bool IsSettingsPolicy(ConfigEntryBase entry) => ReferenceEquals(entry, settingsGrant) || ReferenceEquals(entry, settingsStatus);
        public bool SetAdminSettingsEditing(bool enabled, long expectedGeneration)
        {
            if (!CruciblePlugin.IsReady || !CruciblePlugin.Session.AuthorityReady || !CruciblePlugin.Session.IsAuthority
                || CruciblePlugin.Session.Generation != expectedGeneration || ZNet.instance == null || !ZNet.instance.IsServer()) return false;
            settingsGrant.Value = enabled; file.Save(); PublishSettingsAccess(); return true;
        }
        private void PublishSettingsAccess()
        {
            if (!CruciblePlugin.IsReady || CruciblePlugin.Session.Role == SessionRole.RemoteClient || settingsStatus.Value == settingsGrant.Value) return;
            MeadworksConfig.Amend(settingsStatus, Product, ConfigScope.ServerAuthoritative, settingsGrant.Value);
        }

        /// <summary>Raised after any Thing entry changed (local edit, reload or server push).</summary>
        public event Action? Changed;

        public static ThingConfig Bind(ConfigFile file, MeadworksLog log)
        {
            if (file == null) throw new ArgumentNullException(nameof(file));
            if (log == null) throw new ArgumentNullException(nameof(log));
            var config = new ThingConfig(file, log);
            foreach (var pair in config.entries.Where(p => p.Key.Scope != SettingScope.Local))
                MeadworksBlockedOptions.Track(Product, pair.Value, Map(pair.Key.Scope));
            file.SettingChanged += config.OnSettingChanged;
            file.ConfigReloaded += config.OnReloaded;
            config.Refresh(force: true);
            log.Verbose("Configuration bound: " + ThingSettings.All.Count(s => s.Scope == SettingScope.Local) + " Local, "
                + ThingSettings.All.Count(s => s.Scope == SettingScope.ServerAuthoritative) + " ServerAuthoritative, no cheats.");
            return config;
        }

        public static ConfigScope Map(SettingScope scope) => scope == SettingScope.Local ? ConfigScope.Local : ConfigScope.ServerAuthoritative;

        public ConfigEntryBase Entry(ThingSetting setting) =>
            entries.TryGetValue(setting ?? throw new ArgumentNullException(nameof(setting)), out var entry) ? entry : throw new ArgumentException("Not a Thing setting.", nameof(setting));

        /// <summary>The value Thing acts on now. Keyboard shortcuts use <see cref="Shortcut"/>.</summary>
        public T Get<T>(ThingSetting<T> setting)
        {
            if (setting.Hotkey) throw new ArgumentException(setting.Id + " is a keyboard shortcut; use Shortcut.", nameof(setting));
            var entry = (ConfigEntry<T>)Entry(setting);
            if (setting.Scope == SettingScope.ServerAuthoritative)
                return setting.Normalize(MeadworksConfig.Effective(entry, Product, ConfigScope.ServerAuthoritative, setting.Default));
            var held = Lock(setting);
            if (held.Locked && TryConvert(held.ServerText!, out T server)) return setting.Normalize(server);
            return setting.Normalize(entry.Value);
        }

        /// <summary>A keyboard shortcut's own entry (the converter writes it). Input reads <see cref="ShortcutValue"/>.</summary>
        public ConfigEntry<KeyboardShortcut> Shortcut(ThingSetting<string> setting)
        {
            if (!setting.Hotkey) throw new ArgumentException(setting.Id + " is not a keyboard shortcut.", nameof(setting));
            return (ConfigEntry<KeyboardShortcut>)Entry(setting);
        }

        /// <summary>The shortcut in effect for <c>MeadworksInput.Pressed</c>: the server's when it locks this setting, else the player's own.</summary>
        public KeyboardShortcut ShortcutValue(ThingSetting<string> setting)
        {
            var entry = Shortcut(setting);
            var held = Lock(setting);
            if (held.Locked)
            {
                try { return KeyboardShortcut.Deserialize(held.ServerText!); }
                catch (Exception) { }
            }
            return entry.Value;
        }

        /// <summary>The server's lock list as parsed; empty unless a Thing server binds this client.</summary>
        public LockList Locks => lockList;

        /// <summary>Whether the server forces this Local setting to its own value right now.</summary>
        public bool IsLocked(ThingSetting setting) => setting != null && setting.Lockable && Lock(setting).Locked;

        /// <summary>From the plugin's Update: keeps the lock state, read-only tags and the server's locked values current.</summary>
        public void Tick(float now)
        {
            if (now < nextRefresh) return;
            nextRefresh = now + RefreshSeconds;
            Refresh(force: false);
        }

        private void Refresh(bool force)
        {
            try
            {
                PublishSettingsAccess();
                PublishLockedValues();
                ReadLocks(force);
                foreach (var setting in ThingSettings.All.Where(s => s.Lockable))
                {
                    var locked = IsLocked(setting);
                    if (locked && trackedLocks.Add(setting)) MeadworksBlockedOptions.Track(Product, entries[setting], ConfigScope.ServerAuthoritative);
                    else if (!locked && trackedLocks.Remove(setting)) MeadworksBlockedOptions.Untrack(entries[setting]);
                }
            }
            catch (Exception error)
            {
                log.Warning("Configuration refresh failed: " + error.GetType().Name + ": " + error.Message);
            }
        }

        /// <summary>
        /// On the machine that runs the world (host, dedicated server; also single player and the menu, so the text is current
        /// when hosting starts) the hidden carrier holds this machine's own values of the locked settings; Jotunn sends it to
        /// clients with the rest of the synchronized configuration.
        /// </summary>
        private void PublishLockedValues()
        {
            if (!CruciblePlugin.IsReady || CruciblePlugin.Session.Role == SessionRole.RemoteClient) return;
            var list = LockList.Parse(((ConfigEntry<string>)entries[ThingSettings.General.LockedPlayerSettings]).Value);
            var text = LockedValues.Compose(list, id => ThingSettings.Find(id) is ThingSetting s && entries.TryGetValue(s, out var e) ? e.GetSerializedValue() : null);
            if (text == lockedValues.Value) return;
            var result = MeadworksConfig.Amend(lockedValues, Product, ConfigScope.ServerAuthoritative, text);
            log.Verbose("Locked player values: " + result.Route + (result.Applied ? " applied" : " not applied"));
        }

        private void ReadLocks(bool force)
        {
            var listText = Get(ThingSettings.General.LockedPlayerSettings);
            if (force || listText != lockListText)
            {
                lockListText = listText;
                lockList = LockList.Parse(listText);
                if (lockList.Unknown.Count > 0 || lockList.NotLockable.Count > 0) log.Warning("Locked Player Settings: " + lockList.Describe());
                else log.Verbose("Locked Player Settings: " + lockList.Describe());
            }
            var carried = MeadworksConfig.Effective(lockedValues, Product, ConfigScope.ServerAuthoritative, "");
            if (force || carried != valuesText)
            {
                valuesText = carried;
                values = LockedValues.Decode(carried);
            }
        }

        private PlayerLock Lock(ThingSetting setting) => PlayerLocks.Evaluate(setting.Id, ServerBinds(), lockList, values);

        /// <summary>
        /// A Thing server binds this client: a remote client whose server values are in force (Thing runs on the server and
        /// authority is confirmed) and whose synchronized configuration arrived during this session.
        /// </summary>
        private bool ServerBinds()
        {
            if (!CruciblePlugin.IsReady) return false;
            var session = CruciblePlugin.Session;
            if (session.Role != SessionRole.RemoteClient || syncReceipt.CurrentGeneration != session.Generation) return false;
            return MeadworksAuthority.Decide(Product, ConfigScope.ServerAuthoritative).Source == ConfigValueSource.Server;
        }

        private void OnSynchronized(object sender, Jotunn.Utils.ConfigurationSynchronizationEventArgs args)
        {
            if (args.UpdatedPluginGUIDs == null || !args.UpdatedPluginGUIDs.Contains(Product) || !CruciblePlugin.IsReady) return;
            syncReceipt.Observe();
            Refresh(force: true);
        }

        private static bool TryConvert<T>(string text, out T value)
        {
            try
            {
                value = (T)TomlTypeConverter.ConvertToValue(text, typeof(T));
                return true;
            }
            catch (Exception)
            {
                value = default!;
                return false;
            }
        }

        /// <summary>The admin decision's server switches from current values (design 4).</summary>
        public ServerRules Rules() => ThingSettings.Rules(Get(ThingSettings.General.AdminListEditable), Get(ThingSettings.Remote.Editing),
            Get(ThingSettings.World.WardOverrideAllowed), Get(ThingSettings.World.TerrainNonAdminOwnPieces), Get(ThingSettings.World.RemoveDropsForEveryone));

        public void Shutdown()
        {
            file.SettingChanged -= OnSettingChanged;
            file.ConfigReloaded -= OnReloaded;
            SynchronizationManager.OnConfigurationSynchronized -= OnSynchronized;
            trackedLocks.Clear();
            foreach (var entry in entries.Values) MeadworksBlockedOptions.Untrack(entry);
            entries.Clear();
            Changed = null;
        }

        private void OnSettingChanged(object sender, SettingChangedEventArgs e)
        {
            if (ReferenceEquals(e.ChangedSetting, settingsGrant) || ReferenceEquals(e.ChangedSetting, settingsStatus)) PublishSettingsAccess();
            Changed?.Invoke();
        }

        private void OnReloaded(object sender, EventArgs e) => Changed?.Invoke();

        private void BindSetting(ThingSetting setting)
        {
            ConfigEntryBase entry;
            if (setting.Hotkey)
            {
                var text = (string)setting.DefaultValue;
                entry = MeadworksConfig.Bind(file, setting.Section, setting.Key, text.Length == 0 ? KeyboardShortcut.Empty : KeyboardShortcut.Deserialize(text),
                    setting.Description, Map(setting.Scope));
            }
            else
            {
                switch (setting)
                {
                    case ThingSetting<bool> b: entry = Bind(b, null); break;
                    case ThingSetting<int> i: entry = Bind(i, new AcceptableValueRange<int>((int)i.Minimum!, (int)i.Maximum!)); break;
                    case ThingSetting<string> s: entry = Bind(s, null); break;
                    case ThingSetting<RemoteConfigScope> c: entry = Bind(c, null); break;
                    default: throw new NotSupportedException(setting.Id + ": unsupported value type " + setting.ValueType.Name + ".");
                }
            }
            entries.Add(setting, entry);
        }

        private ConfigEntry<T> Bind<T>(ThingSetting<T> setting, AcceptableValueBase? acceptable)
        {
            if (ReferenceEquals(setting, ThingSettings.Remote.AllowList))
                return MeadworksConfig.Bind(file, setting.Section, setting.Key, setting.Default,
                    new ConfigDescription(setting.Description, acceptable, new ConfigDependency(
                        () => Get(ThingSettings.Remote.Scope) == RemoteConfigScope.AllowList,
                        () => "Requires Remote Config Scope: AllowList.")), Map(setting.Scope));
            return MeadworksConfig.Bind(file, setting.Section, setting.Key, setting.Default, setting.Description, Map(setting.Scope), acceptable);
        }
    }
}
