// Shepherd - ExtraPotions Meadworks animal husbandry.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
using System.Collections.Generic;

namespace ExtraPotions.Shepherd.Troughs
{
    /// <summary>One trough piece (design 11.1): built at runtime from a vanilla prefab, never shipped art.</summary>
    public sealed class TroughPiece
    {
        public TroughPiece(string prefab, string nameKey, string displayName, string description, string sourcePrefab, string? meshPrefab,
            int width, int height, string station, string defaultRecipe)
        {
            Prefab = prefab; NameKey = nameKey; DisplayName = displayName; Description = description; SourcePrefab = sourcePrefab;
            MeshPrefab = meshPrefab; Width = width; Height = height; Station = station; DefaultRecipe = defaultRecipe;
        }

        public string Prefab { get; }
        /// <summary>English name (log text and the English catalog value); the piece shows <see cref="NameToken"/>.</summary>
        public string DisplayName { get; }
        public string Description { get; }
        /// <summary>Vanilla container prefab cloned for the piece, container and wear behavior.</summary>
        public string SourcePrefab { get; }
        /// <summary>Vanilla piece whose meshes replace the source visuals (low troughs); null keeps the source look.</summary>
        public string? MeshPrefab { get; }
        public int Width { get; }
        public int Height { get; }
        public string Station { get; }
        public string DefaultRecipe { get; }
        /// <summary>C05 key of the piece and container name (<see cref="ShepherdText"/>).</summary>
        public string NameKey { get; }
        /// <summary>C05 key of the piece description, shared by every trough.</summary>
        public string DescriptionKey => ShepherdText.PieceTroughDescription;
        public string NameToken => ShepherdText.Token(NameKey);
        public string DescriptionToken => ShepherdText.Token(DescriptionKey);
    }

    /// <summary>The three trough pieces and the GrindstoneSkills Animal Feeder alias.</summary>
    public static class TroughCatalog
    {
        public const string Workbench = "piece_workbench";
        public const string Stonecutter = "piece_stonecutter";
        private const string Feeds = "Hungry tamed animals, and animals being tamed, come to eat the food you put in it.";

        private static readonly TroughPiece[] All =
        {
            new TroughPiece(TroughPrefabs.FeedBarrel, ShepherdText.PieceFeedBarrel, "Feed Barrel", Feeds, "piece_chest_barrel", null, 4, 2, Workbench, "Wood:10, LeatherScraps:4"),
            new TroughPiece(TroughPrefabs.PlankTrough, ShepherdText.PiecePlankTrough, "Plank Trough", Feeds, "piece_chest_wood", "wood_beam_1", 6, 1, Workbench, "Wood:6, Resin:2"),
            new TroughPiece(TroughPrefabs.StoneTrough, ShepherdText.PieceStoneTrough, "Stone Trough", Feeds, "piece_chest_wood", "stone_wall_1x1", 6, 1, Stonecutter, "Stone:12, Wood:2"),
        };

        public static IReadOnlyList<TroughPiece> Pieces => All;

        public static TroughPiece? Find(string? prefab)
        {
            var name = Canonical(prefab);
            foreach (var piece in All) if (piece.Prefab == name) return piece;
            return null;
        }

        /// <summary>The Shepherd prefab a trough name stands for: <c>grindstone_feeder</c> loads as the Feed Barrel.</summary>
        public static string? Canonical(string? prefab)
        {
            if (prefab == TroughPrefabs.GrindstoneFeeder) return TroughPrefabs.FeedBarrel;
            foreach (var piece in All) if (piece.Prefab == prefab) return prefab;
            return null;
        }
    }

    /// <summary>
    /// Which vanilla container a trough is cloned from. Saved troughs only load while their prefab is registered, so
    /// when the catalog source is missing or another mod changed it into something unusable, the next vanilla chest
    /// is tried; null means none is usable and the plugin builds a minimal container itself. The trough keeps its own
    /// prefab name either way.
    /// </summary>
    public static class TroughSource
    {
        /// <summary>Vanilla 1.0.20 container pieces, tried in this order after the catalog source.</summary>
        public static readonly IReadOnlyList<string> Fallbacks = new[]
        {
            "piece_chest_wood", "piece_chest_barrel", "piece_chest", "piece_chest_private", "piece_chest_blackmetal",
        };

        public static IEnumerable<string> Candidates(string? preferred)
        {
            if (!string.IsNullOrEmpty(preferred)) yield return preferred!;
            foreach (var name in Fallbacks) if (name != preferred) yield return name;
        }

        /// <summary>The first usable candidate, or null for the self-made container.</summary>
        public static string? Choose(string? preferred, System.Func<string, bool> usable)
        {
            foreach (var name in Candidates(preferred)) if (usable(name)) return name;
            return null;
        }
    }

    /// <summary>Design 4.3 <c>Build Level</c> (Q6 default 0): who may build troughs.</summary>
    public static class TroughGate
    {
        public const double HiddenAbove = 100;

        /// <summary>0 or less lets everyone (Husbandry on or off); above 100 hides troughs; otherwise the keeper level.</summary>
        public static bool MayBuild(double buildLevel, double keeperLevel, bool husbandryActive)
        {
            if (buildLevel > HiddenAbove) return false;
            if (buildLevel <= 0) return true;
            return husbandryActive && keeperLevel >= buildLevel;
        }
    }

    /// <summary>Troughs hold only food (design 11.1): items some loaded creature eats.</summary>
    public static class TroughFilter
    {
        public static bool Accepts(string? itemPrefab, ISet<string> edible) =>
            !string.IsNullOrEmpty(itemPrefab) && edible != null && edible.Contains(itemPrefab!);
    }
}
