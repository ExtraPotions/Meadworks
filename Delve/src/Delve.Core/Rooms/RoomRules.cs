// Delve - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original, clean room: Max_Dungeon_Rooms 2.0.42 (Digitalroot, AGPL-3.0) is a behavior reference only. Its
// README ("The Algorithm": the generator places rooms up to max attempts and stops once every required room exists and
// more than min are placed), CHANGELOG (min capped at 120 in 2.0.20; Hildir's Plains fortress and unknown generators no
// longer affected in 2.0.23) and config (general pair plus Forest Crypt, Sunken Crypt and Cave overrides) were read; no
// source file was opened. Delve sets the generator's own min and max before placement and restores them after.
using System;
using System.Collections.Generic;
using System.Linq;
using ExtraPotions.Delve.Data;

namespace ExtraPotions.Delve.Rooms
{
    /// <summary>The min and max a generator places rooms with.</summary>
    public readonly struct RoomCounts : IEquatable<RoomCounts>
    {
        public RoomCounts(int min, int max)
        {
            Min = min;
            Max = max;
        }

        public int Min { get; }
        public int Max { get; }

        public bool Equals(RoomCounts other) => Min == other.Min && Max == other.Max;
        public override bool Equals(object? obj) => obj is RoomCounts other && Equals(other);
        public override int GetHashCode() => Min * 1009 + Max;
        public override string ToString() => Min + "/" + Max;
    }

    /// <summary>One per-type override from the Rooms section.</summary>
    public readonly struct RoomOverride
    {
        public RoomOverride(bool enabled, int min, int max)
        {
            Enabled = enabled;
            Min = min;
            Max = max;
        }

        public bool Enabled { get; }
        public int Min { get; }
        public int Max { get; }
    }

    /// <summary>The Rooms section as read now (design 4.2).</summary>
    public sealed class RoomSettings
    {
        public RoomSettings(bool enabled, int min, int max, RoomOverride forestCrypt, RoomOverride sunkenCrypt, RoomOverride cave)
        {
            Enabled = enabled;
            Min = min;
            Max = max;
            ForestCrypt = forestCrypt;
            SunkenCrypt = sunkenCrypt;
            Cave = cave;
        }

        public bool Enabled { get; }
        public int Min { get; }
        public int Max { get; }
        public RoomOverride ForestCrypt { get; }
        public RoomOverride SunkenCrypt { get; }
        public RoomOverride Cave { get; }
    }

    public static class RoomRules
    {
        public const int MinFloor = 10;
        public const int MinCap = 120;
        public const int MaxCap = 1000;

        public const string ForestCrypt = "DG_ForestCrypt";
        public const string SunkenCrypt = "DG_SunkenCrypt";
        public const string Cave = "DG_Cave";

        /// <summary>The generators the Rooms section configures (Max_Dungeon_Rooms' three types).</summary>
        public static IReadOnlyList<string> ConfigGenerators { get; } = Array.AsReadOnly(new[] { ForestCrypt, SunkenCrypt, Cave });

        /// <summary>
        /// The vanilla dungeon generators the general pair sizes (user decision D3, 2026-10-08): every generator prefab of
        /// Valheim 1.0.17 except Hildir's three, as MoreDungeonResets lists them in Solo's config. The plugin still sizes
        /// only generators with the Dungeon algorithm, so the camp and village generators here are left alone at run time.
        /// </summary>
        public static IReadOnlyList<string> VanillaGenerators { get; } = Array.AsReadOnly(new[]
        {
            "DG_AshlandRuins", "DG_Cave", "DG_DvergrBoss", "DG_DvergrTown", "DG_ForestCrypt", "DG_FortressRuins", "DG_GoblinCamp",
            "DG_HalfBurried_ForestCrypt", "DG_Hole", "DG_MeadowsFarm", "DG_MeadowsVillage", "DG_MorkHalla", "DG_NorthVillage", "DG_SunkenCrypt",
        });

        /// <summary>Never changed, listed or not (design A3): Hildir's dungeons, the Plains fortress among them.</summary>
        public static bool Protected(string generator) =>
            generator != null && generator.StartsWith("DG_Hildir_", StringComparison.Ordinal);

        /// <summary>Min within 10..120 and max within 10..1000, never below min.</summary>
        public static RoomCounts Clamp(int min, int max)
        {
            var lo = Math.Min(MinCap, Math.Max(MinFloor, min));
            var hi = Math.Min(MaxCap, Math.Max(MinFloor, max));
            return new RoomCounts(lo, Math.Max(lo, hi));
        }

        /// <summary>
        /// The counts Delve gives a generator, or null when it stays as the game made it. Forest Crypt, Sunken Crypt and Cave
        /// use their Rooms section override when it is on; any other generator listed in <c>Delve.Rooms.yml</c> uses its
        /// entry; every other vanilla generator uses the general pair; Hildir's and unlisted modded generators are untouched.
        /// </summary>
        public static RoomCounts? Effective(string generator, RoomSettings settings, RoomsFile file)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            if (file == null) throw new ArgumentNullException(nameof(file));
            if (!settings.Enabled || string.IsNullOrEmpty(generator) || Protected(generator)) return null;
            switch (generator)
            {
                case ForestCrypt: return Pick(settings.ForestCrypt, settings);
                case SunkenCrypt: return Pick(settings.SunkenCrypt, settings);
                case Cave: return Pick(settings.Cave, settings);
            }
            if (file.Overrides.TryGetValue(generator, out var counts)) return Clamp(counts.Min, counts.Max);
            return VanillaGenerators.Contains(generator) ? Clamp(settings.Min, settings.Max) : (RoomCounts?)null;
        }

        /// <summary>Generators listed in the file that the game does not know (reported once, then ignored).</summary>
        public static IReadOnlyList<string> Unknown(RoomsFile file, IEnumerable<string> knownGenerators)
        {
            var known = new HashSet<string>(knownGenerators ?? Enumerable.Empty<string>(), StringComparer.Ordinal);
            return file.Overrides.Keys.Where(g => !known.Contains(g)).OrderBy(g => g, StringComparer.Ordinal).ToArray();
        }

        private static RoomCounts Pick(RoomOverride o, RoomSettings s) => o.Enabled ? Clamp(o.Min, o.Max) : Clamp(s.Min, s.Max);
    }

    /// <summary><c>Delve.Rooms.yml</c>: <c>overrides:</c> per generator prefab, <c>DG_MorkHalla: {min: 15, max: 30}</c>.</summary>
    public sealed class RoomsFile
    {
        private RoomsFile(IReadOnlyDictionary<string, RoomCounts> overrides, IReadOnlyList<string> errors)
        {
            Overrides = overrides;
            Errors = errors;
        }

        public static RoomsFile Empty { get; } = new RoomsFile(new Dictionary<string, RoomCounts>(), Array.Empty<string>());

        /// <summary>Entries as written (not clamped; <see cref="RoomRules.Effective"/> clamps).</summary>
        public IReadOnlyDictionary<string, RoomCounts> Overrides { get; }

        /// <summary>Lines skipped and why.</summary>
        public IReadOnlyList<string> Errors { get; }

        public static RoomsFile Parse(string? text)
        {
            var errors = new List<string>();
            var overrides = new Dictionary<string, RoomCounts>(StringComparer.Ordinal);
            var sections = MiniYaml.Read(text, errors);
            if (sections.TryGetValue("overrides", out var entries))
            {
                foreach (var e in entries)
                {
                    if (e.Map == null || !e.Map.TryGetValue("min", out var minText) || !e.Map.TryGetValue("max", out var maxText)
                        || !int.TryParse(minText, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var min)
                        || !int.TryParse(maxText, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var max))
                    {
                        errors.Add("line " + e.Line + ": '" + e.Name + "' needs {min: <whole number>, max: <whole number>}");
                        continue;
                    }
                    if (RoomRules.Protected(e.Name))
                    {
                        errors.Add("line " + e.Line + ": '" + e.Name + "' is never changed (Hildir's dungeons keep their size)");
                        continue;
                    }
                    if (RoomRules.ConfigGenerators.Contains(e.Name))
                    {
                        errors.Add("line " + e.Line + ": '" + e.Name + "' is set in the Rooms section of the config; this entry is ignored");
                        continue;
                    }
                    overrides[e.Name] = new RoomCounts(min, max);
                }
            }
            return new RoomsFile(overrides, errors);
        }
    }
}
