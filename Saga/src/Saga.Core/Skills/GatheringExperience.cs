// Saga - ExtraPotions Meadworks progression.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks).
//
// Portions Copyright (C) 2026 Gerald Glasgow, GrindstoneSkills 0.14.1, GPL-3.0-or-later.
// Adapted from ValheimMods at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1:
// GrindstoneSkills/GrindstoneSkills/Woodcutting/Experience/WoodXp.cs, Pickaxes/Experience/MineXp.cs,
// Foraging/Experience/ForageXp.cs, Fishing/Experience/FishXp.cs.
// ValheimMods - Valheim BepInEx mods and shared libraries. Copyright (C) 2026 Gerald Glasgow. This program is free
// software: you can redistribute it and/or modify it under the terms of the GNU General Public License as published
// by the Free Software Foundation, either version 3 of the License, or (at your option) any later version. See
// THIRD_PARTY_NOTICES/ValheimMods-GPL-3.0-or-later.txt and licenses/GPL-3.0.txt.
//
// Changed by expDARE, 2026: namespace Saga.Skills; Unity-free; the experience formulas only, as pure functions of a
// settings snapshot (defaults from Saga's settings table) and the facts the plugin reads (tool tier, wood health,
// biome, discovery, species scale and fish level); a portable Biome enum replaces Heightmap.Biome; credits are
// returned instead of raised, so the plugin raises them through the CS1 experience scope.
// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using ExtraPotions.Saga.Config;

namespace ExtraPotions.Saga.Skills
{
    /// <summary>The game's biomes (Heightmap.Biome names) as far as the experience steps need them.</summary>
    public enum Biome { None, Meadows, BlackForest, Swamp, Mountain, Plains, Mistlands, AshLands, DeepNorth, Ocean }

    internal static class Pct
    {
        public static float Of(float value) => Math.Max(0f, value) / 100f;
    }

    public sealed class WoodValues
    {
        public float Multiplier, TierExperience, SmallWoodHealth, SmallWoodExperience, Fell, Split, Discovery;
        public bool TierScaling;

        public static WoodValues Defaults() => new WoodValues
        {
            Multiplier = SagaSettings.Woodcutting.ExperienceMultiplier.Default,
            TierScaling = SagaSettings.Woodcutting.TierScaling.Default,
            TierExperience = SagaSettings.Woodcutting.TierExperience.Default,
            SmallWoodHealth = SagaSettings.Woodcutting.SmallWoodHealth.Default,
            SmallWoodExperience = SagaSettings.Woodcutting.SmallWoodExperience.Default,
            Fell = SagaSettings.Woodcutting.FellExperience.Default,
            Split = SagaSettings.Woodcutting.SplitExperience.Default,
            Discovery = SagaSettings.Woodcutting.DiscoveryMultiplier.Default,
        };
    }

    /// <summary>Woodcutting experience on top of the game's own (WoodXp).</summary>
    public static class WoodExperience
    {
        /// <summary>1 + Tier Experience Per Tool Tier per tier the wood needs (birch and oak 2, Yggdrasil 4); 1 while Tier Scaling is off.</summary>
        public static float TierFactor(WoodValues v, int tier) => v.TierScaling ? 1f + Pct.Of(v.TierExperience) * Math.Max(0, tier) : 1f;

        /// <summary>
        /// The factor of the game's swing raise for the hardest wood the swing hit: the multiplier, times the tier factor
        /// when the axe can cut it (a swing at wood too hard gets the game's amount only), times the small-wood share.
        /// </summary>
        public static float SwingScale(WoodValues v, int tier, bool canCut, float health)
        {
            var scale = Math.Max(0f, v.Multiplier) * (canCut ? TierFactor(v, tier) : 1f);
            return health < v.SmallWoodHealth ? scale * Math.Min(1f, Pct.Of(v.SmallWoodExperience)) : scale;
        }

        /// <summary>The credit a tree's owner sends for a fell.</summary>
        public static float FellAmount(WoodValues v, int tier) => Math.Max(0f, v.Fell) * TierFactor(v, tier);

        /// <summary>The credit a log's owner sends for a break; only a break that drops wood earns it.</summary>
        public static float SplitAmount(WoodValues v, int tier, bool dropsWood) => dropsWood ? Math.Max(0f, v.Split) * TierFactor(v, tier) : 0f;

        /// <summary>On the woodcutter's client: amount times the multiplier, times Discovery Multiplier for the first fell of a kind of tree.</summary>
        public static float Credit(WoodValues v, float amount, bool fell, bool firstOfKind)
        {
            var total = Math.Max(0f, amount) * Math.Max(0f, v.Multiplier);
            return total > 0f && fell && firstOfKind ? total * Math.Max(1f, v.Discovery) : total;
        }
    }

    public sealed class MineValues
    {
        public float Multiplier, BiomeStep, OreBonus, CleanStrikeExperience, DiscoveryExperience;

        public static MineValues Defaults() => new MineValues
        {
            Multiplier = SagaSettings.Mining.ExperienceMultiplier.Default,
            BiomeStep = SagaSettings.Mining.BiomeStep.Default,
            OreBonus = SagaSettings.Mining.OreBonus.Default,
            CleanStrikeExperience = SagaSettings.Mining.CleanStrikeExperience.Default,
            DiscoveryExperience = SagaSettings.Mining.DiscoveryExperience.Default,
        };
    }

    /// <summary>Mining experience on top of the game's own (MineXp).</summary>
    public static class MineExperience
    {
        /// <summary>
        /// The biome's step in the game's progression: Meadows 0, Black Forest 1, Swamp 2, Mountain 3, Plains 4, Mistlands 5,
        /// Ashlands and Deep North 6; the Ocean, no biome and any other count as Meadows.
        /// </summary>
        public static int BiomeStep(Biome biome)
        {
            switch (biome)
            {
                case Biome.BlackForest: return 1;
                case Biome.Swamp: return 2;
                case Biome.Mountain: return 3;
                case Biome.Plains: return 4;
                case Biome.Mistlands: return 5;
                case Biome.AshLands:
                case Biome.DeepNorth: return 6;
                default: return 0;
            }
        }

        /// <summary>A rock's scale before the multiplier: 1 + step bonus, times 1 + Ore Experience Bonus for ore; 1 when the pickaxe cannot mine it.</summary>
        public static float Scale(MineValues v, Biome biome, bool ore, bool canMine)
        {
            if (!canMine) return 1f;
            var scale = 1f + Pct.Of(v.BiomeStep) * BiomeStep(biome);
            return ore ? scale * (1f + Pct.Of(v.OreBonus)) : scale;
        }

        /// <summary>A credit (clean strike or discovery) of <paramref name="setting"/> for a rock of this scale.</summary>
        public static float Credit(float setting, MineValues v, float scale) => Math.Max(0f, setting) * scale * Math.Max(0f, v.Multiplier);
    }

    public sealed class ForageValues
    {
        public float PerPick, BiomeStep, Discovery, Planted;

        public static ForageValues Defaults() => new ForageValues
        {
            PerPick = SagaSettings.Foraging.ExperiencePerPick.Default,
            BiomeStep = SagaSettings.Foraging.BiomeStep.Default,
            Discovery = SagaSettings.Foraging.DiscoveryMultiplier.Default,
            Planted = SagaSettings.Foraging.PlantedExperience.Default,
        };
    }

    /// <summary>Foraging experience (ForageXp): the factor of the game's pick raise, sent to Foraging instead of Farming.</summary>
    public static class ForageExperience
    {
        public static float Scale(ForageValues v, float itemFactor, Biome biome, bool firstOfKind, bool planted = false)
        {
            var scale = PlantedShare(v.Planted, planted) * Math.Max(0f, v.PerPick) * Math.Max(0f, itemFactor) * (1f + Pct.Of(v.BiomeStep) * MineExperience.BiomeStep(biome));
            return scale > 0f && firstOfKind ? scale * Math.Max(1f, v.Discovery) : scale;
        }

        /// <summary>
        /// The share of the experience a pick earns: 1 for a wild plant, Planted Pickable Foraging XP (0 to 100 percent)
        /// for one a player planted, so planting pickables next to each other is not a fast way to level.
        /// </summary>
        public static float PlantedShare(float plantedPercent, bool planted) =>
            planted ? Math.Min(1f, Math.Max(0f, Pct.Of(plantedPercent))) : 1f;
    }

    public sealed class FishValues
    {
        public float Multiplier, EmptyReel, FightReel, Catch, SizeBonus, Discovery, NewSize;

        public static FishValues Defaults() => new FishValues
        {
            Multiplier = SagaSettings.Fishing.ExperienceMultiplier.Default,
            EmptyReel = SagaSettings.Fishing.EmptyReelExperience.Default,
            FightReel = SagaSettings.Fishing.FightExperience.Default,
            Catch = SagaSettings.Fishing.CatchExperience.Default,
            SizeBonus = SagaSettings.Fishing.SizeBonus.Default,
            Discovery = SagaSettings.Fishing.DiscoveryExperience.Default,
            NewSize = SagaSettings.Fishing.NewSizeExperience.Default,
        };
    }

    /// <summary>Fishing experience (FishXp), all on the angler's own client.</summary>
    public static class FishExperience
    {
        /// <summary>The factor of the game's reeling raise, with or without a fish on the line.</summary>
        public static float ReelScale(FishValues v, bool fishOnLine) => Pct.Of(fishOnLine ? v.FightReel : v.EmptyReel) * Math.Max(0f, v.Multiplier);

        /// <summary>Catch credit: Catch Experience times the species scale times 1 + Size Experience Bonus per level above 1.</summary>
        public static float Catch(FishValues v, float speciesScale, int fishLevel) =>
            Math.Max(0f, v.Catch) * Math.Max(0f, speciesScale) * (1f + Pct.Of(v.SizeBonus) * Math.Max(0, fishLevel - 1)) * Math.Max(0f, v.Multiplier);

        /// <summary>A new species (Discovery Experience) or a new size of a known one (New Size Experience), times the species scale.</summary>
        public static float Discovery(FishValues v, float speciesScale, bool newSpecies) =>
            Math.Max(0f, newSpecies ? v.Discovery : v.NewSize) * Math.Max(0f, speciesScale) * Math.Max(0f, v.Multiplier);
    }

    /// <summary>How much wood credit the server may forward. The cap is the current fell or split setting at the hardest vanilla tool tier.</summary>
    public static class WoodCreditRules
    {
        public const int MaxToolTier = 4;

        public static float Cap(WoodValues values, bool fell) =>
            fell ? WoodExperience.FellAmount(values, MaxToolTier) : WoodExperience.SplitAmount(values, MaxToolTier, true);

        public static float Clamp(float amount, float cap)
        {
            if (float.IsNaN(amount) || float.IsInfinity(amount) || amount <= 0f) return 0f;
            if (float.IsNaN(cap) || float.IsInfinity(cap) || cap <= 0f) return 0f;
            return amount < cap ? amount : cap;
        }

        public static int ClampChain(int chain, int maxChain)
        {
            if (maxChain < 0) maxChain = 0;
            if (chain < 0) return 0;
            return chain > maxChain ? maxChain : chain;
        }

        /// <summary>A fell has to be a tree, and a split a log. A prefab that is both, or neither, is not wood credit.</summary>
        public static bool KindMatches(bool sourceIsTree, bool sourceIsLog, bool fell)
        {
            if (sourceIsTree == sourceIsLog) return false;
            return fell ? sourceIsTree : sourceIsLog;
        }

        public static bool ServerAccepts(bool server, bool senderOwnsSource, bool kindMatchesSource, bool firstForSource) =>
            server && senderOwnsSource && kindMatchesSource && firstForSource;

        public static bool ClientAccepts(bool senderIsServer) => senderIsServer;
    }

    /// <summary>A clean-strike mark is kept only for a miner the sender actually owns.</summary>
    public static class CleanStrikeRules
    {
        public static bool Keep(bool active, bool ownsRock, bool minerPresent, bool senderOwnsMiner, int area) =>
            active && ownsRock && minerPresent && senderOwnsMiner && area >= 0;
    }
}
