// Portions Copyright (C) 2026 Gerald Glasgow. EliteCreaturesPack 0.7.2 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Changed by expDARE, 2026: Bestiary namespace, owned Crucible settings, rebuilt-only bundles and lifecycle adapters.
// SPDX-License-Identifier: GPL-3.0-or-later
using BepInEx.Configuration;
using ExtraPotions.Bestiary.Runtime.Native.Core;
using ExtraPotions.Bestiary.Runtime.Native.Adapters;

namespace ExtraPotions.Bestiary.Runtime.Native.Mimic
{
    /// <summary>
    /// Section 2: the crypt mimic. Whether crypt chests can be mimics, how many are, which chests, and the three numbers
    /// of its fight that are not the Black Forest skeleton's own - how long its bite takes to recharge, how hard it bites
    /// (the user set 20 slash, under the skeleton sword's 25) and how fast it hops. Everything else (health,
    /// resistances, faction) is the skeleton's. Synced; read where they apply, so a reload takes effect.
    /// </summary>
    public static class MimicSettings
    {
        public const string Section = "2 - Crypt Mimic";

        private static GatedEntry<bool> enabled = null!;
        private static GatedEntry<float> chance = null!;
        private static GatedEntry<string> chests = null!;
        private static GatedEntry<float> biteCooldown = null!;
        private static GatedEntry<float> biteDamage = null!;
        private static GatedEntry<float> runSpeed = null!;

        /// <summary>Whether new mimics may appear. Ones already in the world stay.</summary>
        public static bool On => enabled.Value;

        /// <summary>The share of the listed chests that are mimics, 0 to 1.</summary>
        public static float Chance => chance.Value / 100f;

        public static float BiteCooldown => biteCooldown.Value;
        public static float BiteDamage => biteDamage.Value;
        public static float RunSpeed => runSpeed.Value;

        /// <summary>Whether this chest prefab may be a mimic.</summary>
        public static bool Listed(string chestPrefab) => NameList.Contains(chests.Value, chestPrefab);

        public static void Initialize(NativeConfiguration config)
        {
            enabled = config.Bind(Section, "Enabled", true,
                "Crypt mimics. When a crypt first fills its chests, each listed chest may be a mimic instead: it looks exactly like "
                + "the chest until someone opens it (it bites whoever does) or hits it. Off: no new mimics; ones already in the world stay.");
            chance = config.Bind(Section, "Chance", 15f,
                "Percent of the listed chests that are mimics. Only crypts generated after this is on get them.",
                acceptableValues: Settings.Range(0f, 100f));
            chests = config.Bind(Section, "Chests", "TreasureChest_forestcrypt, TreasureChest_sunkencrypt",
                "The chest prefabs that may be mimics, separated by commas. Hildir's crypt chest (her quest key) and "
                + "TreasureChest_fCrypt (it also stands in fortresses and troll caves) are left out on purpose.");
            BindFight(config);
        }

        private static void BindFight(NativeConfiguration config)
        {
            biteCooldown = config.Bind(Section, "Bite Cooldown", 3f,
                "Seconds after a lunge before it can bite again; meanwhile it hops after you.",
                acceptableValues: Settings.Range(0.5f, 60f));
            biteDamage = config.Bind(Section, "Bite Damage", 20f,
                "Slash damage of its bite (the lunge and the ambush), before stars (the crypt skeleton's sword: 25).",
                acceptableValues: Settings.Range(0f, 1000f));
            runSpeed = config.Bind(Section, "Run Speed", 5f,
                "How fast it bounds after you (the crypt skeleton runs at 4); it wanders at half this.",
                acceptableValues: Settings.Range(0.5f, 10f));
        }
    }
}
