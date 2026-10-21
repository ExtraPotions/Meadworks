// Almanac - ExtraPotions Meadworks settings book.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Linq;
using ExtraPotions.Almanac.Values;

namespace ExtraPotions.Almanac.Coexistence
{
    /// <summary>Compatibility.Coexistence setting (design 16, Q1).</summary>
    public enum CoexistenceMode { Auto, AlwaysOwnKey }

    /// <summary>A config manager Almanac knows by GUID, and where its toggle shortcut lives. Almanac only reads it.</summary>
    public sealed class KnownManager
    {
        public KnownManager(string guid, string name, string toggleSection, string toggleKey)
        {
            Guid = guid;
            Name = name;
            ToggleSection = toggleSection;
            ToggleKey = toggleKey;
        }

        public string Guid { get; }
        public string Name { get; }
        public string ToggleSection { get; }
        public string ToggleKey { get; }
    }

    public static class KnownManagers
    {
        /// <summary>BepInEx.ConfigurationManager, shudnal's Valheim fork and Vapok ConfigDrawers (toggle settings as in the Solo profile files).</summary>
        public static readonly IReadOnlyList<KnownManager> All = new[]
        {
            new KnownManager("com.bepis.bepinex.configurationmanager", "Configuration Manager", "General", "Show config manager"),
            new KnownManager("_shudnal.ConfigurationManager", "Valheim Configuration Manager", "General", "Show config manager"),
            new KnownManager("vapok.bepinex.configdrawers", "ConfigDrawers", "General", "Toggle Keybind"),
        };
    }

    /// <summary>An installed manager and its current toggle chord as BepInEx text; null when it could not be read.</summary>
    public sealed class ManagerFacts
    {
        public ManagerFacts(string guid, string name, string? toggleChord)
        {
            Guid = guid;
            Name = name;
            ToggleChord = toggleChord;
        }

        public string Guid { get; }
        public string Name { get; }
        public string? ToggleChord { get; }
    }

    public sealed class HotkeyDecision
    {
        internal HotkeyDecision(string chord, bool usedFallback, string? noticeManager, string? collisionWith)
        {
            Chord = chord;
            UsedFallback = usedFallback;
            NoticeManager = noticeManager;
            CollisionWith = collisionWith;
        }

        /// <summary>Chord to listen for, as BepInEx <c>KeyboardShortcut</c> text.</summary>
        public string Chord { get; }
        public bool UsedFallback { get; }
        /// <summary>Manager whose F1 made Almanac use its fallback; the plugin shows the notice once per session.</summary>
        public string? NoticeManager { get; }
        /// <summary>Manager that shares the chord Almanac kept (explicit key or AlwaysOwnKey); a warning only.</summary>
        public string? CollisionWith { get; }

        public string? NoticeText => NoticeManager == null ? null
            : "F1 opens " + NoticeManager + ". Almanac opens with Left Alt + F1 (change it in Almanac).";
    }

    /// <summary>
    /// Almanac may remain unbound. With an F1 key, it takes F1 only when no installed manager's toggle is F1 (an unreadable
    /// toggle counts as that manager's default F1), else Left Alt + F1 with one notice. An explicit player key, or
    /// AlwaysOwnKey, is kept and a real collision only warns.
    /// </summary>
    public static class HotkeyPolicy
    {
        public const string DefaultChord = "F1";
        public const string FallbackChord = "F1 + LeftAlt";

        public static HotkeyDecision Decide(string? configuredChord, CoexistenceMode mode, IEnumerable<ManagerFacts> installed)
        {
            var managers = (installed ?? Enumerable.Empty<ManagerFacts>()).ToList();
            var defaultChord = ShortcutValue.Parse(DefaultChord);
            if (!ShortcutValue.TryParse(configuredChord, null, out var configured, out _)) configured = defaultChord;
            if (configured.IsEmpty) return new HotkeyDecision("None", false, null, null);

            var explicitKey = !configured.Equals(defaultChord);
            if (explicitKey || mode == CoexistenceMode.AlwaysOwnKey)
                return new HotkeyDecision(configured.ToString(), false, null, Owner(managers, configured)?.Name);

            var owner = Owner(managers, defaultChord);
            if (owner == null) return new HotkeyDecision(defaultChord.ToString(), false, null, null);
            var fallback = ShortcutValue.Parse(FallbackChord);
            return new HotkeyDecision(fallback.ToString(), true, owner.Name, Owner(managers, fallback)?.Name);
        }

        private static ManagerFacts? Owner(IEnumerable<ManagerFacts> managers, ShortcutValue chord)
            => managers.FirstOrDefault(m => Toggle(m).Equals(chord));

        private static ShortcutValue Toggle(ManagerFacts manager)
            => ShortcutValue.TryParse(manager.ToggleChord ?? DefaultChord, null, out var value, out _) ? value : ShortcutValue.Parse(DefaultChord);
    }
}
