// Adapted from assets/references/source/ValheimMods-openkeep-3.0.0/GrindstoneSkills/GrindstoneSkills/Woodcutting/OldGrowth/OldGrowth.cs at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Copyright (C) 2026 Gerald Glasgow. SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Saga namespace, configuration, CS1 and ownership boundaries.
using UnityEngine;

namespace ExtraPotions.Saga.Runtime.Adopted.Grindstone
{
    /// <summary>
    /// Old growth: the biggest trees of each kind give more wood. Every tree gets a random size (its local scale) when it
    /// appears, within a range of its own kind (<see cref="OldGrowthSizes"/>).
    /// <list type="bullet">
    /// <item>When a tree falls, on its owner, <see cref="OnFelled"/> stores its size within its kind, 0..1, on the new
    /// log's ZDO (<see cref="Keys.WoodSize"/>). The log was just spawned in TreeBase.SpawnLog and is owned there;
    /// <see cref="LogSpawns"/> copies the size to the halves when the log splits.</item>
    /// <item>When a log half breaks, on the log's owner, <see cref="LogBreaking"/> asks for the <see cref="Bonus"/>:
    /// the Old Growth Bonus times the woodcutter's level share (the breaker, else the feller) times the size share,
    /// nothing up to the Old Growth Start and all of it from the Old Growth Full.</item>
    /// </list>
    /// Logs felled before GrindstoneSkills stored sizes, or from a tree of unknown kind, carry no size and get no bonus.
    /// </summary>
    public static class OldGrowth
    {
        private static readonly int SizeHash = Keys.WoodSize.GetStableHashCode();

        /// <summary>
        /// Called by <see cref="Felling"/> on the tree's owner, first of the felling features, for every fell (with or
        /// without a woodcutter): stores the tree's size within its kind on the new log (<see cref="Keys.WoodSize"/>).
        /// </summary>
        public static void OnFelled(FellContext fell)
        {
            ZNetView nview = fell != null && fell.Log != null ? fell.Log.m_nview : null;
            if (nview == null || !nview.IsValid() || !nview.IsOwner())
                return;
            float size = OldGrowthSizes.Of(fell.TreePrefab, fell.Species, fell.Scale.x);
            if (size >= 0f)
                nview.GetZDO().Set(SizeHash, size);
        }

        /// <summary>Called by <see cref="LogBreaking"/> before the drops spawn: this feature's yield bonus (0 = none).</summary>
        public static float Bonus(BreakContext broken)
        {
            if (broken == null || !broken.DropsWood || broken.Size < 0f)
                return 0f;
            float level = broken.Woodcutter != null ? broken.Woodcutter.Level : 0f;
            return WoodSkill.Share(OldGrowthSettings.Bonus.Value, level) * SizeShare(broken.Size);
        }

        /// <summary>
        /// The share of the bonus a tree of this size within its kind (0..1) earns: 0 up to the start, 1 from the full,
        /// linear in between. A full at or below the start makes every tree above the start full size.
        /// </summary>
        public static float SizeShare(float size)
        {
            float start = OldGrowthSettings.Start.Value / 100f;
            float full = OldGrowthSettings.Full.Value / 100f;
            if (size <= start)
                return 0f;
            return full <= start ? 1f : Mathf.Clamp01((size - start) / (full - start));
        }
    }
}
