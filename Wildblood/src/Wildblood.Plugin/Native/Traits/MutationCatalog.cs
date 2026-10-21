// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
namespace ExtraPotions.Wildblood.Adopted.Traits
{
    /// <summary>
    /// The static facts about each mutation the specification fixes: the display word that goes in a creature's
    /// name, and the default star colour. Tunable per-mutation numbers live in the config classes, not here.
    /// </summary>
    public static class MutationCatalog
    {
        /// <summary>Every mutation, in specification order. Iterate this, never <c>Enum.GetValues</c>, so order is guaranteed.</summary>
        public static readonly Mutation[] InOrder =
        {
            Mutation.Mad, Mutation.Bloated, Mutation.Cloaked, Mutation.Splintering, Mutation.Leeching,
            Mutation.Warding, Mutation.Plated, Mutation.Miasmic, Mutation.Devouring, Mutation.Thieving,
            Mutation.Gilded, Mutation.Blinking, Mutation.Relentless, Mutation.Juggernaut, Mutation.Screecher,
            Mutation.Frostbound, Mutation.Mudbound, Mutation.Corrodent, Mutation.Cloning,
            Mutation.Flamebound, Mutation.Piercing, Mutation.Howling, Mutation.Binding,
        };

        /// <summary>
        /// Whether a mutation rolls when a rule file does not name it under `mutations enabled`: Piercing, Howling and
        /// Binding are off until a server turns them on (EliteCreaturesReborn 3.22).
        /// </summary>
        public static bool OnByDefault(Mutation mutation) =>
            mutation != Mutation.Piercing && mutation != Mutation.Howling && mutation != Mutation.Binding;

        /// <summary>The mutation a rule-file name (its display word) refers to, or null for an unknown word.</summary>
        public static Mutation? FromName(string name)
        {
            foreach (Mutation mutation in InOrder)
            {
                if (string.Equals(Word(mutation), name, System.StringComparison.OrdinalIgnoreCase))
                {
                    return mutation;
                }
            }
            return null;
        }

        /// <summary>The word placed before the creature's own name, e.g. "Mad" in "Mad Greydwarf".</summary>
        public static string Word(Mutation mutation) => mutation switch
        {
            Mutation.Mad => "Mad",
            Mutation.Bloated => "Bloated",
            Mutation.Cloaked => "Cloaked",
            Mutation.Splintering => "Splintering",
            Mutation.Leeching => "Leeching",
            Mutation.Warding => "Warding",
            Mutation.Plated => "Plated",
            Mutation.Miasmic => "Miasmic",
            Mutation.Devouring => "Devouring",
            Mutation.Thieving => "Thieving",
            Mutation.Gilded => "Gilded",
            Mutation.Blinking => "Blinking",
            Mutation.Relentless => "Relentless",
            Mutation.Juggernaut => "Juggernaut",
            Mutation.Screecher => "Screecher",
            Mutation.Frostbound => "Frostbound",
            Mutation.Mudbound => "Mudbound",
            Mutation.Corrodent => "Corrodent",
            Mutation.Cloning => "Cloning",
            Mutation.Flamebound => "Flamebound",
            Mutation.Piercing => "Piercing",
            Mutation.Howling => "Howling",
            Mutation.Binding => "Binding",
            _ => "",
        };

        /// <summary>The default star colour as an "#RRGGBB" string; the palette config seeds itself from these.</summary>
        public static string DefaultColorHex(Mutation mutation) => mutation switch
        {
            Mutation.Mad => "#E23030",         // Red
            Mutation.Bloated => "#8B5A2B",     // Brown
            Mutation.Cloaked => "#4AA6FF",     // Blue - kept bright/light so it reads apart from Warding's navy on a small star
            Mutation.Splintering => "#FFFFFF", // Bright white
            Mutation.Leeching => "#33CC33",    // Green
            Mutation.Warding => "#101C50",     // Dark blue, near navy - genuinely dark against snow and night
            Mutation.Plated => "#F2C40C",      // Yellow
            Mutation.Miasmic => "#1E6B2E",     // Dark green
            Mutation.Devouring => "#7A0F0F",   // Dark red
            Mutation.Thieving => "#A64BE0",    // Violet
            Mutation.Gilded => "#FFE066",      // Pale gold - lighter than Plated's deep yellow
            Mutation.Blinking => "#29E0E0",    // Cyan - greener than Cloaked's sky blue
            Mutation.Relentless => "#FF7F24",  // Orange
            Mutation.Juggernaut => "#6E6E6E",  // Iron grey
            Mutation.Screecher => "#FF5CC8",   // Pink
            Mutation.Frostbound => "#BFEFFF",  // Pale ice - whiter than Cloaked's sky blue
            Mutation.Mudbound => "#5E4A1E",    // Mud - darker and greener than Bloated's brown
            Mutation.Corrodent => "#B7410E",   // Rust - darker and redder than Relentless's orange
            Mutation.Cloning => "#B4A8FF",     // Lavender - paler than Thieving's violet
            Mutation.Flamebound => "#FF4A1C",  // Ember - redder than Relentless's orange, brighter than Mad's red
            Mutation.Piercing => "#A8B8C8",    // Steel - bluer and lighter than Juggernaut's iron
            Mutation.Howling => "#D4C08A",     // Wolf tan - paler than Gilded's gold
            Mutation.Binding => "#6B8E23",     // Root olive - yellower than Miasmic's green
            _ => "#FFFFFF",
        };
    }
}
