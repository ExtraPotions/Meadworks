// Beacon - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original, behavior only for the first two toggles (AutoConnector 1.0.0 always adds favorites and skips the prompt);
// same setting model as Thing.Core and Tempest.Core (expDARE), without the Cheat scope. No AutoConnector code was read.
using System;
using System.Collections.Generic;
using ExtraPotions.Beacon.Secrets;

namespace ExtraPotions.Beacon.Config
{
    /// <summary>
    /// Beacon's mirror of Crucible's <c>ConfigScope</c> (Beacon.Core references nothing beyond netstandard). Beacon has no
    /// Cheat settings (design section 6). Personal settings are Local; a server running Beacon may restrict the ones marked
    /// <see cref="BeaconSetting.ServerLockable"/>.
    /// </summary>
    public enum SettingScope { Local, ServerAuthoritative }

    /// <summary>One configuration entry from design section 5.</summary>
    public abstract class BeaconSetting
    {
        private protected BeaconSetting(string section, string key, string description, bool hotkey, bool serverLockable, SettingScope scope)
        {
            if (string.IsNullOrWhiteSpace(section) || section.Trim() != section) throw new ArgumentException("Expected a trimmed section name.", nameof(section));
            if (string.IsNullOrWhiteSpace(key) || key.Trim() != key) throw new ArgumentException("Expected a trimmed key.", nameof(key));
            if (string.IsNullOrWhiteSpace(description)) throw new ArgumentException("Expected a description.", nameof(description));
            Section = section;
            Key = key;
            Description = description;
            Hotkey = hotkey;
            ServerLockable = serverLockable;
            Scope = scope;
        }

        public string Section { get; }
        public string Key { get; }
        public string Id => Section + "." + Key;
        public SettingScope Scope { get; }
        public string Description { get; }

        /// <summary>A keyboard shortcut; the value is BepInEx's serialized shortcut text (empty: unassigned).</summary>
        public bool Hotkey { get; }

        /// <summary>A server running Beacon may lock this setting (section 6: only Reconnect On Disconnect).</summary>
        public bool ServerLockable { get; }

        public abstract Type ValueType { get; }
        public abstract object DefaultValue { get; }
        public abstract object? Minimum { get; }
        public abstract object? Maximum { get; }

        public override string ToString() => Id;
    }

    public sealed class BeaconSetting<T> : BeaconSetting
    {
        readonly Func<T, T> normalize;

        internal BeaconSetting(string section, string key, T defaultValue, string description, Func<T, T> normalize,
            object? minimum = null, object? maximum = null, bool hotkey = false, bool serverLockable = false, SettingScope scope = SettingScope.Local)
            : base(section, key, description, hotkey, serverLockable, scope)
        {
            Default = defaultValue;
            this.normalize = normalize;
            Minimum = minimum;
            Maximum = maximum;
        }

        public T Default { get; }
        public override Type ValueType => typeof(T);
        public override object DefaultValue => Default!;
        public override object? Minimum { get; }
        public override object? Maximum { get; }

        public T Normalize(T value) => normalize(value);
    }

    public static class BeaconSettings
    {
        static BeaconSetting<bool> Toggle(string section, string key, bool value, string description, bool lockable = false) =>
            new BeaconSetting<bool>(section, key, value, description, v => v, serverLockable: lockable);

        static BeaconSetting<int> Integer(string section, string key, int value, int min, int max, string description) =>
            new BeaconSetting<int>(section, key, value, description, v => Math.Max(min, Math.Min(max, v)), min, max);

        static BeaconSetting<string> Text(string section, string key, string description, bool hotkey = false) =>
            new BeaconSetting<string>(section, key, "", description, v => (v ?? "").Trim(), hotkey: hotkey);

        public static class General
        {
            public const string Section = "General";
            public static readonly BeaconSetting<bool> AddToFavorites = Toggle(Section, "Add To Favorites", true,
                "Adds every saved server to the Favorite tab of the Join Game screen.");
            public static readonly BeaconSetting<bool> SkipPasswordPrompt = Toggle(Section, "Skip Password Prompt", true,
                "Sends a saved server's stored password when the game asks for it, so the prompt is not shown.");
            public static readonly BeaconSetting<bool> OfferToSavePasswords = Toggle(Section, "Offer To Save Passwords", true,
                "After a server accepts a password you typed, asks once per server whether Beacon should save it.");
            public static readonly BeaconSetting<bool> RemoveEntryWithFavorite = Toggle(Section, "Remove Entry With Favorite", true,
                "Removing a saved server from the game's favorites also removes it from Beacon (with an undo message).");
            public static readonly BeaconSetting<string> PanelKey = Text(Section, "Panel Key",
                "Keyboard shortcut that opens the Beacon panel. Empty: only the Join Game screen button opens it.", hotkey: true);
        }

        public static class AutoJoin
        {
            public const string Section = "Auto Join";
            public static readonly BeaconSetting<bool> Enabled = Toggle(Section, "Auto Join", false,
                "Joins a saved server automatically after the game reaches the main menu, after a short countdown you can cancel.");
            public static readonly BeaconSetting<string> Server = Text(Section, "Auto Join Server",
                "Name of the saved server to join. Empty: the server marked as default.");
            public static readonly BeaconSetting<string> Character = Text(Section, "Auto Join Character",
                "Character to join with. Empty: the last character used.");
            public static readonly BeaconSetting<int> CountdownSeconds = Integer(Section, "Auto Join Countdown Seconds", 5, 0, 30,
                "Seconds of countdown before the automatic join; Escape or the pad's B button cancels.");
        }

        public static class Reconnect
        {
            public const string Section = "Reconnect";
            public static readonly BeaconSetting<bool> Enabled = Toggle(Section, "Reconnect On Disconnect", false,
                "Reconnects after an unexpected disconnect. Never after a kick or ban. A server running Beacon may turn this off.", lockable: true);
            public static readonly BeaconSetting<int> Attempts = Integer(Section, "Reconnect Attempts", 3, 1, 10,
                "Most reconnect attempts after one disconnect.");
        }

        public static class Passwords
        {
            public const string Section = "Passwords";
            public static readonly BeaconSetting<LinuxFallback> LinuxFallback = new BeaconSetting<LinuxFallback>(Section, "Linux Fallback",
                SecretStores.DefaultLinuxFallback,
                "On Linux without a usable keyring: SessionOnly remembers passwords until the game closes; Disabled never stores them. On Linux a change applies right away. On systems other than Windows and Linux, restart the game after changing it.",
                v => Enum.IsDefined(typeof(LinuxFallback), v) ? v : SecretStores.DefaultLinuxFallback);
        }

        public static class Server
        {
            public static readonly BeaconSetting<bool> AllowReconnect = new BeaconSetting<bool>("Server", "Allow Automatic Reconnect", true,
                "Server policy: false prevents Beacon clients from automatically reconnecting after an unexpected disconnect. True leaves each player's preference unchanged.",
                v => v, scope: SettingScope.ServerAuthoritative);
        }

        public static IReadOnlyList<BeaconSetting> All { get; } = Array.AsReadOnly(new BeaconSetting[]
        {
            General.AddToFavorites, General.SkipPasswordPrompt, General.OfferToSavePasswords, General.RemoveEntryWithFavorite, General.PanelKey,
            AutoJoin.Enabled, AutoJoin.Server, AutoJoin.Character, AutoJoin.CountdownSeconds,
            Reconnect.Enabled, Reconnect.Attempts,
            Passwords.LinuxFallback, Server.AllowReconnect,
        });
    }
}
