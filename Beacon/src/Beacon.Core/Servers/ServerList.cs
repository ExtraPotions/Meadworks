// Beacon - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original, behavior only: saved servers with aliases sharing one password follow AutoConnector 1.0.0's README
// (ServerAliases); the default flag, order and secret references are Meadworks additions (design F9, 4.1).
using System;
using System.Collections.Generic;
using System.Linq;

namespace ExtraPotions.Beacon.Servers
{
    /// <summary>
    /// One saved server: its display name, the address added to favorites, other addresses of the same server, the default
    /// flag and the opaque reference of its secret in the secret store. It never holds a password (design 4.1).
    /// </summary>
    public sealed class ServerEntry
    {
        internal ServerEntry(string name, ServerAddress address, IReadOnlyList<ServerAddress> aliases, string secretRef)
        {
            Name = name;
            Address = address;
            Aliases = aliases;
            SecretRef = secretRef;
        }

        public string Name { get; }
        public ServerAddress Address { get; }
        public IReadOnlyList<ServerAddress> Aliases { get; }
        public string SecretRef { get; }
        public bool IsDefault { get; internal set; }

        public bool Matches(ServerAddress address) => Address == address || Aliases.Any(a => a == address);

        public IEnumerable<ServerAddress> AllAddresses => new[] { Address }.Concat(Aliases);
    }

    /// <summary>Metadata-only undo of one removed favorite; the protected secret remains in its platform store.</summary>
    public sealed class ServerRemoval
    {
        internal ServerRemoval(ServerEntry entry, int index) { Entry = entry; Index = index; WasDefault = entry.IsDefault; }
        public ServerEntry Entry { get; }
        public int Index { get; }
        public bool WasDefault { get; }
    }

    /// <summary>The ordered server list (design F9). Names are unique ignoring case; no address appears twice.</summary>
    public sealed class ServerList
    {
        readonly List<ServerEntry> entries = new List<ServerEntry>();
        readonly Func<string> newReference;
        int revision;

        /// <param name="newReference">Creates a fresh secret reference; the default uses random GUID text (not a secret itself).</param>
        public ServerList(Func<string>? newReference = null)
        {
            this.newReference = newReference ?? (() => "s" + Guid.NewGuid().ToString("N"));
        }

        public IReadOnlyList<ServerEntry> Entries => entries;

        public int Count => entries.Count;

        /// <summary>Advances when the saved list changes. Reading the list does not.</summary>
        public int Revision => revision;

        public ServerEntry? Default => entries.FirstOrDefault(e => e.IsDefault);

        public ServerEntry? Find(ServerAddress address) =>
            address == null ? null : entries.FirstOrDefault(e => e.Matches(address));

        public ServerEntry? FindByName(string name) =>
            entries.FirstOrDefault(e => string.Equals(e.Name, (name ?? "").Trim(), StringComparison.OrdinalIgnoreCase));

        public ServerEntry Add(string name, ServerAddress address, IEnumerable<ServerAddress>? aliases = null) =>
            Insert(name, address, aliases, null);

        /// <summary>Adds an entry read from the file, keeping its reference.</summary>
        internal ServerEntry Insert(string name, ServerAddress address, IEnumerable<ServerAddress>? aliases, string? secretRef)
        {
            var trimmed = (name ?? "").Trim();
            if (trimmed.Length == 0) throw new ArgumentException("A server needs a name.", nameof(name));
            if (address == null) throw new ArgumentNullException(nameof(address));
            if (FindByName(trimmed) != null) throw new ArgumentException("A server named '" + trimmed + "' is already saved.", nameof(name));
            var others = (aliases ?? Enumerable.Empty<ServerAddress>()).Where(a => a != null && a != address).Distinct().ToList();
            foreach (var a in new[] { address }.Concat(others))
                if (Find(a) != null) throw new ArgumentException("Address " + a + " already belongs to another saved server.", nameof(address));
            var reference = secretRef ?? NewUniqueReference();
            if (entries.Any(e => e.SecretRef == reference)) throw new ArgumentException("Duplicate secret reference.", nameof(secretRef));
            var entry = new ServerEntry(trimmed, address, others.AsReadOnly(), reference);
            entries.Add(entry);
            revision++;
            return entry;
        }

        public bool Remove(string name)
        {
            var entry = FindByName(name);
            if (entry == null || !entries.Remove(entry)) return false;
            revision++;
            return true;
        }

        public ServerRemoval? RemoveForUndo(string name)
        {
            var entry = FindByName(name); if (entry == null) return null;
            var removal = new ServerRemoval(entry, entries.IndexOf(entry));
            entries.Remove(entry);
            revision++;
            return removal;
        }

        /// <summary>Restores only if names, addresses and references are still free; a newer chosen default wins.</summary>
        public ServerEntry Restore(ServerRemoval removal)
        {
            if (removal == null) throw new ArgumentNullException(nameof(removal));
            var original = removal.Entry;
            var restored = Insert(original.Name, original.Address, original.Aliases, original.SecretRef);
            Move(restored.Name, removal.Index);
            if (removal.WasDefault && Default == null) SetDefault(restored.Name);
            return restored;
        }

        /// <summary>Edits presentation and addresses without changing the order, default or protected-secret reference.</summary>
        public ServerEntry Edit(string name, string nextName, ServerAddress address, IEnumerable<ServerAddress>? aliases = null)
        {
            var original = FindByName(name) ?? throw new ArgumentException("Unknown saved server.", nameof(name));
            var trimmed = (nextName ?? "").Trim();
            if (trimmed.Length == 0) throw new ArgumentException("A server needs a name.", nameof(nextName));
            if (address == null) throw new ArgumentNullException(nameof(address));
            var others = (aliases ?? Enumerable.Empty<ServerAddress>()).Where(a => a != null && a != address).Distinct().ToList();
            var remaining = entries.Where(e => !ReferenceEquals(e, original)).ToList();
            if (remaining.Any(e => string.Equals(e.Name, trimmed, StringComparison.OrdinalIgnoreCase)))
                throw new ArgumentException("Duplicate server name.", nameof(nextName));
            if (new[] { address }.Concat(others).Any(a => remaining.Any(e => e.Matches(a))))
                throw new ArgumentException("Address already belongs to another server.", nameof(address));
            var replacement = new ServerEntry(trimmed, address, others.AsReadOnly(), original.SecretRef) { IsDefault = original.IsDefault };
            entries[entries.IndexOf(original)] = replacement;
            revision++;
            return replacement;
        }

        public void SetDefault(string? name)
        {
            var entry = name == null ? null : FindByName(name);
            if (name != null && entry == null) throw new ArgumentException("No saved server named '" + name + "'.", nameof(name));
            foreach (var e in entries) e.IsDefault = ReferenceEquals(e, entry);
            revision++;
        }

        public void Move(string name, int index)
        {
            var entry = FindByName(name) ?? throw new ArgumentException("No saved server named '" + name + "'.", nameof(name));
            entries.Remove(entry);
            entries.Insert(Math.Max(0, Math.Min(entries.Count, index)), entry);
            revision++;
        }

        string NewUniqueReference()
        {
            for (var i = 0; i < 16; i++)
            {
                var r = newReference();
                if (entries.All(e => e.SecretRef != r)) return r;
            }
            throw new InvalidOperationException("Could not create a unique secret reference.");
        }
    }
}
