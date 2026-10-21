// Adapted from assets/references/source/ValheimMods-openkeep-3.0.0/GrindstoneSkills/GrindstoneSkills/Woodcutting/Core/Woodcutter.cs at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Copyright (C) 2026 Gerald Glasgow. SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Saga namespace, configuration, CS1 and ownership boundaries.
using UnityEngine;

namespace ExtraPotions.Saga.Runtime.Adopted.Grindstone
{
    /// <summary>
    /// The player behind a woodcutting hit or a fallen log: who (player ID, 0 when unknown), at what Woodcutting level,
    /// and how deep in a domino chain (0 for a tree felled by a swing, 1 for the first tree a felled log knocks over).
    /// Read from a hit on the target's owner (<see cref="FromHit"/>) or from a log's ZDO (<see cref="FromZdo"/>).
    /// </summary>
    public sealed class Woodcutter
    {
        private static readonly int PlayerHash = Keys.WoodPlayer.GetStableHashCode();
        private static readonly int LevelHash = Keys.WoodLevel.GetStableHashCode();
        private static readonly int ChainHash = Keys.WoodChain.GetStableHashCode();

        public Woodcutter(long playerId, float level, int chain)
        {
            PlayerId = playerId;
            Level = Mathf.Clamp(level, 0f, WoodSkill.MaxLevel);
            Chain = Mathf.Max(0, chain);
        }

        public long PlayerId { get; }
        public float Level { get; }
        public int Chain { get; }

        /// <summary>The level as a share of level 100, 0..1.</summary>
        public float Factor => WoodSkill.Factor(Level);

        /// <summary>The woodcutter of a hit, on the target's owner; null unless it is a woodcutting hit.</summary>
        public static Woodcutter FromHit(HitData hit)
        {
            if (hit == null || hit.m_skill != WoodSkill.Skill)
                return null;
            return new Woodcutter(WoodHit.PlayerId(hit), hit.m_skillLevel, WoodHit.Chain(hit));
        }

        /// <summary>The woodcutter stored on a log's ZDO; null when the log has none (felled without GrindstoneSkills).</summary>
        public static Woodcutter FromZdo(ZDO zdo)
        {
            if (zdo == null)
                return null;
            float level = zdo.GetFloat(LevelHash, -1f);
            return level < 0f ? null : new Woodcutter(zdo.GetLong(PlayerHash), level, zdo.GetInt(ChainHash));
        }

        /// <summary>Stores this woodcutter on a ZDO the caller owns.</summary>
        public void WriteTo(ZDO zdo)
        {
            if (zdo == null)
                return;
            zdo.Set(PlayerHash, PlayerId);
            zdo.Set(LevelHash, Level);
            zdo.Set(ChainHash, Chain);
        }
    }
}
