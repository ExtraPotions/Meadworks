// Portions Copyright (C) 2026 Gerald Glasgow. EliteCreaturesPack 0.7.2 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Changed by expDARE, 2026: Bestiary namespace, owned Crucible settings, rebuilt-only bundles and lifecycle adapters.
// SPDX-License-Identifier: GPL-3.0-or-later
using BepInEx.Configuration;
using ExtraPotions.Bestiary.Catalog;
using ExtraPotions.Bestiary.Runtime.Native.Adapters;

namespace ExtraPotions.Bestiary.Runtime.Native.Headsman
{
    /// <summary>
    /// Section 25: the Crypt Executioner. Whether it may appear and how rarely (the share of the Black Forest's burial
    /// chambers that hold one, fixed per chamber by its seed, and how long it takes to come back after it dies), its
    /// health, the damage of each attack, the reach of its axe head and the speed of its slam's wind-up, how many
    /// skeletons it may have raised at once, and the chance it drops its axehead. Synced; read where they apply, so a
    /// reload takes effect.
    /// </summary>
    public static class HeadsmanSettings
    {
        public const string Section = "25 - Crypt Executioner";

        private static GatedEntry<bool> enabled = null!;
        private static GatedEntry<float> chambers = null!, respawnDays = null!, health = null!;
        private static GatedEntry<float> slam = null!, sweep = null!, spin = null!, thrown = null!, rear = null!;
        private static GatedEntry<int> summons = null!;
        private static GatedEntry<float> axehead = null!, axeRadius = null!, windup = null!;

        /// <summary>Whether chambers generated from now on may hold one. Ones already placed stay.</summary>
        public static bool On => enabled.Value;

        /// <summary>The share of burial chambers that hold one, 0 to 1.</summary>
        public static float Chambers => chambers.Value / 100f;

        /// <summary>Minutes before it comes back after it died (a game day is 30 minutes).</summary>
        public static float RespawnMinutes => respawnDays.Value * 30f;

        public static float Health => health.Value;
        public static float SlamDamage => slam.Value;
        public static float SweepDamage => sweep.Value;
        public static float SpinDamage => spin.Value;
        public static float ThrowDamage => thrown.Value;
        public static float RearDamage => rear.Value;
        public static int Summons => summons.Value;

        /// <summary>Metres round the blade's edge that the slam and the spin hit.</summary>
        public static float AxeRadius => axeRadius.Value;

        /// <summary>How many times faster than its clip the slam's wind-up plays.</summary>
        public static float SlamWindup => windup.Value;
        public static float AxeheadChance => axehead.Value / 100f;

        public static void Initialize(NativeConfiguration config)
        {
            BindSpawn(config);
            BindFight(config);
        }

        private static void BindSpawn(NativeConfiguration config)
        {
            enabled = config.Bind(Section, "Enabled", true,
                "The Crypt Executioner: a skeleton headsman with a bone greataxe, a mini boss waiting in some of the Black Forest's "
                + "burial chambers. Off: no chamber gets one, and the ones already placed raise none.");
            chambers = config.Bind(Section, "Chambers", 25f,
                "Percent of burial chambers that hold one, decided once per chamber: when it is generated, or for chambers from before the mod when first loaded.", acceptableValues: Settings.Range(0f, 100f));
            respawnDays = config.Bind(Section, "Respawn Days", 3f,
                "Game days before it comes back to its chamber after it died.", acceptableValues: Settings.Range(0.1f, 100f));
            health = config.Bind(Section, "Health", 900f, "Its health before stars (a troll: 600).", acceptableValues: Settings.Range(1f, 100000f));
            axehead = config.Bind(Section, "Axehead Chance", 50f,
                "Percent chance it drops its axehead, from which the Executioner's Greataxe is made.", acceptableValues: Settings.Range(0f, 100f));
        }

        private static void BindFight(NativeConfiguration config)
        {
            slam = config.Bind(Section, "Slam Damage", 60f, "Slash damage of the overhead slam, where the axe lands.", acceptableValues: Settings.Range(0f, 1000f));
            sweep = config.Bind(Section, "Sweep Damage", 35f,
                "Slash damage of the low sweep across its front, where the axe head passes.", acceptableValues: Settings.Range(0f, 1000f));
            spin = config.Bind(Section, "Spin Damage", 50f, "Slash damage of the spin, where its axe head passes.", acceptableValues: Settings.Range(0f, 1000f));
            axeRadius = config.Bind(Section, "Axe Head Radius", ExecutionerReach.DefaultAxeRadius,
                "Meters round the blade's edge that hit in the slam (where the axe lands) and the spin (the ring the head draws; inside it is safe).",
                acceptableValues: Settings.Range(0.1f, 2f));
            windup = config.Bind(Section, "Slam Windup Speed", 1.35f,
                "How much faster than its animation the slam is raised and cocked before it comes down (1 = as animated).",
                acceptableValues: Settings.Range(0.5f, 3f));
            thrown = config.Bind(Section, "Throw Damage", 55f, "Slash damage of the thrown axe.", acceptableValues: Settings.Range(0f, 1000f));
            rear = config.Bind(Section, "Rear Strike Damage", 55f,
                "Slash damage of the strike behind it, used when two or more foes are near and one is behind.", acceptableValues: Settings.Range(0f, 1000f));
            summons = config.Bind(Section, "Summons", 2,
                "Skeletons it may have raised at once from its thrown axe's bones; 0 raises none.", acceptableValues: new AcceptableValueRange<int>(0, 10));
        }
    }
}
