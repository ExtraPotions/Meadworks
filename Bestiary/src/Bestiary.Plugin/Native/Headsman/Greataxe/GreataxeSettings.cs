// Portions Copyright (C) 2026 Gerald Glasgow. EliteCreaturesPack 0.7.2 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Changed by expDARE, 2026: Bestiary namespace, owned Crucible settings, rebuilt-only bundles and lifecycle adapters.
// SPDX-License-Identifier: GPL-3.0-or-later
using BepInEx.Configuration;
using ExtraPotions.Bestiary.Runtime.Native.Adapters;

namespace ExtraPotions.Bestiary.Runtime.Native.Headsman
{
    /// <summary>
    /// Section 26: the Executioner's Greataxe, the players' two-handed axe made from the Crypt Executioner's axehead. Its
    /// damage (a fully upgraded Bronze Axe's by default) and its recipe at the workbench. Synced.
    /// </summary>
    public static class GreataxeSettings
    {
        // No apostrophe: BepInEx refuses = \n \t \ " ' [ ] in section and key names, and the throw stops the whole Awake.
        public const string Section = "26 - Executioner Greataxe";

        private static GatedEntry<bool> enabled = null!;
        private static GatedEntry<float> slash = null!, chop = null!;
        private static GatedEntry<int> spines = null!, bones = null!, station = null!;

        public static bool On => enabled.Value;
        public static float Slash => slash.Value;
        public static float Chop => chop.Value;
        public static int Spines => spines.Value;
        public static int Bones => bones.Value;
        public static int StationLevel => station.Value;

        public static void Initialize(NativeConfiguration config)
        {
            enabled = config.Bind(Section, "Recipe", true, "Whether the greataxe can be made at the workbench.");
            slash = config.Bind(Section, "Slash", 55f, "Slash damage (a fully upgraded Bronze Axe: 55).", acceptableValues: Settings.Range(0f, 1000f));
            chop = config.Bind(Section, "Chop", 49f, "Chop damage, for trees (a fully upgraded Bronze Axe: 49).", acceptableValues: Settings.Range(0f, 1000f));
            spines = config.Bind(Section, "Spines", 1,
                "Spines (the bone weapons' drop from skeletons) the recipe takes besides the axehead; without the skeleton arsenal, "
                + "bone fragments take their place.", acceptableValues: new AcceptableValueRange<int>(0, 100));
            Settings.Renew(spines, 8);
            bones = config.Bind(Section, "Bone Fragments", 10, "Bone fragments the recipe takes.", acceptableValues: new AcceptableValueRange<int>(0, 100));
            station = config.Bind(Section, "Workbench Level", 3, "Workbench level required to craft the Executioner Greataxe.", acceptableValues: new AcceptableValueRange<int>(1, 5));
        }
    }
}
