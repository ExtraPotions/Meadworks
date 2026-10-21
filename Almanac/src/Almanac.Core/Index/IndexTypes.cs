// Almanac - ExtraPotions Meadworks settings book.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
using System;

namespace ExtraPotions.Almanac.Index
{
    /// <summary>A plugin as BepInEx reports it (GUID, display name, version text).</summary>
    public sealed class PluginIdentity
    {
        public PluginIdentity(string guid, string name, string version)
        {
            if (string.IsNullOrEmpty(guid)) throw new ArgumentException("Plugin GUID is required", nameof(guid));
            Guid = guid;
            Name = string.IsNullOrEmpty(name) ? guid : name;
            Version = version ?? "";
        }

        public string Guid { get; }
        public string Name { get; }
        public string Version { get; }

        public override string ToString() => Name + " (" + Guid + " " + Version + ")";
    }

    /// <summary>How a config file reached the index (design section 3.1).</summary>
    public enum DiscoveryRoute { PluginInstance, ConstructorHook, FieldScan, CoreConfig, DiskScan, DataFileRegistry }

    /// <summary>What Almanac may do with a file.</summary>
    public enum FileKind
    {
        /// <summary>A live <c>ConfigFile</c>: entries edit through their <c>ConfigEntry</c>; the text is never edited.</summary>
        BoundConfig,
        /// <summary>BepInEx's own <c>BepInEx.cfg</c>: every entry advanced and restart-required.</summary>
        CoreConfig,
        /// <summary>A <c>.cfg</c> on disk that no live <c>ConfigFile</c> owns: read-only text view (Q4).</summary>
        UnboundConfig,
        /// <summary>A Meadworks YAML file registered with the Crucible YAML hub: the only text-editable kind (Q4).</summary>
        DataFile,
    }

    public enum GroupKind { Plugin, UnknownOwner, Core, DiskFiles }

    /// <summary>Section and key of a setting.</summary>
    public readonly struct EntryKey : IEquatable<EntryKey>
    {
        public EntryKey(string section, string key)
        {
            Section = section ?? "";
            Key = key ?? "";
        }

        public string Section { get; }
        public string Key { get; }

        public bool Equals(EntryKey other) => string.Equals(Section, other.Section, StringComparison.Ordinal) && string.Equals(Key, other.Key, StringComparison.Ordinal);
        public override bool Equals(object? obj) => obj is EntryKey other && Equals(other);
        public override int GetHashCode() => unchecked(StringComparer.Ordinal.GetHashCode(Section ?? "") * 397 ^ StringComparer.Ordinal.GetHashCode(Key ?? ""));
        public override string ToString() => "[" + Section + "] " + Key;
    }

    /// <summary>One bound setting as the index records it: identity and setting type name, never the value (values are read live).</summary>
    public sealed class ConfigEntryInfo
    {
        public ConfigEntryInfo(string section, string key, string settingTypeName)
        {
            Key = new EntryKey(section, key);
            SettingTypeName = settingTypeName ?? "";
        }

        public EntryKey Key { get; }
        public string Section => Key.Section;
        public string Name => Key.Key;
        public string SettingTypeName { get; }

        public override string ToString() => Key + " : " + SettingTypeName;
    }

    /// <summary>A Meadworks YAML data file registered by its product (Crucible YAML hub, AS05).</summary>
    public sealed class DataFileRegistration
    {
        public DataFileRegistration(PluginIdentity product, string path, string displayName)
        {
            Product = product ?? throw new ArgumentNullException(nameof(product));
            if (string.IsNullOrEmpty(path)) throw new ArgumentException("Path is required", nameof(path));
            Path = path;
            DisplayName = string.IsNullOrEmpty(displayName) ? System.IO.Path.GetFileName(path) : displayName;
        }

        public PluginIdentity Product { get; }
        public string Path { get; }
        public string DisplayName { get; }
    }
}
