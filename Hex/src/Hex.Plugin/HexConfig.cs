// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using ExtraPotions.Hex.Ghost;
using ExtraPotions.Meadworks.Crucible;
namespace ExtraPotions.Hex.Runtime;

public sealed class HexConfig
{
    private readonly Dictionary<string, ConfigEntryBase> entries = new();
    private readonly Dictionary<string, object> safeValues = new();
    private readonly Dictionary<string, MischiefCategory> categories = new();
    private readonly Dictionary<string, string> cheatIds = new();
    // Tick re-reads the values only after a setting changed (file.SettingChanged), so a frame does no per-setting work.
    // nonNeutral holds the keys whose value differs from the safe one.
    private Dictionary<string, ConfigEntryBase>? preferences;
    private readonly List<string> nonNeutral = new();
    private bool rememberStale = true, neutralStale = true;
    private readonly ConfigFile file;
    private readonly Dictionary<string, ConfigEntry<KeyboardShortcut>> hotkeys = new(StringComparer.Ordinal);
    private readonly HexHotkeyValues hotkeyValues = new();
    internal readonly Dictionary<string, ConfigEntry<KeyboardShortcut>> ActionHotkeys = new(StringComparer.Ordinal);
    public ConfigEntry<bool> Enabled { get; }
    public ConfigEntry<KeyboardShortcut> PanicKey { get; }
    public ConfigEntry<KeyboardShortcut> MenuKey { get; }
    public ConfigEntry<bool> QuickActions { get; }
    public ConfigEntry<bool> DebugTools { get; }
    public ConfigEntry<bool> MapTools { get; }
    public ConfigEntry<bool> SkillEditor { get; }
    public ConfigEntry<bool> Spawning { get; }
    public ConfigEntry<int> EspDistance { get; }
    public ConfigEntry<string> EspSearch { get; }
    internal readonly Dictionary<string, ConfigEntry<bool>> EspKinds = new(StringComparer.Ordinal);
    public ConfigEntry<int> ConfirmSpawnAbove { get; }
    public ConfigEntry<bool> RestorePresetOnJoin { get; }
    /// <summary>Authority-local server setting: Ghost while vanilla clients are connected. Not a personal cheat.</summary>
    public ConfigEntry<GhostVanillaPolicy> GhostWithVanilla { get; }
    public HexConfig(ConfigFile file)
    {
        this.file = file;
        var save = file.SaveOnConfigSet; file.SaveOnConfigSet = false;
        try
        {
            Enabled = MeadworksConfig.Bind(file, "General", "Enabled", true, Localized<bool>("enabled"));
            RestorePresetOnJoin = MeadworksConfig.Bind(file, "General", "Restore Preset On Join", true, Localized<bool>("preset_restore"), ConfigScope.Local);
            SkillEditor = MeadworksConfig.Bind(file, "General", "Skill Editor", false, Localized<bool>("skill_editor"), ConfigScope.Local);
            Spawning = MeadworksConfig.Bind(file, "General", "Spawning", false, Localized<bool>("spawning"), ConfigScope.Local);
            ConfirmSpawnAbove = MeadworksConfig.Bind(file, "General", "Confirm Spawn Above", 100, Localized<int>("spawn_confirm_above", new AcceptableValueRange<int>(1, HexSpawnData.MaxAmount),
                new ConfigDependency(() => Enabled.Value && Spawning.Value, () => "Requires General: Enabled and Spawning.")), ConfigScope.Local);
            MapTools = MeadworksConfig.Bind(file, "General", "Map Tools", false, Localized<bool>("map_tools"), ConfigScope.Local);
            DebugTools = MeadworksConfig.Bind(file, "General", "Debug Tools", false, Localized<bool>("debug_tools"), ConfigScope.Local);
            QuickActions = MeadworksConfig.Bind(file, "General", "Quick Actions", false, Localized<bool>("quick_actions"));
            MenuKey = MeadworksConfig.Bind(file, "General", "Menu Key", KeyboardShortcut.Empty, Localized<KeyboardShortcut>("menu_key"), ConfigScope.Local);
            PanicKey = MeadworksConfig.Bind(file, "General", "Panic Key", KeyboardShortcut.Empty, Localized<KeyboardShortcut>("panic"));
            Toggle("unlimited_stamina"); Toggle("unlimited_eitr"); Toggle("infinite_jumps"); Toggle("moon_gravity"); Toggle("bouncy"); Toggle("super_knockback"); Toggle("party_hits"); Toggle("disable_durability_loss"); Toggle("disable_food_degradation");
            Toggle("no_carry_limit"); Bind("custom_carry_weight", 0, new AcceptableValueRange<int>(0, 99999));
            Number("walk_speed", 10, 300); Number("run_speed", 10, 300); Number("swim_speed", 10, 300); Number("fly_speed", 10, 300); Number("mounted_speed", 10, 300); Number("jump_power", 25, 500); Number("player_size", 25, 300);
            Bind("local_time_minute", -1, new AcceptableValueRange<int>(-1, 1439)); Toggle("local_time_freeze");
            Toggle("keep_items"); Toggle("keep_food"); Toggle("keep_skills"); Toggle("keep_status_effects");
            BindValue("achievements_despite_cheats", true, false, MischiefCategory.Progress);
            BindValue("clear_inventory_marks", true, false, MischiefCategory.Progress);
            BindValue("clear_container_marks", true, false, MischiefCategory.Progress);
            BindValue("clear_drop_marks", true, false, MischiefCategory.Progress);
            Toggle("debug_mode");
            BindValue("esp", false, false, MischiefCategory.Information);
            EspDistance = MeadworksConfig.Bind(file, "ESP", "Max Distance", 100, EspDescription<int>("esp_distance", new AcceptableValueRange<int>(10, 500)), ConfigScope.Local);
            EspSearch = MeadworksConfig.Bind(file, "ESP", "Search", "", EspDescription<string>("esp_search"), ConfigScope.Local);
            foreach (var kind in new[] { "players", "creatures", "drops", "resources", "pickables", "dungeons" })
                EspKinds.Add(kind, MeadworksConfig.Bind(file, "ESP", kind, true, EspDescription<bool>("esp_" + kind), ConfigScope.Local));
            Toggle("god_mode"); Toggle("ghost_mode"); Toggle("fly"); Toggle("noclip"); Toggle("freecam");
            // The authority alone reads this policy; syncing it to peers without optional Hex produces missing-config warnings.
            GhostWithVanilla = MeadworksConfig.Bind(file, "Server", "Ghost With Vanilla Clients", GhostVanillaRule.Default, Localized<GhostVanillaPolicy>(GhostNotices.SettingKey), ConfigScope.Local);
            foreach (var key in HexActionKeys.Keys) ActionHotkeys.Add(key, MeadworksConfig.Bind(file, "Action Hotkeys", key, KeyboardShortcut.Empty,
                HotkeyDescription(HexActionKeys.Label(key), key.StartsWith("GP_", StringComparison.Ordinal) ? " " + key : ""), ConfigScope.Local));
            foreach (var pair in PreferenceEntries()) hotkeys.Add(pair.Key, MeadworksConfig.Bind(file, "Hotkeys", pair.Key, KeyboardShortcut.Empty,
                HotkeyDescription("label_" + pair.Key), ConfigScope.Local));
        }
        finally { file.SaveOnConfigSet = save; }
        file.Save();
        file.SettingChanged += (_, _) => rememberStale = neutralStale = true;
        file.ConfigReloaded += (_, _) => rememberStale = neutralStale = true;
    }
    private ConfigDescription EspDescription<T>(string key, AcceptableValueBase? range = null) =>
        Localized<T>(key, range, new ConfigDependency(() => Effective("esp", false),
            () => "Requires ESP enabled and Information permission."));

    private static string? CatalogText(string key, string language)
    {
        if (!MeadworksTranslations.Registry.TryGet(HexCore.TokenNamespace, out var translations)) return null;
        var value = translations.Text(key, language);
        return string.Equals(value, key, StringComparison.Ordinal) ? null : value;
    }
    private static ConfigDescription Localized<T>(string key, AcceptableValueBase? range = null, params object[] tags)
    {
        var labelKey = key == "menu_key" ? "ui_menu_key" : key.StartsWith("esp_", StringComparison.Ordinal) ? key : "label_" + key;
        var helpKey = key == "esp_distance" ? "help_esp_distance" : key == "esp_search" ? "help_esp_search"
            : key.StartsWith("esp_", StringComparison.Ordinal) ? "help_esp_filters" : key;
        var all = new object[tags.Length + 1]; Array.Copy(tags, all, tags.Length);
        all[tags.Length] = new ConfigPresentation<T>(Plugin.Guid, (_, language) =>
            new ConfigPresentationText(label: CatalogText(labelKey, language), help: CatalogText(helpKey, language)));
        return new ConfigDescription(CatalogText(helpKey, MeadworksLanguages.English) ?? key, range, all);
    }
    private static ConfigDescription HotkeyDescription(string labelKey, string suffix = "")
    {
        string Help(string language) => string.Format(CatalogText("hotkey_description", language) ?? "{0}",
            (CatalogText(labelKey, language) ?? labelKey) + suffix);
        return new ConfigDescription(Help(MeadworksLanguages.English), null,
            new ConfigPresentation<KeyboardShortcut>(Plugin.Guid, (_, language) =>
            {
                var label = CatalogText(labelKey, language); var format = CatalogText("hotkey_description", language);
                return label == null || format == null ? null : new ConfigPresentationText(label: label + suffix,
                    help: string.Format(format, label + suffix));
            }));
    }

    // BindCheat synchronizes values to all players. Hex instead keeps personal preferences in C01 Local entries,
    // tags each with C13 and evaluates the grant at every native hook. Revocation never erases preferences.
    private ConfigEntry<T> Bind<T>(string key, T safe, AcceptableValueBase? range = null)
        => BindValue(key, safe, safe, MischiefCategory.Personal, range);
    private ConfigEntry<T> BindValue<T>(string key, T initial, T safe, MischiefCategory category, AcceptableValueBase? range = null)
    {
        var label = key switch { "unlimited_stamina" => "Unlimited Stamina", "unlimited_eitr" => "Unlimited Eitr", "infinite_jumps" => "Infinite Jumps", "walk_speed" => "Walk Speed Percent",
            "run_speed" => "Run Speed Percent", "fly_speed" => "Fly Speed Percent", "mounted_speed" => "Mounted Speed Percent", "swim_speed" => "Swim Speed Percent", "player_size" => "Player Size Percent", "party_hits" => "Party Hits", "super_knockback" => "Super Knockback", "bouncy" => "Bouncy", "jump_power" => "Jump Power Percent", "moon_gravity" => "Moon Gravity",
            "disable_durability_loss" => "Disable Durability Loss", "disable_food_degradation" => "Disable Food Degradation", "no_carry_limit" => "No Carry Limit",
            "local_time_minute" => "Local Time Minute", "local_time_freeze" => "Freeze Local Time", "custom_carry_weight" => "Custom Carry Weight", "achievements_despite_cheats" => "Achievements Despite Cheats", "clear_inventory_marks" => "Clear Inventory Cheat Marks", "clear_container_marks" => "Clear Container Cheat Marks", "clear_drop_marks" => "Clear Drop Cheat Marks", "keep_items" => "Keep Items On Death", "keep_food" => "Keep Food On Death", "keep_skills" => "Keep Skills On Death", "keep_status_effects" => "Keep Status Effects On Death", "debug_mode" => "Debug Mode", "esp" => "ESP", "god_mode" => "God Mode", "ghost_mode" => "Ghost Mode", "fly" => "Fly", "noclip" => "NoClip", "freecam" => "FreeCam", _ => key };
        var entry = MeadworksConfig.Bind(file, category == MischiefCategory.Progress ? "Achievements" : category == MischiefCategory.Information ? "Information" : "Personal", label, initial, Localized<T>(key, range), ConfigScope.Local);
        entries.Add(key, entry);
        safeValues.Add(key, safe!);
        categories.Add(key, category);
        cheatIds.Add(key, CheatId(entry));
        MeadworksMischief.Cheats.Tag(Plugin.Guid, cheatIds[key], category);
        return entry;
    }
    private void Toggle(string key) => Bind(key, false);
    private static string CheatId(ConfigEntryBase entry) => entry.Definition.Section + "." + entry.Definition.Key;
    private void Number(string key, int min, int max) => Bind(key, 100, new AcceptableValueRange<int>(min, max));
    // The set of entries is fixed once the constructor has bound them; callers only read it.
    internal Dictionary<string, ConfigEntryBase> PreferenceEntries() => preferences ??= BuildPreferenceEntries();
    private Dictionary<string, ConfigEntryBase> BuildPreferenceEntries()
    {
        var result = new Dictionary<string, ConfigEntryBase>(entries, StringComparer.Ordinal);
        result.Add("quick_actions", QuickActions); result.Add("debug_tools", DebugTools);
        result.Add("map_tools", MapTools); result.Add("skill_editor", SkillEditor);
        result.Add("spawning", Spawning);
        return result;
    }
    internal MischiefCategory? Category(string key) => categories.TryGetValue(key, out var category) ? category : (MischiefCategory?)null;
    internal object SafeValue(string key) => safeValues.TryGetValue(key, out var safe) ? safe : false;
    internal ConfigEntry<KeyboardShortcut> Hotkey(string key) => hotkeys[key];
    internal bool SetPreference(string key, object value)
    {
        if (!Plugin.Ready || !Plugin.Active || !ValidPreference(key, value)) return false;
        if (!Equals(value, SafeValue(key)) && categories.TryGetValue(key, out var category)
            && (!Enabled.Value || (!MeadworksMischief.PlayersMay(PreferenceEntries()[key]) && !MeadworksMischief.TryUse(Plugin.Guid, CheatId(PreferenceEntries()[key]), category, HexText.Text(key))))) return false;
        return MeadworksConfig.Amend(PreferenceEntries()[key], value, HexText.Text(key)).Applied;
    }
    /// <summary>Whether this player may turn the cheat on: its Mischief category allows them, or the admin lets players use it.</summary>
    internal bool MayUse(string key) => !categories.TryGetValue(key, out var category)
        || MeadworksMischief.AllowsCheat(PreferenceEntries()[key], Plugin.Guid, category);
    internal string ResetSpeeds()
    {
        if (!Plugin.Ready || !Plugin.Active || !Native.HexMovement.Owned(Player.m_localPlayer)) return HexText.Text("actions_unavailable");
        foreach (var key in HexMenu.Speeds)
            if (!SetPreference(key, SafeValue(key))) return HexText.Text("actions_unavailable");
        return HexText.Text("speeds_reset_done");
    }
    internal void PanicReset()
    {
        Native.HexSpawnProtection.Reset();
        foreach (var entry in new[] { QuickActions, DebugTools, MapTools, SkillEditor, Spawning })
            MeadworksConfig.Amend(entry, false);
        Native.HexPresets.CancelJoinRestore();
        foreach (var pair in entries) MeadworksConfig.Amend(pair.Value, safeValues[pair.Key]);
    }
    internal Dictionary<string, object> Snapshot() => System.Linq.Enumerable.ToDictionary(PreferenceEntries(), pair => pair.Key, pair => pair.Value.BoxedValue, StringComparer.Ordinal);
    internal bool ValidPreference(string key, object value)
    {
        var preferences = PreferenceEntries();
        return preferences.TryGetValue(key, out var entry) && value != null && value.GetType() == entry.SettingType
            && (entry.Description.AcceptableValues == null || entry.Description.AcceptableValues.IsValid(value));
    }
    internal void ApplyPreferences(IReadOnlyDictionary<string, object> snapshot)
    {
        foreach (var pair in snapshot) if (!ValidPreference(pair.Key, pair.Value)) throw new ArgumentException("Invalid preset preference.");
        var preferences = PreferenceEntries();
        var save = file.SaveOnConfigSet; file.SaveOnConfigSet = false;
        try { foreach (var pair in snapshot) preferences[pair.Key].BoxedValue = pair.Value; }
        finally { file.SaveOnConfigSet = save; }
        file.Save();
    }
    public T Effective<T>(string key, T safe)
    {
        if (!Plugin.Ready || !Plugin.Active || !Enabled.Value) return safe;
        return MeadworksMischief.Effective((ConfigEntry<T>)entries[key], Plugin.Guid, categories[key], safe);
    }
    public void Tick()
    {
        if (!Plugin.Active) return;
        if (MeadworksInput.Pressed(PanicKey)) { PanicReset(); return; }
        if (!Plugin.Active || !Enabled.Value) return;
        // Remember is idempotent for unchanged values, so running it only after a change remembers exactly what every frame did.
        if (rememberStale)
        {
            rememberStale = false;
            foreach (var pair in PreferenceEntries()) hotkeyValues.Remember(pair.Key, pair.Value.BoxedValue, SafeValue(pair.Key));
        }
        if (Native.HexMovement.Owned(Player.m_localPlayer) && !Player.m_localPlayer.IsDead())
            foreach (var pair in hotkeys)
            {
                if (!MeadworksInput.Pressed(pair.Value)) continue;
                if ((pair.Key.StartsWith("achievements_", StringComparison.Ordinal) || pair.Key.StartsWith("clear_", StringComparison.Ordinal)) && !Native.HexAchievements.Available) continue;
                var entry = PreferenceEntries()[pair.Key];
                if (hotkeyValues.Next(pair.Key, entry.BoxedValue, SafeValue(pair.Key), out var value)) SetPreference(pair.Key, value);
                else if (MessageHud.instance != null) MessageHud.instance.ShowMessage(MessageHud.MessageType.Center, HexText.Text("hotkey_choose"));
            }
        if (Native.HexMovement.Owned(Player.m_localPlayer))
            foreach (var pair in ActionHotkeys) if (MeadworksInput.Pressed(pair.Value)) HexActionShortcuts.Run(pair.Key);
        if (neutralStale)
        {
            neutralStale = false;
            nonNeutral.Clear();
            foreach (var pair in entries) if (!Equals(pair.Value.BoxedValue, safeValues[pair.Key])) nonNeutral.Add(pair.Key);
        }
        // Asked every frame as before: the gate's answer follows the session's authority and the server's policy, not only the values.
        foreach (var key in nonNeutral)
        {
            if (categories[key] == MischiefCategory.Progress && !Native.HexAchievements.Available) continue;
            // TryUse's two steps, so the label is looked up only for a refusal (the notice is the only reader of it).
            var status = MeadworksMischief.Query(Plugin.Guid, cheatIds[key], categories[key]);
            MeadworksBlockedOptions.Notify(status, status.Allowed ? null : HexText.Text(key));
        }
    }
}
