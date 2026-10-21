// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using ExtraPotions.Meadworks.Crucible;
using CruciblePlugin = ExtraPotions.Meadworks.Crucible.Plugin;
namespace ExtraPotions.Bestiary.Runtime.Workshop;

// A product-local source facade over the two actual C01 entries; no ServerSync or second configuration manager.
internal sealed class WorkshopSyncContext : IDisposable
{
    private readonly Dictionary<string, ConfigEntry<string>> entries = new(StringComparer.Ordinal);
    private readonly List<WorkshopTextCarrier> wrappers = new();
    private bool? authority;
    internal bool IsSourceOfTruth => CruciblePlugin.IsReady && CruciblePlugin.Session.IsAuthority;
    internal event Action<bool>? SourceOfTruthChanged;
    internal WorkshopSyncContext(ConfigFile file)
    {
        foreach (string name in new[] { "YamlBundle", "ServerLocalization" })
            entries[name] = MeadworksConfig.BindHidden(file, "40 - Server Data", name, "", "Validated server workshop snapshot.", ConfigScope.ServerAuthoritative);
    }
    internal ConfigEntry<string> Entry(string key) => entries[key == "YamlBundle" ? key : "ServerLocalization"];
    internal void Track(WorkshopTextCarrier wrapper) => wrappers.Add(wrapper);
    internal void Tick()
    {
        bool now = IsSourceOfTruth;
        if (authority == now) return;
        authority = now; SourceOfTruthChanged?.Invoke(now);
    }
    internal void ClearWrappers() { foreach (var wrapper in wrappers) wrapper.Dispose(); wrappers.Clear(); }
    public void Dispose() { ClearWrappers(); SourceOfTruthChanged = null; }
}
internal sealed class WorkshopTextCarrier : IDisposable
{
    private readonly WorkshopSyncContext context;
    private readonly ConfigEntry<string> entry;
    internal event Action? ValueChanged;
    internal string Value => MeadworksConfig.Effective(entry, Plugin.Guid, ConfigScope.ServerAuthoritative, "");
    internal WorkshopTextCarrier(WorkshopSyncContext context, string key, string initial, int priority)
    {
        this.context = context; entry = context.Entry(key);
        entry.SettingChanged += Changed; context.Track(this);
    }
    private void Changed(object sender, EventArgs args) => ValueChanged?.Invoke();
    internal void AssignLocalValue(string value)
    {
        if (!context.IsSourceOfTruth || !MeadworksConfig.Amend(entry, Plugin.Guid, ConfigScope.ServerAuthoritative, value).Applied)
            throw new InvalidOperationException("Workshop publication requires the authority.");
    }
    public void Dispose() { entry.SettingChanged -= Changed; ValueChanged = null; }
}
