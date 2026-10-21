// Portions Copyright (C) 2026 Gerald Glasgow. EliteCreaturesPack 0.7.2 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Changed by expDARE, 2026: Bestiary namespace, owned Crucible settings, rebuilt-only bundles and lifecycle adapters.
// SPDX-License-Identifier: GPL-3.0-or-later
using BepInEx.Configuration;
using ExtraPotions.Bestiary.Runtime.Native.Adapters;

namespace ExtraPotions.Bestiary.Runtime.Native.Crossbow
{
    /// <summary>
    /// Section 6: the Skeleton Crossbowmen's shot, the numbers that are not its skeleton's own: how hard it hits against
    /// that skeleton's bow, how often it shoots, how fast a bolt flies and how far it shoots from. Everything else
    /// (health, resistances, faction, loot) is its skeleton's. Whether it spawns is an arsenal skeleton's switch
    /// (<see cref="Arsenal.ArsenalSettings.CrossbowmanSpawns"/>; the draw is <see cref="Skeletons.SkeletonDraw"/>).
    /// Synced; read where they apply, so a reload takes effect.
    /// </summary>
    public static class XbowSettings
    {
        public const string Section = "6 - Skeleton Crossbowman";

        private static GatedEntry<float> damageFactor = null!;
        private static GatedEntry<float> shotInterval = null!;
        private static GatedEntry<float> boltSpeed = null!;
        private static GatedEntry<float> range = null!;

        public static float DamageFactor => damageFactor.Value;
        public static float ShotInterval => shotInterval.Value;
        public static float BoltSpeed => boltSpeed.Value;
        public static float Range => range.Value;

        public static void Initialize(NativeConfiguration config)
        {
            damageFactor = config.Bind(Section, "Damage Factor", 1f,
                "A bolt's damage, as blunt, against the bow of the skeleton archer it replaces (20), before stars. 1: the "
                + "same as that archer.", acceptableValues: Settings.Range(0f, 10f));
            shotInterval = config.Bind(Section, "Shot Interval", 6f,
                "Seconds between its shots at the least (the skeleton archer's: 4). The whole shot, spanning and loading included, takes about 5.5 of them.",
                acceptableValues: Settings.Range(3f, 60f));
            boltSpeed = config.Bind(Section, "Bolt Speed", 40f,
                "Meters a second the bolt flies, straight (the skeleton archer's arrow: 30).", acceptableValues: Settings.Range(10f, 120f));
            range = config.Bind(Section, "Range", 25f, "Meters it shoots from (the skeleton archer: 20).",
                acceptableValues: Settings.Range(6f, 60f));
        }
    }
}
