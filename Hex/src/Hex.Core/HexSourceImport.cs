// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Original data mapping from the user's reference configs, not third-party mod source.
using System;
using System.Collections.Generic;

namespace ExtraPotions.Hex;

public static class HexSourceImport
{
    public const string VatFile = "com.icecub.valheimadmintool.cfg";
    public const string VatPresetFile = "com.icecub.valheimadmintool.cheats.cfg";
    public const string AsFile = "liveriadevs.valheim.achievementsync.cfg";
    public static IReadOnlyDictionary<string, string> ToggleKeys { get; } = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["GodMode"] = "god_mode", ["DebugMode"] = "debug_mode", ["UnlimitedStamina"] = "unlimited_stamina",
        ["UnlimitedEitr"] = "unlimited_eitr", ["FlyMode"] = "fly", ["InfiniteJumps"] = "infinite_jumps",
        ["NoClipMode"] = "noclip", ["FreeCamMode"] = "freecam", ["GhostMode"] = "ghost_mode", ["NoCarryLimit"] = "no_carry_limit",
        ["DisableFoodDegradation"] = "disable_food_degradation", ["DisableDurabilityLoss"] = "disable_durability_loss",
        ["DisableItemLoss"] = "keep_items", ["DisableFoodLoss"] = "keep_food", ["DisableStatusLoss"] = "keep_status_effects",
        ["DisableSkillLoss"] = "keep_skills", ["MoonGravity"] = "moon_gravity", ["SuperKnockback"] = "super_knockback",
        ["BouncyMode"] = "bouncy", ["PartyHits"] = "party_hits"
    };
    public static IReadOnlyDictionary<string, string> Owners { get; } = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["NoBuildCost"] = "Timber", ["BuildEverywhere"] = "Timber", ["NoStructuralDamage"] = "Timber",
        ["MapTeleport"] = "Wayfarer", ["UnrestrictedPortals"] = "Threshold", ["ForgeAlwaysSucceed"] = "Inscribe",
        ["InstantCrafting"] = "Kindle", ["WindInBack"] = "Keel", ["ShipSpeed"] = "Keel",
        ["WardOverride"] = "Thing", ["RemoveDrops"] = "Thing"
    };
    public static IReadOnlyDictionary<string, string> ActionKeys { get; } = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["QuickHeal"] = "heal", ["Rested"] = "rested", ["ExploreMap"] = "reveal", ["RevealPoi"] = "poi",
        ["ShowWorldSeed"] = "seed", ["ResetMap"] = "reset", ["BossPowerEikthyr"] = "GP_Eikthyr",
        ["BossPowerElder"] = "GP_TheElder", ["BossPowerBonemass"] = "GP_Bonemass", ["BossPowerModer"] = "GP_Moder",
        ["BossPowerYagluth"] = "GP_Yagluth", ["BossPowerQueen"] = "GP_Queen", ["BossPowerFader"] = "GP_Ashlands"
    };
    public static IReadOnlyDictionary<string, string> AchievementKeys { get; } = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["EnableAchievements"] = "achievements_despite_cheats", ["ClearItemCheatFlags"] = "clear_inventory_marks",
        ["ClearContainerCheatFlags"] = "clear_container_marks", ["ClearWorldDropCheatFlags"] = "clear_drop_marks"
    };
    // C09 supplies parsed source ids (Section.Key), preserving sections and duplicate rows for its own validation.
    public static string? Target(string source, string sectionKey)
    {
        if (source == "vat")
        {
            if (sectionKey == "General.Toggle menu") return "menu";
            if (sectionKey.StartsWith("Hotkeys.", StringComparison.Ordinal))
            {
                if (ToggleKeys.TryGetValue(sectionKey.Substring(8), out var key)) return "hotkey:" + key;
                if (ActionKeys.TryGetValue(sectionKey.Substring(8), out var action)) return "action:" + action;
            }
        }
        if (source == "as" && sectionKey.StartsWith("General.", StringComparison.Ordinal)
            && AchievementKeys.TryGetValue(sectionKey.Substring(8), out var achievement)) return "preference:" + achievement;
        if (source == "as-guard" && (sectionKey == "General.SyncPastStats" || sectionKey == "General.ResyncKey")) return "Achievements." + sectionKey.Substring(8);
        return null;
    }
    public static string? Referral(string sectionKey)
    {
        if (sectionKey.StartsWith("Automatic resets.", StringComparison.Ordinal)) return "Delve";
        return sectionKey.StartsWith("Hotkeys.", StringComparison.Ordinal) && Owners.TryGetValue(sectionKey.Substring(8), out var owner) ? owner : null;
    }
    public static string? PresetKey(string sectionKey)
    {
        var dot = sectionKey.LastIndexOf('.');
        var sourceKey = dot >= 0 ? sectionKey.Substring(dot + 1) : sectionKey;
        // Only explicit known reference names or exact Hex identifiers are accepted; unknown state formats stay unknown.
        if (ToggleKeys.TryGetValue(sourceKey, out var key)) return key;
        foreach (var runtime in HexCatalog.RuntimeKeys) if (runtime == sourceKey) return runtime;
        return null;
    }
}
