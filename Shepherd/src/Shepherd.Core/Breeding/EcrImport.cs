// Shepherd - ExtraPotions Meadworks animal husbandry.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks).
//
// Adapted from EliteCreaturesReborn 3.20.0, Copyright (C) 2026 Gerald Glasgow, GPL-3.0-or-later (ValheimMods monorepo
// at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1: EliteCreaturesReborn/EliteCreaturesReborn/Traits/TraitKeys.cs (key
// names), EliteCreaturesReborn/EliteCreaturesReborn/Traits/TraitStore.cs TraitStore.SetSire and TraitStore.TakeSire
// (stars + 1, 0 = none), EliteCreaturesReborn/EliteCreaturesReborn/Breeding/EggData.cs EggData.Read (egg text
// stars,mask,biome), EliteCreaturesReborn/EliteCreaturesReborn/Breeding/KeeperBonus.cs (the grindstone_star_up key)).
// Full notice in THIRD_PARTY_NOTICES/ValheimMods-GPL-3.0-or-later.txt.
//
// Changed by expDARE, 2026: namespace Shepherd.Breeding; Unity-free, reading through IKeyStore; read-only import
// for the converter (design 9.2): the ECR sire level becomes Shepherd's pending sire level when Shepherd has none,
// the source keys are never edited, one successful migration is marked to prevent stale sire reuse, and the mask
// is handed back for Wildblood (dropped by the caller without it).
// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Globalization;

namespace ExtraPotions.Shepherd.Breeding
{
    /// <summary>A sire EliteCreaturesReborn recorded at conception.</summary>
    public sealed class EcrSire
    {
        internal EcrSire(int level, int mask) { Level = level; Mask = mask; }

        /// <summary>Level = stars + 1, which is exactly the stored <c>ecr_sire_stars</c> value.</summary>
        public int Level { get; }
        public int Mask { get; }
    }

    /// <summary>An EliteCreaturesReborn egg's traits (<c>m_customData["ecr_egg"]</c> = <c>stars,mask,biome</c>).</summary>
    public readonly struct EcrEgg
    {
        public EcrEgg(int stars, int mask, int biome) { Stars = stars; Mask = mask; Biome = biome; }

        public int Stars { get; }
        public int Level => Stars + 1;
        public int Mask { get; }
        public int Biome { get; }
    }

    /// <summary>EliteCreaturesReborn breeding data, read when ECR is absent (design 9.2, S12 converter).</summary>
    public static class EcrImport
    {
        public const string SireStarsKey = "ecr_sire_stars";
        public const string SireMaskKey = "ecr_sire_mask";
        public const string EggKey = "ecr_egg";
        public const string SireImportedKey = "shepherd.imported.ecr_sire";

        /// <summary>The GrindstoneSkills to ECR Better Offspring hand-off (design 9.1): 1 asks ECR for a star, 0 declines.</summary>
        public const string StarUpKey = "grindstone_star_up";

        /// <summary>The stored <c>ecr_sire_stars</c> (stars + 1; 0 or less = no sire) and <c>ecr_sire_mask</c>.</summary>
        public static EcrSire? ReadSire(int sireStarsValue, int mask) =>
            sireStarsValue <= 0 ? null : new EcrSire(sireStarsValue, Math.Max(0, mask));

        /// <summary>
        /// When the pregnant parent carries an ECR sire and no Shepherd pending sire, writes the sire's level as the
        /// pending sire (no token or name: ECR never stored them). Returns false when there is nothing to import.
        /// </summary>
        public static bool ImportPendingSire(IKeyStore store, out int mask)
        {
            if (store == null) throw new ArgumentNullException(nameof(store));
            mask = 0;
            var sire = ReadSire(store.GetInt(SireStarsKey), store.GetInt(SireMaskKey));
            if (sire == null || store.GetInt(SireImportedKey) != 0 || BreedingStore.ReadPendingSire(store) != null) return false;
            BreedingStore.WritePendingSire(store, new SireRecord("", "", sire.Level, 0, ""));
            store.Set(SireImportedKey, 1);
            mask = sire.Mask;
            return true;
        }

        /// <summary>
        /// Parses <c>stars,mask,biome</c> as ECR does: at least three non-negative invariant integers, later fields
        /// ignored. Anything else is not an ECR egg (it hatches with the game's behaviour).
        /// </summary>
        public static bool TryParseEgg(string? text, out EcrEgg egg)
        {
            egg = default;
            if (string.IsNullOrEmpty(text)) return false;
            var parts = text!.Split(',');
            if (parts.Length < 3 || !Int(parts[0], out int stars) || stars == int.MaxValue || !Int(parts[1], out int mask) || !Int(parts[2], out int biome))
                return false;
            egg = new EcrEgg(stars, mask, biome);
            return true;
        }

        private static bool Int(string text, out int value) =>
            int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value) && value >= 0;
    }
}
