// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Globalization;
using ExtraPotions.Thing.Communication;

namespace ExtraPotions.Thing.Commands
{
    public sealed class RegenerationRequest
    {
        public static IReadOnlyList<string> Kinds { get; } = Array.AsReadOnly(new[] { "FullReset", "Chests", "MudPiles", "FrostCaveCrystals", "BlackCores", "Morkhalla", "Hole", "AncientArmor", "Obsidian", "Flametal", "Ice", "Queen", "CampSpawners", "DungeonSpawners" });
        public string Mode { get; }
        public string Kind { get; }
        public int Offset { get; }
        public bool Enabled { get; }
        public string Version { get; }
        RegenerationRequest(string mode, string kind = "", int offset = 0, bool enabled = false, string version = "")
        { Mode = mode; Kind = kind; Offset = offset; Enabled = enabled; Version = version; }
        /// <summary>"thing regenerate nature": the Delve surface reset of trees and bushes around the admin (parity row 26).</summary>
        public const string Nature = "nature";
        public static bool KnownKind(string text)
        { foreach (var kind in Kinds) if (kind == text) return true; return false; }
        public static bool TryParse(IReadOnlyList<string> args, out RegenerationRequest request)
        {
            request = null!;
            if (args.Count == 1) { request = new RegenerationRequest("kinds"); return true; }
            if (args.Count == 3 && (args[1] == "kinds" || args[1] == "actions" || args[1] == "due" || args[1] == "nearest" || args[1] == "nature") && int.TryParse(args[2], NumberStyles.None, CultureInfo.InvariantCulture, out var offset)
                && offset >= 0 && offset < UI.ThingUiPage.MaxTotal) { request = new RegenerationRequest(args[1], offset: offset); return true; }
            if (args.Count == 5 && args[1] == "dryrun" && KnownKind(args[2]) && (args[3] == "on" || args[3] == "off") && MessageEditing.Version(args[4]))
            { request = new RegenerationRequest("dryrun", args[2], enabled: args[3] == "on", version: args[4]); return true; }
            return false;
        }
    }
}
