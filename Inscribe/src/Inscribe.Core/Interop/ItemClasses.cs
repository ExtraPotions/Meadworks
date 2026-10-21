// Inscribe - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: the capability ids and the meadworks.items.classes v1 contract (design 9.2, F3). The default class
// ids are EliteCrafting 0.7.0's economy class names (names only, config/EliteCrafting_economy.yml at ad278ecf).
using System;
using System.Collections.Generic;
using System.Linq;

namespace ExtraPotions.Inscribe.Interop
{
    /// <summary>
    /// Crucible capability ids Inscribe provides. Contracts are BCL delegate types so no product assembly references
    /// another; Crucible resolves a capability by its exact contract type.
    /// <list type="bullet">
    /// <item><see cref="ItemClasses"/> v1 (Inscribe provides, Bestiary consumes): <see cref="ItemClassesV1.ContractType"/>.</item>
    /// <item><see cref="Decoration"/> v1 (Inscribe provides, Mantle consumes): planned with R15b and Mantle MS1, not published yet.</item>
    /// </list>
    /// </summary>
    public static class CapabilityIds
    {
        public const string ItemClasses = "meadworks.items.classes";
        public const string Decoration = "meadworks.items.decoration";

        public static readonly Version Version1 = new Version(1, 0);

        public static IReadOnlyList<string> All { get; } = new[] { ItemClasses, Decoration, SocketsV1.Capability, SalvageRuneV1.Capability,
            SalvageGemsV1.Capability, ExtraPotions.Inscribe.Rarity.LegendarySources.Capability };
    }

    /// <summary>
    /// <c>meadworks.items.classes</c> v1, the shape Bestiary proposed and resolves:
    /// <c>Tuple&lt;Func&lt;string classId, string prefab, bool&gt; ClaimItems, Func&lt;string prefab, int level, bool&gt; SetItemLevel,
    /// Func&lt;string json, bool&gt; SetCreatureLoot&gt;</c>. Each call returns false for a refused entry and never throws.
    /// Differences from design 9.2 (recorded): one prefab per claim instead of <c>string[]</c>, the loot endpoint lives in
    /// this capability instead of a separate <c>meadworks.inscribe.loot</c>, and <c>RegisterItemClass</c> waits for a v2.
    /// </summary>
    public static class ItemClassesV1
    {
        public static Type ContractType { get; } = typeof(Tuple<Func<string, string, bool>, Func<string, int, bool>, Func<string, bool>>);

        public static Tuple<Func<string, string, bool>, Func<string, int, bool>, Func<string, bool>> Create(ItemRegistry registry)
        {
            if (registry == null) throw new ArgumentNullException(nameof(registry));
            return Tuple.Create<Func<string, string, bool>, Func<string, int, bool>, Func<string, bool>>(
                (cls, prefab) => Guard(() => registry.ClaimItem(cls, prefab)),
                (prefab, level) => Guard(() => registry.SetItemLevel(prefab, level)),
                json => Guard(() => registry.SetCreatureLoot(json)));
        }

        private static bool Guard(Func<bool> call)
        {
            try { return call(); }
            catch (Exception) { return false; }
        }
    }

    /// <summary>
    /// What other products registered: item class claims, item levels and creature loot profiles. These sit under the
    /// YAML layers (a server's files win, R02b). Main thread only.
    /// </summary>
    public sealed class ItemRegistry
    {
        public const int MaxLevel = 8;

        /// <summary>EliteCrafting 0.7.0's 33 default class ids in economy order, then the backpack class (#23) a pack product claims.</summary>
        public static IReadOnlyList<string> DefaultClassIds { get; } = new[]
        {
            "unused", "throwable", "tankard", "practice", "cosmetic", "helmet", "chest", "legs", "cape", "trinket", "utility", "buckler", "tower", "shield",
            "light", "tool", "fishing", "pickaxe", "crossbow", "bow", "staff_elemental", "staff_blood", "knife", "spear", "atgeir", "fist", "greatsword",
            "sword_1h", "dual_axe", "battleaxe", "axe_1h", "sledge", "mace_1h", "backpack",
        };

        private readonly HashSet<string> classIds;
        private readonly Dictionary<string, string> claims = new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly Dictionary<string, int> levels = new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly Dictionary<string, LootProfile> loot = new Dictionary<string, LootProfile>(StringComparer.Ordinal);

        public ItemRegistry(IEnumerable<string>? classIds = null)
        {
            this.classIds = new HashSet<string>(classIds ?? DefaultClassIds, StringComparer.Ordinal);
        }

        public IReadOnlyDictionary<string, LootProfile> Loot => loot;

        /// <summary>Warnings of the last <see cref="SetCreatureLoot"/> call (the plugin logs them).</summary>
        public IReadOnlyList<string> LastWarnings { get; private set; } = Array.Empty<string>();

        /// <summary>Errors of the last refused call (the plugin logs them).</summary>
        public IReadOnlyList<string> LastErrors { get; private set; } = Array.Empty<string>();

        /// <summary>Raised after any accepted change, so caches built from classes, levels and loot refresh.</summary>
        public event Action? Changed;

        public string? ClassOf(string prefab) => prefab != null && claims.TryGetValue(prefab, out var cls) ? cls : null;

        public int LevelOf(string prefab) => prefab != null && levels.TryGetValue(prefab, out var level) ? level : 0;

        public bool ClaimItem(string? classId, string? prefab)
        {
            if (string.IsNullOrEmpty(classId) || string.IsNullOrEmpty(prefab) || !classIds.Contains(classId!)) return Refuse("ClaimItems: unknown class '" + classId + "' or no prefab");
            claims[prefab!] = classId!;
            return Accept();
        }

        public bool SetItemLevel(string? prefab, int level)
        {
            if (string.IsNullOrEmpty(prefab) || level < 1 || level > MaxLevel) return Refuse("SetItemLevel: '" + prefab + "' " + level + " refused (a prefab name and a level from 1 to 8)");
            levels[prefab!] = level;
            return Accept();
        }

        public bool SetCreatureLoot(string? json)
        {
            var profile = LootProfile.Parse(json, out var errors, out var warnings);
            LastWarnings = warnings;
            if (profile == null)
            {
                LastErrors = errors;
                return false;
            }
            loot[profile.Prefab] = profile;
            return Accept();
        }

        private bool Refuse(string error)
        {
            LastErrors = new[] { error };
            return false;
        }

        private bool Accept()
        {
            LastErrors = Array.Empty<string>();
            Changed?.Invoke();
            return true;
        }
    }
}
