// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-owned reuse: Almanac 64a81614deb039db71d88ff70803ee648d3317dd LockResolver registry/CurrentOwner/Writable algorithms, restricted to actual server authority; no client receipt observer.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using BepInEx.Configuration;
using ExtraPotions.Meadworks.Crucible;
using ExtraPotions.Thing.Runtime.ConfigEditing;
using Jotunn.Managers;
using CruciblePlugin = ExtraPotions.Meadworks.Crucible.Plugin;

namespace ExtraPotions.Thing.Runtime
{
    internal static class ThingExternalConfig
    {
        const BindingFlags Members = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
        internal sealed class Facts
        {
            internal bool Allowed, Hidden;
            internal string Kind = "unknown";
            internal object? System, Data;
            internal MethodInfo? Cache, Send;
            internal string Stamp => Kind + ":" + Allowed + ":" + Hidden + ":" + Identity(System) + ":" + Identity(Data);
        }
        sealed class Owner
        {
            internal object System = null!, Data = null!;
            internal bool Conditional;
        }
        static string Identity(object? value) => value == null ? "-" : RuntimeHelpers.GetHashCode(value).ToString(System.Globalization.CultureInfo.InvariantCulture);
        static bool Authority => CruciblePlugin.IsReady && CruciblePlugin.Session.AuthorityReady && ThingServer.CurrentAuthority && ZNet.instance != null && ZNet.instance.IsServer();
        internal static object? Read(object target, string name)
        {
            var type = target as Type ?? target.GetType(); var instance = target is Type ? null : target;
            for (var current = type; current != null; current = current.BaseType)
            {
                var property = current.GetProperty(name, Members | BindingFlags.DeclaredOnly); if (property != null) return property.GetValue(instance);
                var field = current.GetField(name, Members | BindingFlags.DeclaredOnly); if (field != null) return field.GetValue(instance);
            }
            return null;
        }
        static object[] Bounded(IEnumerable sequence, int max)
        { var values = sequence.Cast<object>().Take(max + 1).ToArray(); if (values.Length > max) throw new InvalidOperationException("ConfigRegistryBound"); return values; }
        static bool CurrentOwner(Owner owner, ConfigEntryBase entry)
        {
            return ReferenceEquals(Read(owner.Data, "BaseConfig"), entry) && Read(owner.System.GetType(), "configSyncs") is IEnumerable systems
                && Bounded(systems, 4096).Any(system => ReferenceEquals(system, owner.System)) && Read(owner.System, "allConfigs") is IEnumerable entries
                && Bounded(entries, 10000).Any(data => ReferenceEquals(data, owner.Data));
        }
        static bool Writable(Owner owner, ConfigEntryBase entry)
        {
            if (!CurrentOwner(owner, entry) || Read(owner.System, "IsSourceOfTruth") is not true) return false;
            var name = owner.Conditional ? "IsWritableConfig" : "isWritableConfig";
            for (var current = owner.System.GetType(); current != null; current = current.BaseType)
            {
                var method = current.GetMethods(Members | BindingFlags.DeclaredOnly).SingleOrDefault(m => m.Name == name && m.ReturnType == typeof(bool)
                    && m.GetParameters().Length == 1 && m.GetParameters()[0].ParameterType.IsInstanceOfType(owner.Data));
                if (method != null) return method.Invoke(method.IsStatic ? null : owner.System, new[] { owner.Data }) is true;
            }
            return false;
        }
        static readonly Dictionary<ConfigEntryBase, Owner> owners = new Dictionary<ConfigEntryBase, Owner>();
        static readonly HashSet<ConfigEntryBase> ambiguous = new HashSet<ConfigEntryBase>();
        static bool scanFailed = true;
        internal static void BeginRequest()
        {
            owners.Clear(); ambiguous.Clear(); scanFailed = false;
            if (!Authority) { scanFailed = true; return; }
            try
            {
                var assemblies = AppDomain.CurrentDomain.GetAssemblies(); if (assemblies.Length > 4096) throw new InvalidOperationException("ConfigAssemblyBound");
                foreach (var assembly in assemblies)
                    foreach (var name in new[] { "ServerSync.ConfigSync", "ConditionalConfigSync.ConditionalConfigSync" })
                    {
                        var type = assembly.GetType(name, false); if (type == null) continue;
                        if (Read(type, "configSyncs") is not IEnumerable systems) { scanFailed = true; continue; }
                        foreach (var system in Bounded(systems, 4096))
                        {
                            if (system == null) continue;
                            if (Read(system, "allConfigs") is not IEnumerable entries) { scanFailed = true; continue; }
                            foreach (var data in Bounded(entries, 10000))
                            {
                                if (data == null || Read(data, "BaseConfig") is not ConfigEntryBase entry) continue;
                                if (ambiguous.Contains(entry)) continue;
                                if (owners.TryGetValue(entry, out var before) && (!ReferenceEquals(before.System, system) || !ReferenceEquals(before.Data, data)))
                                { owners.Remove(entry); ambiguous.Add(entry); continue; }
                                owners[entry] = new Owner { System = system, Data = data, Conditional = name.StartsWith("Conditional", StringComparison.Ordinal) };
                            }
                        }
                    }
            }
            catch { scanFailed = true; }
        }
        internal static void Reset() { owners.Clear(); ambiguous.Clear(); scanFailed = true; }
        internal static Facts Inspect(ConfigEntryBase entry)
        {
            var facts = new Facts();
            if (!Authority) return new Facts { Hidden = true };
            if (MeadworksConfig.TryGetMetadata(entry, out _)) return facts;
            try
            {
                var tags = NativeTags.Read(entry.Description.Tags);
                if (tags.Errors.Count != 0) return new Facts { Hidden = true };
                var locked = tags.Attributes.ReadOnly == true || tags.Attributes.CharterReadOnly || tags.Attributes.GenericReadOnly;
                if (scanFailed) return new Facts { Hidden = true };
                owners.TryGetValue(entry, out var owner);
                if (ambiguous.Contains(entry)) return new Facts { Hidden = true };
                if (owner != null)
                {
                    var otherTag = entry.Description.Tags.FirstOrDefault(tag => tag != null && tag.GetType().Assembly == typeof(SynchronizationManager).Assembly && tag.GetType().Name == "ConfigurationManagerAttributes");
                    if (otherTag != null && Read(otherTag, "IsAdminOnly") is true)
                    {
                        var shouldManage = typeof(SynchronizationManager).GetMethod("ShouldManageConfig", Members, null, new[] { typeof(ConfigFile) }, null);
                        if (SynchronizationManager.Instance == null || shouldManage?.Invoke(SynchronizationManager.Instance, new object[] { entry.ConfigFile }) is not false) return new Facts { Hidden = true };
                    }
                    facts.Kind = owner.Conditional ? "ConditionalConfigSync" : "ServerSync"; facts.System = owner.System; facts.Data = owner.Data;
                    facts.Hidden = owner.Conditional && Read(owner.Data, "IsHidden") is not false;
                    facts.Allowed = !locked && !facts.Hidden && (!owner.Conditional || Read(owner.Data, "IsPolicyStateInitialized") is true) && Writable(owner, entry);
                    return Authority ? facts : new Facts();
                }
                var jotunnTag = entry.Description.Tags.FirstOrDefault(tag => tag != null && tag.GetType().Assembly == typeof(SynchronizationManager).Assembly && tag.GetType().Name == "ConfigurationManagerAttributes");
                if (tags.Attributes.IsAdminOnly == true)
                {
                    if (jotunnTag == null || Read(jotunnTag, "IsAdminOnly") is not true) return facts;
                    var manager = SynchronizationManager.Instance;
                    var manages = typeof(SynchronizationManager).GetMethod("ShouldManageConfig", Members, null, new[] { typeof(ConfigFile) }, null);
                    if (manager == null || manages?.Invoke(manager, new object[] { entry.ConfigFile }) is not bool managed) return facts;
                    facts.Kind = managed ? "Jotunn" : "NativeBound"; facts.System = manager; facts.Data = entry;
                    if (managed)
                    {
                        facts.Cache = typeof(SynchronizationManager).GetMethod("CacheConfigurationValues", Members, null, Type.EmptyTypes, null);
                        facts.Send = typeof(SynchronizationManager).GetMethod("SynchronizeChangedConfig", Members, null, Type.EmptyTypes, null);
                        facts.Allowed = !locked && facts.Cache != null && facts.Send != null;
                    }
                    else facts.Allowed = !locked;
                    return Authority ? facts : new Facts();
                }
                var unknownSynced = entry.Description.Tags.Any(tag => tag != null && tag.GetType().Name.StartsWith("SyncedConfigEntry", StringComparison.Ordinal));
                var managedName = System.IO.Path.GetFileName(entry.ConfigFile.ConfigFilePath).StartsWith("com.meadworks.", StringComparison.OrdinalIgnoreCase);
                if (unknownSynced || managedName) return facts;
                // The caller rediscovers the exact loaded plugin ConfigFile/entry before this native bound route executes.
                facts.Kind = "NativeBound"; facts.System = entry.ConfigFile; facts.Data = entry; facts.Allowed = !locked && Authority; return facts;
            }
            catch { return new Facts { Hidden = true }; }
        }
        static bool Same(Facts first, Facts current) => first.Kind == current.Kind && current.Allowed && !current.Hidden && ReferenceEquals(first.System, current.System) && ReferenceEquals(first.Data, current.Data);
        internal static bool Apply(ConfigEntryBase entry, Action apply, Func<bool> current)
        {
            BeginRequest(); var before = Inspect(entry); if (!before.Allowed || before.Hidden || !current()) return false;
            if (before.Kind == "Jotunn") before.Cache!.Invoke(before.System, null);
            BeginRequest();
            if (!Authority || !Same(before, Inspect(entry)) || !current()) return false;
            apply();
            BeginRequest();
            if (!Authority || !Same(before, Inspect(entry))) throw new InvalidOperationException("ConfigOwnerChangedAfterWrite");
            if (before.Kind == "Jotunn") before.Send!.Invoke(before.System, null);
            return true;
        }
    }
}
