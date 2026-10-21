// Portions Copyright (C) 2026 Gerald Glasgow. EliteCreaturesPack 0.9.0 at ValheimMods 3bb778330f23645ccddb42fbc0c6ffab72c842b6.
// Changed by expDARE, 2026: Bestiary namespace, owned Crucible settings, the Bestiary master switch.
// SPDX-License-Identifier: GPL-3.0-or-later
using ExtraPotions.Bestiary.Runtime.Native.Adapters;

namespace ExtraPotions.Bestiary.Runtime.Native.Ballista
{
    /// <summary>
    /// Section 28: the Bone Ballista, one switch. It only puts the ballista in the hammer and its Bone Missiles on the
    /// workbench, or takes them out: the prefabs are always registered, so ballistas already built load and still shoot.
    /// "Enabled" is also off while Bestiary's own master switch is off (<see cref="GatedEntry{T}"/>).
    /// </summary>
    public static class BallistaSettings
    {
        public const string Section = "28 - Bone Ballista";

        private static GatedEntry<bool> enabled = null!;

        public static bool Enabled => enabled.Value;

        public static void Initialize(NativeConfiguration config)
        {
            enabled = config.Bind(Section, "Enabled", true,
                "A small ballista of bones in the hammer's Misc. tab beside the game's ballista (3 spines, 25 bone fragments), "
                + "and its Bone Missiles at the workbench (level 2: 5 bone fragments and 2 feathers make 20). Hold it with the "
                + "use key, aim with the mouse (45 degrees either way), shoot with attack; it reloads from the missiles you "
                + "carry. Off: neither can be made; ballistas already built stay and still shoot.");
        }
    }
}
