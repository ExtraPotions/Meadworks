// Adapted from assets/references/source/ValheimMods-openkeep-3.0.0/GrindstoneSkills/GrindstoneSkills/Pickaxes/Seams/OpenSeams.cs at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Copyright (C) 2026 Gerald Glasgow. SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Saga namespace, configuration, CS1 and ownership boundaries.
using System.Collections.Generic;
using UnityEngine;

namespace ExtraPotions.Saga.Runtime.Adopted.Grindstone
{
    /// <summary>
    /// The local miner's open seams, at most one per rock, keyed by the rock's ZDOID, on the miner's own client. A seam
    /// leaves the book when its window runs out, its chunk breaks, its rock is destroyed or unloaded (<see cref="Tick"/>,
    /// once a frame, and <see cref="Find"/>), when a chain moves on or ends (<see cref="SeamSwing"/>), and all at once when
    /// Pickaxes is turned off or the game scene closes (<see cref="SeamDriver"/>); its glow goes with it.
    /// </summary>
    internal static class OpenSeams
    {
        private static readonly Dictionary<ZDOID, Seam> ByRock = new Dictionary<ZDOID, Seam>();
        private static readonly List<ZDOID> Stale = new List<ZDOID>();

        /// <summary>The rock's open seam; null when there is none or it has just closed (which removes it).</summary>
        public static Seam Find(Rock rock)
        {
            ZDOID id = rock != null ? rock.Id : ZDOID.None;
            if (id.IsNone() || !ByRock.TryGetValue(id, out Seam seam))
                return null;
            if (seam.IsOpen)
                return seam;
            Close(id);
            return null;
        }

        /// <summary>
        /// Opens a seam on the rock of <paramref name="swing"/>, the swing that just ended, on a chunk
        /// <see cref="SeamPicker"/> chooses, in place of the rock's current seam; <paramref name="links"/> is the chain so
        /// far. The window follows the local miner's level. No chunk fits: the rock has no seam (a chain ends there).
        /// </summary>
        public static void Open(SeamNote swing, int links)
        {
            Rock rock = swing.Rock;
            ZDOID id = rock.Id;
            Close(id);
            int area = SeamPicker.Pick(swing);
            if (id.IsNone() || area < 0)
                return;
            float window = PickSkill.Between(SeamSettings.WindowAt0.Value, SeamSettings.WindowAt100.Value, PickSkill.Local());
            float closes = Time.time + Mathf.Max(0.1f, window);
            SeamGlow glow = SeamGlow.Show(rock, area, closes, SeamDriver.Holder);
            ByRock[id] = new Seam(rock, id, area, closes, links, glow);
            SeamSound.Play(RockChunks.Centre(rock, area), links);
        }

        public static void Close(ZDOID id)
        {
            if (!ByRock.TryGetValue(id, out Seam seam))
                return;
            ByRock.Remove(id);
            if (seam.Glow != null)
                seam.Glow.Remove();
        }

        public static void CloseAll()
        {
            foreach (Seam seam in ByRock.Values)
            {
                if (seam.Glow != null)
                    seam.Glow.Remove();
            }
            ByRock.Clear();
        }

        /// <summary>Closes every seam whose window ran out or whose chunk or rock is gone.</summary>
        public static void Tick()
        {
            if (ByRock.Count == 0)
                return;
            Stale.Clear();
            foreach (KeyValuePair<ZDOID, Seam> pair in ByRock)
            {
                if (!pair.Value.IsOpen)
                    Stale.Add(pair.Key);
            }
            foreach (ZDOID id in Stale)
                Close(id);
        }
    }
}
