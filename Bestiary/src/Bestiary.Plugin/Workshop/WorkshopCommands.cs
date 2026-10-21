// Reference export dispatch adapted from CreatureManager 1.2.5 CreatureConsoleCommands at 47e45a6e8c3b389a51e42fc7387d1a4fb90af473.
// Portions Copyright (C) sighsorry1029. Changed by expDARE, 2026: owned names and authority gate. SPDX-License-Identifier: GPL-3.0-only
using System;
using System.Collections;
using System.Collections.Generic;
using ExtraPotions.Bestiary.Runtime.Workshop.Adopted;
using HarmonyLib;
namespace ExtraPotions.Bestiary.Runtime.Workshop;

internal static class WorkshopCommands
{
    private static readonly Dictionary<string, Terminal.ConsoleCommand> owned = new();
    private static readonly List<string> scopes = new() { "creature", "ai", "attack", "loadout", "projectile", "texture" };
    private static IDictionary? Commands => AccessTools.Field(typeof(Terminal), "commands")?.GetValue(null) as IDictionary;
    internal static void Register()
    {
        if (owned.Count != 0) return;
        Add("bestiary:reference", "Write workshop references: creature|ai|attack|loadout|projectile|texture", Reference, () => scopes);
        Add("bestiary:full", "Write the full creature scaffold: bestiary:full creature", Full, () => new List<string> { "creature" });
        Add("bestiary:reload", "Reload validated workshop YAML and localization on the authority.", Reload, () => new());
    }
    private static void Add(string name, string help, Terminal.ConsoleEvent run, Terminal.ConsoleOptionsFetcher options)
    {
        if (Commands?.Contains(name) == true) return;
        owned[name] = new Terminal.ConsoleCommand(name, help, run, optionsFetcher: options);
    }
    private static bool Allowed(Terminal.ConsoleEventArgs args)
    {
        if (WorkshopHost.Active && ExtraPotions.Meadworks.Crucible.Plugin.Session.IsAuthority) return true;
        args.Context?.AddString("Workshop changes require the active server or single-player authority."); return false;
    }
    private static void Reference(Terminal.ConsoleEventArgs args)
    {
        if (!Allowed(args)) return;
        string scope = args.Length == 2 ? args[1].ToLowerInvariant() : "";
        if (!scopes.Contains(scope)) { args.Context?.AddString("Syntax: bestiary:reference creature|ai|attack|loadout|projectile|texture"); return; }
        CreatureAssetOwnerCatalog.RefreshMappings(); string path, error;
        bool wrote = scope switch
        {
            "ai" => CreatureDomainManager.TryWriteAiReferenceConfigurationFile(out path, out error),
            "attack" => CreatureDomainManager.TryWriteAttackReferenceConfigurationFile(out path, out error),
            "loadout" => CreatureDomainManager.TryWriteCreatureLoadoutReferenceConfigurationFile(out path, out error),
            "projectile" => CreatureDomainManager.TryWriteProjectileReferenceConfigurationFile(out path, out error),
            "texture" => CreatureDomainManager.TryWriteTextureReferenceConfigurationFile(out path, out error),
            _ => CreatureDomainManager.TryWriteReferenceConfigurationFile(out path, out error)
        };
        args.Context?.AddString(wrote ? "Wrote " + scope + " reference to " + path : error);
    }
    private static void Full(Terminal.ConsoleEventArgs args)
    {
        if (!Allowed(args)) return;
        if (args.Length != 2 || args[1] != "creature") { args.Context?.AddString("Syntax: bestiary:full creature"); return; }
        CreatureAssetOwnerCatalog.RefreshMappings();
        bool wrote = CreatureDomainManager.TryWriteFullScaffoldConfigurationFile(out string path, out string error);
        args.Context?.AddString(wrote ? "Wrote creature scaffold to " + path : error);
    }
    private static void Reload(Terminal.ConsoleEventArgs args)
    {
        if (!Allowed(args)) return;
        CreatureDomainManager.RequestConfigurationReload(); CreatureServerLocalization.ReloadFromDiskAndSync();
        args.Context?.AddString("Workshop reload requested; invalid data preserves the last complete configuration.");
    }
    internal static void Stop()
    {
        var commands = Commands;
        foreach (var entry in owned) if (commands?.Contains(entry.Key) == true && ReferenceEquals(commands[entry.Key], entry.Value)) commands.Remove(entry.Key);
        owned.Clear();
    }
}
