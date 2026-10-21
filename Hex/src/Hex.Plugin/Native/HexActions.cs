// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Original native API implementation. VAT is a behavior only design reference; no source or assemblies accessed.
using System;
using System.Linq;
using ExtraPotions.Meadworks.Crucible;

namespace ExtraPotions.Hex.Runtime.Native;

internal static class HexActions
{
    private static Terminal.ConsoleCommand? command;
    private static System.Collections.Generic.IReadOnlyList<string> powers => HexActionKeys.Powers;

    internal static void Initialize()
    {
        MeadworksMischief.Cheats.Tag(Plugin.Guid, "Actions.Heal", MischiefCategory.Personal);
        MeadworksMischief.Cheats.Tag(Plugin.Guid, "Actions.Rested", MischiefCategory.Personal);
        MeadworksMischief.Cheats.Tag(Plugin.Guid, "Actions.BossPower", MischiefCategory.Progress);
        if (Terminal.commands.ContainsKey("hex")) return;
        command = new Terminal.ConsoleCommand("hex", HexText.Text("actions_help"),
            (Terminal.ConsoleEventArgs args) => args.Context?.AddString(Execute(args.Args.Skip(1).ToArray())));
    }

    internal static void Shutdown()
    {
        if (command != null && Terminal.commands.TryGetValue("hex", out var current) && ReferenceEquals(current, command))
            Terminal.commands.Remove("hex");
        command = null;
    }

    // Shared by the console and the future kit action buttons. The action rechecks ownership and its own category.
    internal static string Execute(string[] args)
    {
        if (args.Length == 0 || args[0].Equals("help", StringComparison.OrdinalIgnoreCase)) return HexText.Text("actions_help");
        if (args[0].Equals("debug", StringComparison.OrdinalIgnoreCase))
            return args.Length == 2 ? HexDebug.Execute(args[1].ToLowerInvariant())
                : args.Length == 3 && args[2].Equals("confirm", StringComparison.OrdinalIgnoreCase) ? HexDebug.Execute(args[1].ToLowerInvariant(), true) : HexText.Text("debug_help");
        if (args[0].Equals("spawn", StringComparison.OrdinalIgnoreCase)) return HexSpawn.Execute(args);
        if (args[0].Equals("protection", StringComparison.OrdinalIgnoreCase))
            return args.Length == 1 ? HexSpawnProtection.Toggle() : HexText.Text("actions_help");
        if (args[0].Equals("seed", StringComparison.OrdinalIgnoreCase))
            return args.Length == 1 ? HexInformation.Seed() : HexText.Text("actions_help");
        if (args[0].Equals("map", StringComparison.OrdinalIgnoreCase)) return HexMap.Execute(args);
        if (args[0].Equals("poi", StringComparison.OrdinalIgnoreCase))
            return args.Length == 2 && args[1].Equals("actual", StringComparison.OrdinalIgnoreCase) ? HexPoi.Request()
                : args.Length == 2 && args[1].Equals("estimate", StringComparison.OrdinalIgnoreCase) ? HexPoi.RequestEstimate()
                : args.Length == 2 && args[1].Equals("clear", StringComparison.OrdinalIgnoreCase) ? HexPoi.Clear() : HexText.Text("poi_help");
        if (args[0].Equals("skills", StringComparison.OrdinalIgnoreCase))
            return args.Length == 1 ? HexSkills.List() : HexText.Text("skills_help");
        if (args[0].Equals("skill", StringComparison.OrdinalIgnoreCase))
            return args.Length == 3 && float.TryParse(args[2], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var level)
                ? HexSkills.Set(args[1], level) : HexText.Text("skills_help");
        if (args[0].Equals("preset", StringComparison.OrdinalIgnoreCase)) return HexPresets.Execute(args);
        var player = Player.m_localPlayer;
        if (!Plugin.Ready || !Plugin.Active || Plugin.Settings?.Enabled.Value != true
            || Plugin.Settings.QuickActions.Value != true || !HexMovement.Owned(player)) return HexText.Text("actions_unavailable");
        var verb = args[0].ToLowerInvariant();
        var category = verb == "power" ? MischiefCategory.Progress : MischiefCategory.Personal;
        var id = verb switch { "heal" => "Actions.Heal", "rested" => "Actions.Rested", "power" => "Actions.BossPower", _ => "" };
        if (id.Length == 0 || (verb == "power" ? args.Length != 2 : args.Length != 1)) return HexText.Text("actions_help");
        if (!MeadworksMischief.TryUse(Plugin.Guid, id, category, HexText.Text("actions_" + verb))) return HexText.Text("actions_denied");
        if (verb == "heal") player.Heal(player.GetMaxHealth());
        else if (verb == "rested")
        {
            var manager = player.GetSEMan();
            var effect = manager.GetStatusEffect(SEMan.s_statusEffectRested)
                ?? manager.AddStatusEffect(SEMan.s_statusEffectRested);
            if (effect == null) return HexText.Text("actions_unavailable");
            effect.m_time = 0f;
            effect.m_ttl = 3600f;
        }
        else
        {
            var name = powers.FirstOrDefault(power => power.Equals(args[1], StringComparison.OrdinalIgnoreCase));
            if (name == null || ObjectDB.instance == null) return HexText.Text("actions_power_unknown");
            var effect = ObjectDB.instance.GetStatusEffect(name.GetStableHashCode());
            if (effect == null || player.GetSEMan().AddStatusEffect(effect, resetTime: true) == null
                && !player.GetSEMan().HaveStatusEffect(effect.NameHash())) return HexText.Text("actions_unavailable");
        }
        return HexText.Text("actions_done");
    }
}
