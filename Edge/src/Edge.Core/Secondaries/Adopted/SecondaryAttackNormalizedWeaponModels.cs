// Edge - ExtraPotions Meadworks. SPDX-License-Identifier: GPL-3.0-only
// Portions from SecondaryAttacks 1.2.17 (sighsorry1029), GPL-3.0, at 4375a2b17297052a773c5b143b0cae21dc655ed8:
// SecondaryAttackNormalizedWeaponModels.cs. Source copyright and licence retained in THIRD_PARTY_NOTICES.
// Changed by expDARE, 2026: Edge namespace, catalog and diagnostics seams; Unity rotation value replaced by a portable
// RotationOffset; world-applied snapshot omitted; generated clone names use Edge. Normalisation rules retained.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks) for the changes.
using System.Collections.Generic;

namespace ExtraPotions.Edge.Secondaries.Adopted;

internal sealed class NormalizedWeaponConfig
{
    public bool Enabled { get; set; } = true;

    public bool UseAutomaticFallback { get; set; }

    public NormalizedSecondaryModeConfig? Secondary { get; set; }

    public NormalizedSneakAmbushConfig? SneakAmbush { get; set; }

    public NormalizedCleavingThrustConfig? CleavingThrust { get; set; }

    public NormalizedLaunchSlamConfig? LaunchSlam { get; set; }

    public NormalizedKnockbackChainConfig? KnockbackChain { get; set; }

    public NormalizedAftershockConfig? Aftershock { get; set; }

    public NormalizedRiftTrailConfig? RiftTrail { get; set; }

    public NormalizedFractureLineConfig? FractureLine { get; set; }

    public NormalizedHarvestSweepConfig? HarvestSweep { get; set; }

    public NormalizedMeleeOnProjectileHitConfig? SpearRain { get; set; }

    public NormalizedImpactBurstConfig? ImpactBurst { get; set; }

    public NormalizedBoomerangConfig? Boomerang { get; set; }

    public NormalizedSpinningSweepConfig? SpinningSweep { get; set; }

    public MeleeSpecialPreset MeleePreset { get; set; } = MeleeSpecialPreset.None;

    public bool HasExplicitMeleePreset { get; set; }

    public bool HasEnabledMeleeFeatureConfig =>
        SneakAmbush?.Enabled == true ||
        CleavingThrust?.Enabled == true ||
        LaunchSlam?.Enabled == true ||
        KnockbackChain?.Enabled == true ||
        Aftershock?.Enabled == true ||
        RiftTrail?.Enabled == true ||
        FractureLine?.Enabled == true ||
        ImpactBurst?.Enabled == true ||
        Boomerang?.Enabled == true ||
        SpinningSweep?.Enabled == true ||
        HarvestSweep?.Enabled == true;

    public NormalizedWeaponConfig Clone()
    {
        return (NormalizedWeaponConfig)MemberwiseClone();
    }
}

internal sealed class NormalizedSecondaryModeConfig
{
    public string Type { get; set; } = "";

    public string Animation { get; set; } = "";

    public float ResourceMultiplier { get; set; } = 1f;

    public float OutputMultiplier { get; set; } = 1f;

    public float DurabilityFactor { get; set; } = 1f;

    public NormalizedProjectileSecondaryConfig? Projectile { get; set; }

    public string CopyFrom { get; set; } = "";

    public NormalizedMeleeOnProjectileHitConfig? OnProjectileHit { get; set; }

    public NormalizedSummonEmpowerSecondaryConfig? SummonEmpower { get; set; }

    public NormalizedShieldConvertSecondaryConfig? ShieldConvert { get; set; }
}
