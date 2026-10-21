// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.10.0 at d3b57606cb24775d61b7bda244b22eb89e7e7717.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
using System.Collections.Generic;
using ExtraPotions.Inscribe.Adopted.Affixes;
using ExtraPotions.Inscribe.Adopted.Items;
using ExtraPotions.Inscribe.Adopted.Rules;

namespace ExtraPotions.Inscribe.Adopted.Rolling
{
    /// <summary>
    /// One roll's working state over a builder (classes-and-tiers.md section 5): which ids and exclusion groups the item
    /// already holds, its prefix and suffix counts against the rarity's limits, and the two-stage draw itself - the
    /// affix by <c>weight</c> from the item class's pool, then its tier by tier weight, then the value. Short-lived:
    /// made per roll, never shared.
    /// <para>
    /// Occupancy: every affix on the item holds its id (dormant ones too); its exclusion group and kind are known when
    /// the id is still defined (enabled or not); an orphaned id has neither and blocks nothing else.
    /// </para>
    /// </summary>
    internal sealed class AffixDraw
    {
        private readonly RollContext _context;
        private readonly AffixLimits _limits;
        private readonly HashSet<string> _ids = new HashSet<string>();
        private readonly HashSet<string> _groups = new HashSet<string>();
        private readonly List<PoolEntry> _candidates = new List<PoolEntry>();
        private readonly List<AffixTierDef> _tiers = new List<AffixTierDef>();
        private readonly List<float> _weights = new List<float>();

        public AffixDraw(ItemStateBuilder builder, RollContext context)
        {
            Builder = builder;
            _context = context;
            _limits = AffixLimits.For(builder, context);
            foreach (AffixRoll roll in builder.Affixes)
            {
                Occupy(roll.Id);
            }
        }

        public ItemStateBuilder Builder { get; }

        /// <summary>Draws one affix and appends it. False (nothing changed) when the pool has no candidate.</summary>
        public bool TryAdd()
        {
            if (!TryDraw(out AffixRoll roll, out AffixDef def))
            {
                return false;
            }
            Builder.AddAffix(roll);
            Occupy(roll.Id);
            _limits.Count(def);
            return true;
        }

        private bool TryDraw(out AffixRoll roll, out AffixDef def)
        {
            roll = default;
            def = null!;
            Gather();
            int index = PickAffix();
            if (index < 0)
            {
                return false;
            }
            PoolEntry entry = _candidates[index];
            def = entry.Def;
            TierEligibility.Eligible(def, entry.Fit, _context, _tiers);
            AffixTierDef tier = TierEligibility.Pick(_tiers, _context, _weights);
            roll = new AffixRoll(def.Id, tier.Grade, ValueOf(def, tier));
            return true;
        }

        /// <summary>Marks an id (and its exclusion group) as held by the item.</summary>
        private void Occupy(string id)
        {
            _ids.Add(id);
            string? group = _context.Rules.Affixes.Get(id)?.ExclusionGroup;
            if (group != null)
            {
                _groups.Add(group);
            }
        }

        // Uniform in the tier's range at its decimals; a scaled affix is then multiplied by the class's damage_scale
        // and rounded again (what is stored is final); flags store 1.
        private float ValueOf(AffixDef def, AffixTierDef tier)
        {
            if (def.Value == AffixValueType.Flag)
            {
                return 1f;
            }
            float value = RollMath.RollValue(tier.Min, tier.Max, tier.Decimals, _context.Random);
            return def.Scaled ? RollMath.Scale(value, _context.Class.DamageScale, tier.Decimals) : value;
        }

        /// <summary>Every inscription the next draw could take, in pool order; draws nothing (the Rune Table shows them).</summary>
        public void Candidates(List<PoolEntry> into)
        {
            Gather();
            into.Clear();
            into.AddRange(_candidates);
        }

        private void Gather()
        {
            _candidates.Clear();
            IReadOnlyList<PoolEntry> pool = _context.Rules.Affixes.Pool(_context.Class.ClassId);
            for (int i = 0; i < pool.Count; i++)
            {
                if (IsCandidate(pool[i]))
                {
                    _candidates.Add(pool[i]);
                }
            }
        }

        private bool IsCandidate(PoolEntry entry)
        {
            AffixDef def = entry.Def;
            if (!def.Enabled || def.Weight <= 0f || _ids.Contains(def.Id) || !_limits.Allows(def.Kind) || !Favoured(def.Id))
            {
                return false;
            }
            if (def.ExclusionGroup != null && _groups.Contains(def.ExclusionGroup))
            {
                return false;
            }
            if (!ItemClasses.Satisfies(_context.Class, def.Requires))
            {
                return false;
            }
            TierEligibility.Eligible(def, entry.Fit, _context, _tiers);
            return _tiers.Count > 0;
        }

        private int PickAffix()
        {
            _weights.Clear();
            foreach (PoolEntry entry in _candidates)
            {
                _weights.Add(entry.Def.Weight);
            }
            return RollMath.PickWeighted(_weights, _context.Random);
        }

        // With an essence (the Rune Table's Ascension) only its own inscriptions may be drawn.
        private bool Favoured(string id) => _context.Favoured == null || _context.Favoured.Contains(id);
    }
}
