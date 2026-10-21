// Ember - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original, clean room. TorchesAreFires 1.1.0 (blacks7ar) has no license grant: no code was copied and its DLL
// was not decompiled. Behavior comes from its README, CHANGELOG, the Solo config file and design section 1.1 T1 to T6;
// see docs/research/torchesarefires-behavior.md.
using System;
using System.Collections.Generic;
using System.Linq;

namespace ExtraPotions.Ember.Torches
{
    /// <summary>Torches.Fire Pieces (design 4.2): a comma list of piece prefab names, case-sensitive.</summary>
    public static class FirePieceList
    {
        public const string Default = "piece_groundtorch,piece_groundtorch_wood,piece_groundtorch_green,piece_groundtorch_blue,piece_walltorch";

        /// <summary>Trimmed names in list order; blanks and exact duplicates dropped.</summary>
        public static IReadOnlyList<string> Parse(string? text)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var result = new List<string>();
            foreach (var part in (text ?? "").Split(','))
            {
                var name = part.Trim();
                if (name.Length > 0 && seen.Add(name)) result.Add(name);
            }
            return result;
        }

        public static string Normalize(string? text) => string.Join(",", Parse(text));

        /// <summary>Listed names the game does not know (logged once at Normal by the plugin).</summary>
        public static IReadOnlyList<string> Unknown(IEnumerable<string> names, Func<string, bool> known)
        {
            if (names == null) throw new ArgumentNullException(nameof(names));
            if (known == null) throw new ArgumentNullException(nameof(known));
            return names.Where(n => !known(n)).Distinct(StringComparer.Ordinal).ToList();
        }
    }

    /// <summary>Server values of the held-torch burn rules (T3 to T5), normalized.</summary>
    public readonly struct TorchBurnSettings
    {
        static readonly IReadOnlyList<string> NoWeathers = Array.Empty<string>();

        public TorchBurnSettings(bool environmentAffects, bool coldBiomeAffects, float burnRate, IReadOnlyList<string>? extraWeathers = null)
        {
            EnvironmentAffects = environmentAffects;
            ColdBiomeAffects = coldBiomeAffects;
            BurnRate = float.IsNaN(burnRate) || float.IsInfinity(burnRate) ? TorchBurn.DefaultBurnRate : Math.Max(0f, Math.Min(1f, burnRate));
            ExtraWeathers = extraWeathers ?? NoWeathers;
        }

        public bool EnvironmentAffects { get; }
        public bool ColdBiomeAffects { get; }
        public float BurnRate { get; }

        /// <summary>Further environment names that count as burn weather (Tempest's list, design section 9).</summary>
        public IReadOnlyList<string> ExtraWeathers { get; }
    }

    /// <summary>T3 to T5 and Endless Torches: the durability a held torch loses per second.</summary>
    public static class TorchBurn
    {
        public const float DefaultBurnRate = 0.5f;

        /// <summary>
        /// Vanilla weather names that make a held torch burn at Burn Rate (TorchesAreFires config and README), plus the Deep North
        /// storm Twilight_SnowStorm (Valheim 1.0.20 environment list).
        /// </summary>
        public static IReadOnlyList<string> BurnWeathers { get; } = Array.AsReadOnly(new[] { "LightRain", "SwampRain", "ThunderStorm", "SnowStorm", "Twilight_SnowStorm" });

        /// <summary>Biome names (Heightmap.Biome) that count as cold.</summary>
        public static IReadOnlyList<string> ColdBiomes { get; } = Array.AsReadOnly(new[] { "Mountain", "DeepNorth" });

        public static bool IsBurnWeather(string? environment, TorchBurnSettings settings) =>
            environment != null && (BurnWeathers.Contains(environment, StringComparer.Ordinal) || settings.ExtraWeathers.Contains(environment, StringComparer.Ordinal));

        public static bool IsColdBiome(string? biome) => biome != null && ColdBiomes.Contains(biome, StringComparer.Ordinal);

        /// <summary>
        /// Effective drain: 0 with Endless Torches; Burn Rate under burn weather (Environment Affects Torch) or in a cold
        /// biome (Cold Biome Affects Torch); otherwise the item's own drain. Never written to the shared item data.
        /// </summary>
        public static float Drain(float vanillaDrain, TorchBurnSettings settings, string? environment, string? biome, bool endless)
        {
            if (endless) return 0f;
            if ((settings.EnvironmentAffects && IsBurnWeather(environment, settings)) || (settings.ColdBiomeAffects && IsColdBiome(biome)))
                return settings.BurnRate;
            return float.IsNaN(vanillaDrain) || vanillaDrain < 0f ? 0f : vanillaDrain;
        }

        /// <summary>Listed fire pieces use no fuel with Endless Torches.</summary>
        public static bool UsesFuel(bool endless) => !endless;
    }

    /// <summary>T1, T2: when a piece or a carrier acts as fire.</summary>
    /// <summary>Which hand carries a lit torch.</summary>
    public enum CarriedHand { None, Right, Left }

    public static class TorchFire
    {
        /// <summary>Radius in meters of the heat and fire area on a burning listed piece or a torch carrier (TorchesAreFires
        /// parity, observed natively 2026-10-08: warm to 6 m, Cold at 7 m). Only the campfire's area types are reused.</summary>
        public const float AreaRadius = 6f;

        /// <summary>Radius in meters of the area on a carrier of a lit torch: 2 m, as TorchesAreFires' held torch (user decision 2026-10-08).</summary>
        public const float CarriedAreaRadius = 2f;
        /// <summary>The hand whose item attach anchors a carrier's warmth area (user decision 2026-10-08, as TorchesAreFires'
        /// held torch): the right hand first, as the runtime's held-torch lookup.</summary>
        public static CarriedHand HandFor(bool rightHoldsTorch, bool leftHoldsTorch) =>
            rightHoldsTorch ? CarriedHand.Right : leftHoldsTorch ? CarriedHand.Left : CarriedHand.None;

        public static bool PieceIsFire(bool moduleActive, bool listed, bool burning) => moduleActive && listed && burning;

        public static bool HeldWarms(bool moduleActive, bool heldTorchWarms, bool torchEquipped, float durability) =>
            moduleActive && heldTorchWarms && torchEquipped && durability > 0f;
    }
}
