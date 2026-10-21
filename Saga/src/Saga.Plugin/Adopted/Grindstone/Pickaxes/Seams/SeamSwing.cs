// Adapted from assets/references/source/ValheimMods-openkeep-3.0.0/GrindstoneSkills/GrindstoneSkills/Pickaxes/Seams/SeamSwing.cs at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Copyright (C) 2026 Gerald Glasgow. SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Saga namespace, configuration, CS1 and ownership boundaries.
using System.Collections.Generic;
using UnityEngine;

namespace ExtraPotions.Saga.Runtime.Adopted.Grindstone
{
    /// <summary>
    /// The running swing's hits on multi-chunk rocks, on the miner's own client, and what they mean once the swing is
    /// over. A pickaxe swing sends one hit per chunk collider it touched, in no useful order, so a hit elsewhere on the
    /// rock cannot end a chain before the whole swing is known to have missed the seam chunk. Each rock the swing hit
    /// gets a <see cref="SeamNote"/> (grouped by <see cref="MineSwing.Serial"/>). When the swing is over
    /// (<see cref="SeamDriver"/> sees its scope closed, or the next swing's first hit arrives), <see cref="Settle"/>
    /// decides for each rock, in hit order:
    /// <list type="bullet">
    /// <item>a clean strike: the chain's next seam opens at once, one link further;</item>
    /// <item>an open seam the swing did not strike: the chain ends and the seam closes;</item>
    /// <item>no seam: the swing's one roll of the seam chance (only the first such rock rolls).</item>
    /// </list>
    /// A new seam may land on a chunk the swing touched, as long as the swing did not break it (<see cref="SeamPicker"/>).
    /// </summary>
    internal static class SeamSwing
    {
        private static readonly List<SeamNote> Notes = new List<SeamNote>();
        private static int serial;

        /// <summary>The swing the notes belong to is over, and they wait to be settled.</summary>
        public static bool Finished => Notes.Count > 0 && !(MineSwing.Active && MineSwing.Serial == serial);

        /// <summary>Notes a hit of the running swing; returns the swing's note for that rock.</summary>
        public static SeamNote Record(Rock rock, int area)
        {
            if (serial != MineSwing.Serial)
            {
                Settle();
                serial = MineSwing.Serial;
            }
            ZDOID id = rock.Id;
            SeamNote note = Notes.Find(known => known.Id == id);
            if (note == null)
            {
                note = new SeamNote(rock, id);
                Notes.Add(note);
            }
            if (!note.Areas.Contains(area))
                note.Areas.Add(area);
            return note;
        }

        /// <summary>Decides what the finished swing did to each rock it hit, then forgets it.</summary>
        public static void Settle()
        {
            SeamNote[] notes = Notes.ToArray();
            Notes.Clear();
            bool rolled = false;
            foreach (SeamNote note in notes)
                rolled = Decide(note, rolled) || rolled;
        }

        /// <summary>Drops the notes unsettled (Pickaxes off, no local player, the scene closing).</summary>
        public static void Forget() => Notes.Clear();

        /// <summary>Settles one rock; true when it used the swing's roll.</summary>
        private static bool Decide(SeamNote note, bool rolled)
        {
            if (!note.Rock.IsValid)
                return false;
            if (note.Link > 0)
            {
                OpenSeams.Open(note, note.Link);
                return false;
            }
            if (OpenSeams.Find(note.Rock) != null)
            {
                OpenSeams.Close(note.Id);
                return false;
            }
            if (rolled)
                return false;
            if (Random.value * 100f < Chance())
                OpenSeams.Open(note, 0);
            return true;
        }

        /// <summary>The local miner's seam chance in percent, from Seam Chance At 0 to Seam Chance At 100.</summary>
        private static float Chance() =>
            PickSkill.Between(SeamSettings.ChanceAt0.Value, SeamSettings.ChanceAt100.Value, PickSkill.Local());
    }
}
