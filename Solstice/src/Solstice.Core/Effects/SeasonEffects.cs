// Solstice - ExtraPotions Meadworks. SPDX-License-Identifier: GPL-3.0-or-later
// Adapted from Seasons 1.10.6 by shudnal (https://github.com/shudnal/Seasons), commit 4054af6e8823d787921d673607e4942554872b66,
// Unlicense (public domain), SeasonSettings/SeasonSettings.cs (the fields and LoadDefaultSeasonSettings). See THIRD_PARTY_NOTICES/Seasons-Unlicense.txt.
// Changed by expDARE, 2026: the default values are copied verbatim; the class is a plain Unity-free data type without the reference's
// JSON files, file watcher and config sync, and says which effects the plugin applies in 0.1.0.
using System;
using System.Collections.Generic;
using ExtraPotions.Solstice.Calendar;

namespace ExtraPotions.Solstice.Effects
{
    /// <summary>What a season does to the game: the multipliers and switches of one season.</summary>
    public sealed class SeasonEffects
    {
        /// <summary>
        /// The effects the plugin applies in 0.1.0 (each is a server-authoritative setting). The others are data for later:
        /// torches belong to Ember, night length to the sleep rules, procreation to Shepherd and drops to Wildblood (DEVELOPMENT.md).
        /// </summary>
        public static readonly IReadOnlyList<string> Wired = Array.AsReadOnly(new[]
        {
            nameof(PlantsGrowthMultiplier), nameof(BeehiveProductionMultiplier), nameof(FoodDrainMultiplier), nameof(StaminaDrainMultiplier),
            nameof(FireplaceDrainMultiplier), nameof(SapCollectingSpeedMultiplier), nameof(RestedBuffDurationMultiplier), nameof(WindIntensityMultiplier),
            nameof(RainProtection)
        });

        public int DaysInSeason { get; set; } = 10;
        public int NightLength { get; set; } = 30;
        public bool TorchAsFiresource { get; set; }
        public float TorchDurabilityDrain { get; set; } = 0.1f;
        public float PlantsGrowthMultiplier { get; set; } = 1.0f;
        public float BeehiveProductionMultiplier { get; set; } = 1.0f;
        public float FoodDrainMultiplier { get; set; } = 1.0f;
        public float StaminaDrainMultiplier { get; set; } = 1.0f;
        public float FireplaceDrainMultiplier { get; set; } = 1.0f;
        public float SapCollectingSpeedMultiplier { get; set; } = 1.0f;
        public bool RainProtection { get; set; }
        public float WoodFromTreesMultiplier { get; set; } = 1.0f;
        public float WindIntensityMultiplier { get; set; } = 1.0f;
        public float RestedBuffDurationMultiplier { get; set; } = 1.0f;
        public float LivestockProcreationMultiplier { get; set; } = 1.0f;
        public bool OverheatIn2WarmClothes { get; set; }
        public float MeatFromAnimalsMultiplier { get; set; } = 1.0f;
        public float TreesRegrowthChance { get; set; }

        public SeasonEffects Clone() => (SeasonEffects)MemberwiseClone();

        /// <summary>A fresh copy of the reference defaults of a season (LoadDefaultSeasonSettings).</summary>
        public static SeasonEffects Defaults(Season season)
        {
            var e = new SeasonEffects();
            switch (season)
            {
                case Season.Spring:
                    e.PlantsGrowthMultiplier = 2.0f;
                    e.BeehiveProductionMultiplier = 0.5f;
                    e.FireplaceDrainMultiplier = 0.75f;
                    e.SapCollectingSpeedMultiplier = 2.0f;
                    e.WoodFromTreesMultiplier = 0.75f;
                    e.WindIntensityMultiplier = 0.9f;
                    e.RestedBuffDurationMultiplier = 1.25f;
                    e.LivestockProcreationMultiplier = 1.5f;
                    e.MeatFromAnimalsMultiplier = 0.5f;
                    e.TreesRegrowthChance = 0.9f;
                    break;
                case Season.Summer:
                    e.PlantsGrowthMultiplier = 1.5f;
                    e.BeehiveProductionMultiplier = 2f;
                    e.FoodDrainMultiplier = 0.75f;
                    e.NightLength = 15;
                    e.StaminaDrainMultiplier = 0.8f;
                    e.FireplaceDrainMultiplier = 0.25f;
                    e.SapCollectingSpeedMultiplier = 1.25f;
                    e.WoodFromTreesMultiplier = 0.75f;
                    e.WindIntensityMultiplier = 1.1f;
                    e.RestedBuffDurationMultiplier = 1.5f;
                    e.LivestockProcreationMultiplier = 1.25f;
                    e.OverheatIn2WarmClothes = true;
                    e.MeatFromAnimalsMultiplier = 0.75f;
                    e.TreesRegrowthChance = 0.75f;
                    break;
                case Season.Fall:
                    e.PlantsGrowthMultiplier = 0.5f;
                    e.BeehiveProductionMultiplier = 1.5f;
                    e.FireplaceDrainMultiplier = 1f;
                    e.TorchAsFiresource = true;
                    e.SapCollectingSpeedMultiplier = 0.5f;
                    e.WoodFromTreesMultiplier = 1.25f;
                    e.WindIntensityMultiplier = 1.2f;
                    e.RestedBuffDurationMultiplier = 0.85f;
                    e.LivestockProcreationMultiplier = 0.75f;
                    e.MeatFromAnimalsMultiplier = 1.25f;
                    e.TreesRegrowthChance = 0.25f;
                    break;
                case Season.Winter:
                    e.PlantsGrowthMultiplier = 0f;
                    e.BeehiveProductionMultiplier = 0f;
                    e.FoodDrainMultiplier = 1.25f;
                    e.NightLength = 45;
                    e.TorchAsFiresource = true;
                    e.StaminaDrainMultiplier = 1.2f;
                    e.FireplaceDrainMultiplier = 2f;
                    e.SapCollectingSpeedMultiplier = 0.25f;
                    e.RainProtection = true;
                    e.WoodFromTreesMultiplier = 1.5f;
                    e.WindIntensityMultiplier = 0.9f;
                    e.RestedBuffDurationMultiplier = 0.75f;
                    e.LivestockProcreationMultiplier = 0.5f;
                    e.MeatFromAnimalsMultiplier = 1.5f;
                    e.TreesRegrowthChance = 0f;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(season));
            }
            return e;
        }

        /// <summary>The calendar for the given season lengths.</summary>
        public static SeasonCalendar CalendarOf(Func<Season, int> daysIn) =>
            new SeasonCalendar(daysIn(Season.Spring), daysIn(Season.Summer), daysIn(Season.Fall), daysIn(Season.Winter));
    }
}
