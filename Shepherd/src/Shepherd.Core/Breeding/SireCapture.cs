// Shepherd - ExtraPotions Meadworks animal husbandry.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks).
//
// Adapted from EliteCreaturesReborn 3.30.0, Copyright (C) 2026 Gerald Glasgow, GPL-3.0-or-later (ValheimMods monorepo
// at 0471a49e972e4453a453e365cb90b5fd20d0f4ea: EliteCreaturesReborn/EliteCreaturesReborn/Breeding/Parents.cs
// Parents.FindPartner and Parents.IsPartner). Full notice in THIRD_PARTY_NOTICES/ValheimMods-GPL-3.0-or-later.txt.
//
// Changed by expDARE, 2026: namespace Shepherd.Breeding; Unity-free: the plugin gathers the loaded candidates and passes
// plain facts in; candidates must also be ready to procreate (design 10.2, the game's ReadyForProcreation); the nearest
// wins and an equal distance keeps the earlier candidate; the record carries token, name, level, generation and the
// Wildblood traits blob instead of ECR traits.
// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;

namespace ExtraPotions.Shepherd.Breeding
{
    /// <summary>One loaded creature near a parent that just conceived, as the plugin sees it.</summary>
    public sealed class PartnerCandidate
    {
        public PartnerCandidate(string token, string name, string prefabName, int level, int generation, string traits,
            double distance, bool isSelf, bool isTamed, bool isReady)
        {
            Token = token ?? ""; Name = name ?? ""; PrefabName = prefabName ?? ""; Level = level; Generation = generation;
            Traits = traits ?? ""; Distance = distance; IsSelf = isSelf; IsTamed = isTamed; IsReady = isReady;
        }

        public string Token { get; }
        public string Name { get; }
        public string PrefabName { get; }
        public int Level { get; }
        public int Generation { get; }
        public string Traits { get; }
        public double Distance { get; }
        public bool IsSelf { get; }
        public bool IsTamed { get; }
        /// <summary>The game's <c>Procreation.ReadyForProcreation()</c>: tamed, not pregnant, not hungry.</summary>
        public bool IsReady { get; }
    }

    /// <summary>The sire captured at conception (design 10.1 <c>shepherd.pending_sire*</c> keys).</summary>
    public sealed class SireRecord
    {
        public SireRecord(string token, string name, int level, int generation, string traits)
        {
            if (level < 1) throw new ArgumentOutOfRangeException(nameof(level), "levels start at 1");
            Token = token ?? ""; Name = name ?? ""; Level = level; Generation = Math.Max(0, generation); Traits = traits ?? "";
        }

        public string Token { get; }
        public string Name { get; }
        public int Level { get; }
        public int Generation { get; }
        public string Traits { get; }
    }

    /// <summary>
    /// Sire capture (design 10.2): the partner prefab is <c>m_seperatePartner</c> or the parent's own prefab; the nearest
    /// tamed, ready candidate of that prefab within <c>m_partnerCheckRange</c>, excluding the parent itself, is the sire.
    /// No candidate (asexual <c>m_noPartnerOffspring</c> lines) gives no sire, and inheritance uses the mother only.
    /// </summary>
    public static class SireCapture
    {
        public static SireRecord? Choose(string partnerPrefab, double partnerRange, IEnumerable<PartnerCandidate> candidates)
        {
            if (candidates == null) throw new ArgumentNullException(nameof(candidates));
            if (string.IsNullOrEmpty(partnerPrefab)) return null;
            PartnerCandidate? best = null;
            foreach (var candidate in candidates)
            {
                if (candidate == null || candidate.IsSelf || !candidate.IsTamed || !candidate.IsReady) continue;
                if (!string.Equals(candidate.PrefabName, partnerPrefab, StringComparison.Ordinal)) continue;
                if (!(candidate.Distance <= partnerRange)) continue;
                if (best == null || candidate.Distance < best.Distance) best = candidate;
            }
            return best == null ? null : new SireRecord(best.Token, best.Name, Math.Max(1, best.Level), best.Generation, best.Traits);
        }
    }
}
