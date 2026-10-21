// Ember - ExtraPotions Meadworks.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original; same binding model as Kindle.Plugin (expDARE).
using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;
using ExtraPotions.Ember.Config;
using ExtraPotions.Meadworks.Crucible;
using Jotunn.Extensions;
using Jotunn.Managers;
using CruciblePlugin = ExtraPotions.Meadworks.Crucible.Plugin;

namespace ExtraPotions.Ember.Runtime
{
    /// <summary>
    /// Ember's configuration surface (E02, design section 4). Every setting of <see cref="EmberSettings.All"/> is bound
    /// through <see cref="MeadworksConfig.Bind{T}(ConfigFile,string,string,T,string,ConfigScope,AcceptableValueBase)"/>
    /// with its scope (cheats through <see cref="MeadworksConfig.BindCheat{T}"/>). Values are read at use time:
    /// <list type="bullet">
    /// <item>Local: the player's value, or the server's value while the server locks it (Locked Player Settings).</item>
    /// <item>ServerAuthoritative: <see cref="MeadworksConfig.Effective{T}"/> (the server's value when Ember runs there).</item>
    /// <item>Cheat: the authority gate's decision; a blocked cheat reads its safe value (<see cref="EmberCheats"/>).</item>
    /// </list>
    /// ServerAuthoritative, cheat and locked entries are kept read-only in configuration managers while blocked
    /// (<see cref="MeadworksBlockedOptions.Track"/>). Main thread only.
    /// </summary>
    public sealed class EmberConfig
    {
        /// <summary>Hidden ServerAuthoritative carrier of the server's values for the locked Local settings.</summary>
        public const string LockedValuesKey = "Locked Player Values";

        private const string Product = Plugin.Guid;
        private const float RefreshSeconds = 0.5f;

        private static readonly HashSet<EmberSetting> Shortcuts = BuildShortcuts();

        private static HashSet<EmberSetting> BuildShortcuts()
        {
            // Initialize the catalog before its nested setting holders. Mono eagerly initializes
            // the containing type when a holder creates its normalization delegate.
            _ = EmberSettings.All;
            return new HashSet<EmberSetting> { EmberSettings.Doors.ToggleKey, EmberSettings.Torches.TorchSwitchKey };
        }

        private readonly ConfigFile file;
        private readonly MeadworksLog log;
        private readonly Dictionary<EmberSetting, ConfigEntryBase> entries = new Dictionary<EmberSetting, ConfigEntryBase>();
        private readonly Dictionary<ConfigEntryBase, EmberSetting> settingsByEntry = new Dictionary<ConfigEntryBase, EmberSetting>();
        private readonly HashSet<EmberSetting> trackedLocks = new HashSet<EmberSetting>();
        private readonly ConfigEntry<string> lockedValues;
        private readonly EmberPresetRuntime presets;

        private string? lockListText;
        private LockList lockList = LockList.Empty;
        private string? valuesText;
        private IReadOnlyDictionary<string, string> values = new Dictionary<string, string>();
        private long syncedGeneration = -1;
        private readonly EmberSyncReceipt syncReceipt = new EmberSyncReceipt();
        private float nextRefresh;
        public event EventHandler? Changed;
        private void OnSettingChanged(object sender, SettingChangedEventArgs args) => Changed?.Invoke(this, EventArgs.Empty);

        private EmberConfig(ConfigFile file, MeadworksLog log)
        {
            this.file = file;
            this.log = log;
            var save = file.SaveOnConfigSet;
            file.SaveOnConfigSet = false;
            try
            {
                foreach (var setting in EmberSettings.All) BindSetting(setting);
                // Not a player-facing setting: the C01 hidden bind keeps full Crucible ownership, hides the entry from
                // configuration UIs (Browsable=false, ReadOnly=true) and synchronizes it as a ServerAuthoritative scope.
                lockedValues = MeadworksConfig.BindHidden(file, EmberSettings.Server.Section, LockedValuesKey, "",
                    "Managed by Ember: the server's values for Locked Player Settings. Do not edit.", ConfigScope.ServerAuthoritative);
                MoveDoorDistanceDefaults();
                presets = new EmberPresetRuntime(file, this, log);
            }
            finally
            {
                file.SaveOnConfigSet = save;
            }
            file.Save();
            file.SettingChanged += OnSettingChanged;
            SynchronizationManager.OnConfigurationSynchronized += OnSynchronized;
        }

        /// <summary>Binds every Ember setting on the plugin's config file and starts tracking blocked options.</summary>
        public static EmberConfig Bind(ConfigFile file, MeadworksLog log)
        {
            if (file == null) throw new ArgumentNullException(nameof(file));
            if (log == null) throw new ArgumentNullException(nameof(log));
            var config = new EmberConfig(file, log);
            foreach (var pair in config.entries.Where(p => p.Key.Scope != SettingScope.Local))
                MeadworksBlockedOptions.Track(Product, pair.Value, Map(pair.Key.Scope));
            config.Refresh(force: true);
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

        public ConfigEntryBase Entry(EmberSetting setting) =>
            entries.TryGetValue(setting ?? throw new ArgumentNullException(nameof(setting)), out var entry) ? entry : throw new ArgumentException("Not an Ember setting.", nameof(setting));

        /// <summary>The value Ember acts on now (see the class summary). Shortcut settings use <see cref="Shortcut"/>.</summary>
        public T Get<T>(EmberSetting<T> setting)
        {
            if (Shortcuts.Contains(setting)) throw new ArgumentException(setting.Id + " is a keyboard shortcut; use Shortcut.", nameof(setting));
            var entry = (ConfigEntry<T>)Entry(setting);
            switch (setting.Scope)
            {
                case SettingScope.Cheat:
                    return EmberCheats.Effective(setting, CheatLocked(), entry.Value);
                case SettingScope.ServerAuthoritative:
                    return setting.Normalize(MeadworksConfig.Effective(entry, Product, ConfigScope.ServerAuthoritative, setting.Default));
                default:
                    var held = Lock(setting);
                    if (held.Locked && TryConvert(held.ServerText!, out T server)) return setting.Normalize(server);
                    return setting.Normalize(entry.Value);
            }
        }

        /// <summary>A keyboard shortcut setting (Doors Toggle Key), honoring server locks.</summary>
        public KeyboardShortcut Shortcut(EmberSetting<string> setting)
        {
            if (!Shortcuts.Contains(setting)) throw new ArgumentException(setting.Id + " is not a keyboard shortcut.", nameof(setting));
            var entry = (ConfigEntry<KeyboardShortcut>)Entry(setting);
            var held = Lock(setting);
            if (held.Locked)
            {
                try { return KeyboardShortcut.Deserialize(held.ServerText!); }
                catch (Exception) { }
            }
            return entry.Value;
        }

        /// <summary>Sleep Any Time in effect: off whenever the gate blocks cheats.</summary>
        public bool SleepAnyTime => Get(EmberSettings.Sleep.SleepAnyTime);

        /// <summary>Endless Torches in effect: off whenever the gate blocks cheats.</summary>
        public bool EndlessTorches => Get(EmberSettings.Torches.EndlessTorches);

        /// <summary>Testing offsets in effect (S12): none unless the Testing Mode cheat is allowed and on.</summary>
        public ExtraPotions.Ember.Sleep.TestingOffsets Testing => EmberCheats.Testing(CheatLocked(), Get(EmberSettings.Sleep.TestingMode),
            Get(EmberSettings.Sleep.FakeTotalPlayers), Get(EmberSettings.Sleep.SimulatePlayersInBed));

        /// <summary>The settings Ember acts on for one door kind (Local, honoring server locks).</summary>
        public (ExtraPotions.Ember.Doors.AutomationMechanic Mechanic, float Open, float Close, bool CheckForKey) Door(ExtraPotions.Ember.Doors.DoorKind kind)
        {
            var d = EmberSettings.Door(kind);
            var (open, close) = ExtraPotions.Ember.Doors.DoorDistances.Normalize(Get(d.OpenDistance), Get(d.CloseDistance));
            return (Get(d.Mechanic), open, close, d.CheckForKey != null && Get(d.CheckForKey));
        }

        /// <summary>The server's lock list as parsed; empty unless a Ember server binds this client.</summary>
        public LockList Locks => lockList;

        /// <summary>Whether the server forces this Local setting to its own value right now.</summary>
        public bool IsLocked(EmberSetting setting) => setting != null && setting.Lockable && Lock(setting).Locked;

        /// <summary>
        /// Whether Ember may act on a cheat now; tells the player why not (once per option per session, through the
        /// Crucible notifier). <paramref name="label"/> is the localized option name.
        /// </summary>
        public bool TryUseCheat(EmberSetting cheat, string? label = null)
        {
            if (cheat == null || cheat.Scope != SettingScope.Cheat) throw new ArgumentException("Expected an Ember cheat.", nameof(cheat));
            return MeadworksBlockedOptions.TryUse(Product, cheat.Id, ConfigScope.Cheat, label);
        }

        /// <summary>
        /// Tells the player that a setting is blocked when they try to change it (once per option per session): a
        /// ServerAuthoritative or cheat setting the gate blocks, or a Local setting the server locks. True when the
        /// change may go ahead.
        /// </summary>
        public bool TryChange(EmberSetting setting, string? label = null)
        {
            if (setting == null) throw new ArgumentNullException(nameof(setting));
            if (setting.Scope != SettingScope.Local) return MeadworksBlockedOptions.TryEdit(Product, setting.Id, Map(setting.Scope), label);
            if (!IsLocked(setting)) return true;
            var status = BlockedOption.From(Product, setting.Id, MeadworksAuthority.Decide(Product, ConfigScope.ServerAuthoritative));
            MeadworksBlockedOptions.Notify(status, label);
            return status.Allowed;
        }

        /// <summary>Called from the plugin's Update: keeps the lock state, read-only tags and the server's locked values current.</summary>
        public void Tick(float now)
        {
            presets.Tick();
            if (now < nextRefresh) return;
            nextRefresh = now + RefreshSeconds;
            Refresh(force: false);
        }

        public void Shutdown()
        {
            presets.Dispose();
            file.SettingChanged -= OnSettingChanged;
            Changed = null;
            SynchronizationManager.OnConfigurationSynchronized -= OnSynchronized;
            foreach (var setting in trackedLocks) MeadworksBlockedOptions.Untrack(entries[setting]);
            trackedLocks.Clear();
            foreach (var entry in entries.Values.Where(e => !settingsByEntry[e].Lockable)) MeadworksBlockedOptions.Untrack(entry);
        }

        private void Refresh(bool force)
        {
            try
            {
                PublishLockedValues();
                ReadLocks(force);
                foreach (var setting in EmberSettings.All.Where(s => s.Lockable))
                {
                    var locked = IsLocked(setting);
                    if (locked && trackedLocks.Add(setting))
                        MeadworksBlockedOptions.Track(Product, entries[setting], ConfigScope.ServerAuthoritative);
                    else if (!locked && trackedLocks.Remove(setting))
                        MeadworksBlockedOptions.Untrack(entries[setting]);
                }
            }
            catch (Exception error)
            {
                log.Warning("Configuration refresh failed: " + error.GetType().Name + ": " + error.Message);
            }
        }

        /// <summary>
        /// On the machine that runs the world (host, dedicated server; also single player and the menu, so the text is
        /// current when hosting starts) the hidden carrier holds this machine's own values of the locked settings.
        /// Jotunn sends it to clients with the rest of the synchronized configuration.
        /// </summary>
        private void PublishLockedValues()
        {
            if (!CruciblePlugin.IsReady || CruciblePlugin.Session.Role == SessionRole.RemoteClient) return;
            var list = LockList.Parse(((ConfigEntry<string>)entries[EmberSettings.Server.LockedPlayerSettings]).Value);
            var text = LockedValues.Compose(list, id => EmberSettings.Find(id) is EmberSetting s && entries.TryGetValue(s, out var e) ? e.GetSerializedValue() : null);
            if (text == lockedValues.Value) return;
            var result = MeadworksConfig.Amend(lockedValues, Product, ConfigScope.ServerAuthoritative, text);
            log.Verbose("Locked player values: " + result);
        }

        private void ReadLocks(bool force)
        {
            var listText = Get(EmberSettings.Server.LockedPlayerSettings);
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

        private PlayerLock Lock(EmberSetting setting) => PlayerLocks.Evaluate(setting.Id, ServerBinds(), lockList, values);

        /// <summary>
        /// A Ember server binds this client: a remote client whose server values are in force (Ember runs on the
        /// server and authority is confirmed) and whose synchronized configuration arrived during this session.
        /// </summary>
        private bool ServerBinds()
        {
            if (!CruciblePlugin.IsReady) return false;
            var session = CruciblePlugin.Session;
            syncedGeneration = syncReceipt.CurrentGeneration;
            if (session.Role != SessionRole.RemoteClient || syncedGeneration != session.Generation) return false;
            return MeadworksAuthority.Decide(Product, ConfigScope.ServerAuthoritative).Source == ConfigValueSource.Server;
        }

        private static bool CheatLocked() => MeadworksAuthority.Decide(Product, ConfigScope.Cheat).Source == ConfigValueSource.Locked;

        private void OnSynchronized(object sender, Jotunn.Utils.ConfigurationSynchronizationEventArgs args)
        {
            if (args.UpdatedPluginGUIDs == null || !args.UpdatedPluginGUIDs.Contains(Product) || !CruciblePlugin.IsReady) return;
            syncReceipt.Observe();
            syncedGeneration = syncReceipt.CurrentGeneration;
            Refresh(force: true);
        }

        private void BindSetting(EmberSetting setting)
        {
            ConfigEntryBase entry;
            if (Shortcuts.Contains(setting))
                entry = MeadworksConfig.Bind(file, setting.Section, setting.Key, (string)setting.DefaultValue == "" ? KeyboardShortcut.Empty : KeyboardShortcut.Deserialize((string)setting.DefaultValue), Describe(setting, null), Map(setting.Scope));
            else
            {
                switch (setting)
                {
                    case EmberSetting<bool> b: entry = Bind(b, null); break;
                    case EmberSetting<int> i: entry = Bind(i, new AcceptableValueRange<int>((int)i.Minimum!, (int)i.Maximum!)); break;
                    case EmberSetting<float> f: entry = Bind(f, new AcceptableValueRange<float>((float)f.Minimum!, (float)f.Maximum!)); break;
                    case EmberSetting<string> s: entry = Bind(s, null); break;
                    case EmberSetting<ExtraPotions.Ember.Doors.AutomationMechanic> m: entry = Bind(m, null); break;
                    case EmberSetting<SettingsPreset> p: entry = Bind(p, null); break;
                    default: throw new NotSupportedException(setting.Id + ": unsupported value type " + setting.ValueType.Name + ".");
                }
            }
            entries.Add(setting, entry);
            settingsByEntry.Add(entry, setting);
        }

        /// <summary>
        /// Door Open and Close Distance now default to half the earlier defaults. Once per config file (hidden marker), every saved distance
        /// still equal to its kind's earlier default moves to the new default; customized values are kept. One log line
        /// when anything moved.
        /// </summary>
        private void MoveDoorDistanceDefaults()
        {
            var marker = MeadworksConfig.BindHidden(file, ExtraPotions.Ember.Doors.DoorDistanceDefaults.MarkerSection,
                ExtraPotions.Ember.Doors.DoorDistanceDefaults.MarkerKey, 0,
                "Managed by Ember: records that the door distance defaults update ran for this config. Do not edit.", ConfigScope.Local);
            if (marker.Value >= ExtraPotions.Ember.Doors.DoorDistanceDefaults.Current) return;
            var byId = entries.ToDictionary(p => p.Key.Id, p => p.Value);
            var moved = ExtraPotions.Ember.Doors.DoorDistanceDefaults.Plan(marker.Value,
                ExtraPotions.Ember.Doors.DoorDistanceDefaults.Entries(id => ((ConfigEntry<float>)byId[id]).Value));
            foreach (var entry in moved) ((ConfigEntry<float>)byId[entry.Id]).Value = entry.NewDefault;
            marker.Value = ExtraPotions.Ember.Doors.DoorDistanceDefaults.Current;
            if (moved.Count > 0)
                log.Info("Door distance defaults updated: " + moved.Count + " Open/Close Distance values that held the earlier default now use"
                    + " the halved default (doors 1.5 / 2.25 m, drawbridges 3 / 4 m). Customized distances were kept.");
        }

        private ConfigDescription Describe(EmberSetting setting, AcceptableValueBase? acceptable, string? text = null)
        {
            ConfigDependency? dependency = null;
            if (ReferenceEquals(setting, EmberSettings.Doors.Preset))
                dependency = new ConfigDependency(() => !IsLocked(EmberSettings.Doors.Preset) && !EmberPresets.All.Single(g => g.Selector == EmberSettings.Doors.Preset).Members.Any(IsLocked),
                    () => EmberText.Text("preset_locked"));
            if (ReferenceEquals(setting, EmberSettings.Sleep.NearestOwnedBedRespawn) ||
                ReferenceEquals(setting, EmberSettings.Sleep.QuickRespawn))
                dependency = new ConfigDependency(() => Get(EmberSettings.Sleep.Enabled), () => "Requires " + EmberSettings.Sleep.Enabled.Section + ": " + EmberSettings.Sleep.Enabled.Key + ".");
            if (ReferenceEquals(setting, EmberSettings.Sleep.OwnedBedMapPins))
                dependency = new ConfigDependency(() => Get(EmberSettings.Sleep.Enabled) && Get(EmberSettings.Sleep.NearestOwnedBedRespawn), () => "Requires Sleep: Enabled and Nearest Owned Bed Respawn.");
            return new ConfigDescription(text ?? setting.Description, acceptable, dependency == null ? Array.Empty<object>() : new object[] { dependency });
        }

        private ConfigEntry<T> Bind<T>(EmberSetting<T> setting, AcceptableValueBase? acceptable) =>
            setting.Scope == SettingScope.Cheat
                ? MeadworksConfig.BindCheat(file, setting.Section, setting.Key, setting.Safe, setting.Description, acceptable)
                : MeadworksConfig.Bind(file, setting.Section, setting.Key, setting.Default, Describe(setting, acceptable), Map(setting.Scope));

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

        private static int Count(SettingScope scope) => EmberSettings.All.Count(s => s.Scope == scope);
    }
}
