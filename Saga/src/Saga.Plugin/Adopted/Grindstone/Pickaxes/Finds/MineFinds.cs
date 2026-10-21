// Adapted from assets/references/source/ValheimMods-openkeep-3.0.0/GrindstoneSkills/GrindstoneSkills/Pickaxes/Finds/MineFinds.cs at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Copyright (C) 2026 Gerald Glasgow. SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Saga namespace, configuration, CS1 and ownership boundaries.
using ExtraPotions.Saga.Runtime.Adopted.Grindstone;
using UnityEngine;

namespace ExtraPotions.Saga.Runtime.Adopted.Grindstone
{
    /// <summary>
    /// Mine finds: broken rock can hide one of the game's valuables (amber, an amber pearl, a ruby). Runs on the rock's
    /// ZDO owner, where the game applied the damage and spawned the chunk's own drops (<see cref="MineBreak"/>), for
    /// every chunk or single-piece rock a miner broke, however it broke (a hit, splash, or a collapse after the chunks
    /// under it were mined away), ore and plain stone alike. The chance grows linearly with the miner's Pickaxes level
    /// from "Find Chance At 0" to "Find Chance At 100", times the chunk's health share (<see cref="HealthShare"/>), so
    /// rocks of tiny chunks are no find farm. The find is picked by weight from Saga.MineFinds.yml
    /// (<see cref="MineFindFile"/>: the rock kind's table, else its biome's), its items pop out of the broken chunk
    /// towards the miner (<see cref="FindSpawner"/>, shared with the Woodcutting finds), and everybody near sees
    /// "Found &lt;name&gt;!" there (<see cref="MineCallout.Broadcast"/>). The drops are networked objects owned by this
    /// machine, so every client sees them. The world's resource rate does not scale finds.
    /// </summary>
    public static class MineFinds
    {
        /// <summary>How far from the chunk's centre towards the miner the items appear, in metres: out of the rock's face.</summary>
        private const float Offset = 0.4f;

        /// <summary>How far above the chunk's centre the items appear, in metres, so a piece on the ground drops clear of it.</summary>
        private const float Lift = 0.2f;

        /// <summary>A chunk with this much full health or more (a copper or silver chunk) gets the whole find chance.</summary>
        private const float FullShareHealth = 50f;

        /// <summary>
        /// Called once by <see cref="MineFindSettings.Initialize"/> at plugin start, on every machine, to register the
        /// YAML files with the synced configuration (sync key <see cref="Keys.MineFindsSync"/>).
        /// </summary>
        public static void Register(NativeConfiguration config) => MineFindFile.Register(config);

        /// <summary>
        /// Called by <see cref="MineBreak"/> on the rock's owner, last of the break features, for every chunk (or single
        /// piece) a miner broke. A miner carrying a cheated item finds nothing (the game only marks its drops as
        /// cheated, which the shared spawner cannot do).
        /// </summary>
        public static void OnBreak(RockBreak broken)
        {
            if (!PickSkill.Active || broken.Cheated || Random.value >= Chance(broken.Miner.Level) * HealthShare(broken.Rock.Info))
                return;
            FindEntry find = MineFindFile.Pick(broken.Kind, broken.Biome);
            if (find == null)
                return;
            Vector3 away = Away(broken);
            Vector3 spot = broken.Position + away * Offset + Vector3.up * Lift;
            if (FindSpawner.Spawn(find, spot, away) > 0)
                MineCallout.Broadcast(spot, SagaText.Packet("gs_2dadc5689ae69efb", find.Name));
        }

        /// <summary>The chance of a find, 0..1, for a miner at this Pickaxes level.</summary>
        public static float Chance(float level) =>
            Mathf.Clamp01(PickSkill.Between(MineFindSettings.ChanceAt0.Value, MineFindSettings.ChanceAt100.Value, level) / 100f);

        /// <summary>
        /// The share of the find chance a broken chunk (or single piece) gets: its full health in the prefab
        /// (<see cref="RockInfo.Health"/>) over 50, at most 1. A rock of tiny chunks breaks many of them for the work of
        /// one: a boulder's 30-health chunk gets 60%, a mud pile's 5-health chunk 10%, an Ashlands floor's 1-health piece
        /// 2%; tin and obsidian (30) 60%, copper and silver chunks (50) and bigger all of it.
        /// </summary>
        public static float HealthShare(RockInfo info) => Mathf.Clamp01(info.Health / FullShareHealth);

        /// <summary>
        /// Level ground direction from the chunk towards the miner, so the find tumbles out of the face they are working:
        /// towards their player when it is loaded here, else against the breaking hit's direction (a collapse's hit may
        /// have none), else a random direction.
        /// </summary>
        private static Vector3 Away(RockBreak broken)
        {
            Player miner = broken.Miner.LoadedPlayer;
            Vector3 away = miner != null ? miner.transform.position - broken.Position
                : broken.Hit != null ? -broken.Hit.m_dir : Vector3.zero;
            away.y = 0f;
            if (away.sqrMagnitude > 0.01f)
                return away.normalized;
            float angle = Random.Range(0f, Mathf.PI * 2f);
            return new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
        }
    }
}
