// Bestiary - ExtraPotions Meadworks creatures and bone arsenal.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, GPL-3.0-or-later: EliteCreaturesPack 0.7.2 at
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1 (sections, keys, defaults, ranges and descriptions of the *Settings.cs files).
// Changed by expDARE, 2026: one Unity-free table with Crucible scopes (design section 4); Lock Configuration dropped;
// General and Workshop rows added; wording trimmed where ECP called a value a placeholder; Axe Head Radius defaults to
// 0.6 (ECP 0.5) so the Executioner's slam reaches a player standing in front of it (ExecutionerReach); sections 27 (Bear
// Claws) and 28 (Bone Ballista) from EliteCreaturesPack 0.9.0 at ValheimMods 3bb778330f23645ccddb42fbc0c6ffab72c842b6.
using System.Collections.Generic;
using System.Linq;
using ExtraPotions.Bestiary.Catalog;

namespace ExtraPotions.Bestiary.Config
{
    /// <summary>
    /// Every Bestiary setting of design section 4, in design order. Section names and keys keep ECP's so the converter
    /// is one-to-one. The plugin binds this table through Crucible (<c>MeadworksConfig.Bind</c>) and reads values at use
    /// time through the authority gate. Bestiary has no cheats (design 5): every gameplay setting is ServerAuthoritative.
    /// </summary>
    public static class BestiarySettings
    {
        public const string General = "1 - General", Mimic = "2 - Crypt Mimic", Slinger = "3 - Greydwarf Slinger",
            RimeGiant = "4 - Rime Giant", Kraken = "5 - Kraken", Crossbowman = "6 - Skeleton Crossbowman",
            BoneCrossbow = "7 - Bone Crossbow", Arsenal = "8 - Skeleton Arsenal", Executioner = "25 - Crypt Executioner",
            Greataxe = "26 - Executioner Greataxe", Ballista = "28 - Bone Ballista", Workshop = "30 - Workshop";

        private const SettingScope L = SettingScope.Local, SA = SettingScope.ServerAuthoritative;

        public static IReadOnlyList<SettingSpec> All { get; } = Build();

        private static readonly Dictionary<string, SettingSpec> ById = All.ToDictionary(s => s.Id);

        public static SettingSpec? Find(string id) => ById.TryGetValue(id, out var spec) ? spec : null;

        /// <summary>The spawn switch of one arsenal skeleton title ("Swordsman").</summary>
        public static SettingSpec ArsenalSwitch(string title) => ById[Arsenal + ".Skeleton " + title];

        private static SettingSpec S(string section, string key, object value, string description, double? min = null, double? max = null, SettingScope scope = SA) =>
            new SettingSpec(section, key, scope, value, description, min, max);

        private static SettingSpec[] Build() => new[]
        {
            S(General, "Enabled", true, "Turns all of Bestiary on or off. Off: no Bestiary creature spawns and no recipe is offered; creatures and items already in the world stay."),
            S(General, "Compendium Pages", true, "Shows the Compendium pages for Bestiary's creatures and items (where found, what it does, how to fight it).", scope: L),
            S(General, "Workshop Enabled", true, "The creature workshop: clone and edit creatures, attacks, AI, factions, projectiles and textures from the workshop files."),

            S(Mimic, "Enabled", true, "Crypt mimics. When a crypt first fills its chests, each listed chest may be a mimic instead: it looks exactly like the chest until someone opens it (it bites whoever does) or hits it. Off: no new mimics; ones already in the world stay."),
            S(Mimic, "Chance", 15f, "Percent of the listed chests that are mimics. Only crypts generated after this is on get them.", 0, 100),
            S(Mimic, "Chests", "TreasureChest_forestcrypt, TreasureChest_sunkencrypt", "The chest prefabs that may be mimics, separated by commas. Hildir's crypt chest (her quest key) and TreasureChest_fCrypt (it also stands in fortresses and troll caves) are left out on purpose."),
            S(Mimic, "Bite Cooldown", 3f, "Seconds after a lunge before it can bite again; meanwhile it hops after you.", 0.5, 60),
            S(Mimic, "Bite Damage", 20f, "Slash damage of its bite (the lunge and the ambush), before stars (the crypt skeleton's sword: 25).", 0, 1000),
            S(Mimic, "Run Speed", 5f, "How fast it bounds after you (the crypt skeleton runs at 4); it wanders at half this.", 0.5, 10),

            S(Slinger, "Enabled", true, "Greydwarf slingers: a greydwarf with a slingshot that stands and shoots stones like a skeleton archer, at any range, and never melees. Off: no new slingers; ones already in the world stay."),
            S(Slinger, "Share", 10f, "Percent of the greydwarfs spawning in the biomes below that come as slingers instead.", 0, 100),
            S(Slinger, "Nests", true, "Greydwarf nests spawn them too, not only the wild."),
            S(Slinger, "Biomes", BiomeMask.BlackForest, "Where greydwarfs can come as slingers. Several biomes are separated by commas, like BlackForest, Meadows."),
            S(Slinger, "Shot Interval", 3.5f, "Seconds between its shots at the least.", 0.5, 60),
            S(Slinger, "Stone Damage", 12f, "Blunt damage of a stone, before stars (a greydwarf's thrown rock: 10).", 0, 500),
            S(Slinger, "Stone Speed", 16f, "Meters a second the stone flies (a greydwarf's thrown rock: 12); it arcs onto its target.", 5, 60),
            S(Slinger, "Range", 22f, "Distance in meters at which a Greydwarf Slinger starts throwing stones at a target.", 6, 60),

            S(RimeGiant, "Enabled", true, "Rime giants: a very rare giant of the Mountains, a troll crusted in plates of ice that sleeps by day like a snowy outcrop. At most one per mountain, ever. Off: no new giants; ones already in the world stay."),
            S(RimeGiant, "Mountains", 60f, "Percent of mountains that hold a giant, fixed by the world seed. Snowy hilltops under about 150 m across don't count. Once a mountain's giant has come it never gets another, even after it dies.", 0, 100),
            S(RimeGiant, "Chance", 25f, "Percent per zone roll, while someone roams a mountain whose giant has not come yet.", 0, 100),
            S(RimeGiant, "Interval", 3600f, "Seconds between a zone's rolls.", 60, 86400),
            S(RimeGiant, "Biomes", BiomeMask.Mountain, "The biomes whose regions count as mountains. Several are separated by commas."),
            S(RimeGiant, "Health", 1500f, "Its health before stars (a Troll: 600).", 1, 100000),
            S(RimeGiant, "Plates", 8, "Plates of ice it wears, 0 to 8.", 0, 8),
            S(RimeGiant, "Armored Damage", 15f, "Percent of a physical hit that gets through with every plate on; each plate lost lets an equal part more through.", 0, 100),
            S(RimeGiant, "Shatter At", 40f, "Percent of its health at which the plates left all break off.", 0, 100),
            S(RimeGiant, "Fire Per Plate", 30f, "Fire damage that breaks one plate (a fire arrow against its weakness: about 33).", 1, 1000),
            S(RimeGiant, "Regrow Delay", 12f, "Seconds unhurt, out of the fight, before the plates grow back.", 0, 600),
            S(RimeGiant, "Regrow Interval", 5f, "Seconds per plate as they grow back.", 0.5, 600),
            S(RimeGiant, "Sweep Damage", 75f, "Blunt damage of its fist, with heavy knockback (a troll's punch: 60).", 0, 1000),
            S(RimeGiant, "Slam Damage", 60f, "Blunt damage where its fists land in the slam.", 0, 1000),
            S(RimeGiant, "Avalanche Damage", 45f, "Blunt damage, plus half as much frost, where the slam's wave rolls over you.", 0, 1000),
            S(RimeGiant, "Avalanche Length", 24f, "Meters the wave rolls on flat ground; far further downhill, a few meters uphill. 0 turns the avalanche off.", 0, 60),
            S(RimeGiant, "Boulder Damage", 40f, "Frost damage of its ice boulder in the burst where it lands, plus half as much blunt.", 0, 1000),

            S(Kraken, "Enabled", true, "Krakens: a boss of the deep ocean that hunts ships. Outrun it under sail, or it grabs the ship from below and attacks with six tentacles and then its head. Off: no new krakens; one already in the world stays."),
            S(Kraken, "Chance", 0.3f, "Percent per zone roll while a ship with a crew crosses deep ocean. At 0.3 a crew sailing the open sea meets one about every hour or two.", 0, 100),
            S(Kraken, "Interval", 3600f, "Seconds between a zone's rolls.", 60, 86400),
            S(Kraken, "Min Depth", 25f, "Meters of water it needs: it only spawns over water this deep and never follows a ship into shallower water.", 6, 200),
            S(Kraken, "Health", 3000f, "Its health (Bonemass: 5000, a serpent: 400). The game adds 30% for every other player within 100 m, as for any creature.", 1, 1000000),
            S(Kraken, "Swim Speed", 3.5f, "Meters a second as it chases. Rowing (about 2) never escapes it; a longship or karve under sail with a fair wind does.", 0.5, 20),
            S(Kraken, "Hunt Range", 90f, "Meters at which it notices a ship. It gives up once the ship is further than this.", 20, 300),
            S(Kraken, "Attack Gap Min", 2.5f, "Seconds from one attack to the next at the least. It attacks one thing at a time: a slam, a bite, ink or a smash.", 0.5, 30),
            S(Kraken, "Attack Gap Max", 4f, "Seconds from one attack to the next at the most; each gap is a random time between the two.", 0.5, 30),
            S(Kraken, "Gap Factor Solo", 1.1f, "The gap between attacks is this many times longer with one player aboard (three or more: the gap as set).", 0.5, 5),
            S(Kraken, "Gap Factor Pair", 1.2f, "The gap between attacks is this many times longer with two players aboard.", 0.5, 5),
            S(Kraken, "Tentacle Gap Factor", 0.7f, "Under the ship the slams come quicker: the gap between them is this many times the attack gap.", 0.2, 3),
            S(Kraken, "Phase Attacks", 6, "Attacks in each phase: this many tentacle slams under the ship, then this many from the head beside it, and round again.", 1, 30),
            S(Kraken, "Slam Damage", 60f, "Blunt damage of a tentacle slamming across the deck.", 0, 1000),
            S(Kraken, "Ship Damage", 40f, "Damage to the ship each time a tentacle smashes it in the head phase (a longship has 1000 health, a karve 500).", 0, 10000),
            S(Kraken, "Ship Hit Interval", 10f, "Seconds between tentacle smashes on the ship while the head is up; a smash takes the next attack's turn.", 2, 120),
            S(Kraken, "Bite Damage", 80f, "Pierce damage of its beak, at players near the rail.", 0, 1000),
            S(Kraken, "Ink Damage", 15f, "Blunt damage of the ink it spits.", 0, 1000),
            S(Kraken, "Ink Blind", 1.5f, "Seconds the ink covers the screen of a player it hits (dodge or block it and it doesn't), before it fades.", 0, 10),
            S(Kraken, "Ink Interval", 6f, "Seconds between its ink spits at the least.", 1, 60),
            S(Kraken, "Shield Parry Damage", 60f, "Pierce damage a creature takes when you parry its blow with the Kraken shield (made from the kraken's beak), at quality 1. Players are never bitten.", 0, 10000),
            S(Kraken, "Shield Parry Damage Per Level", 10f, "Added to the parry damage for each quality level of the shield above the first.", 0, 1000),

            S(Crossbowman, "Damage Factor", 1f, "A bolt's damage, as blunt, against the bow of the skeleton archer it replaces (20), before stars. 1: the same as that archer.", 0, 10),
            S(Crossbowman, "Shot Interval", 6f, "Seconds between its shots at the least (the skeleton archer's: 4). The whole shot, spanning and loading included, takes about 5.5 of them.", 3, 60),
            S(Crossbowman, "Bolt Speed", 40f, "Meters a second the bolt flies, straight (the skeleton archer's arrow: 30).", 10, 120),
            S(Crossbowman, "Range", 25f, "Meters it shoots from (the skeleton archer: 20).", 6, 60),

            S(BoneCrossbow, "Craftable", true, "The Bone Crossbow, the crossbowmen's crossbow of bones, can be made at the workbench."),
            S(BoneCrossbow, "Recipe", "ECP_Spine:1, Wood:10:5, BoneFragments:12:6, LeatherScraps:4:2", "What it costs, as item:amount:amount per upgrade, separated by commas. Item names are the game's prefab names, like BoneFragments, Wood, DeerHide, TrophySkeleton; the spine is ECP_Spine."),
            S(BoneCrossbow, "Workbench Level", 2, "The workbench level it needs (its Blunted Bone Bolts too).", 1, 5),
            S(BoneCrossbow, "Damage", 30f, "Blunt damage of its own blow, before the bolt's (a Blunted Bone Bolt adds 27 blunt, the game's bone bolt 32 pierce; the Arbalest: 200 pierce).", 0, 500),
            S(BoneCrossbow, "Damage Per Level", 4f, "Blunt damage added by each upgrade.", 0, 100),
            S(BoneCrossbow, "Reload Time", 2.3f, "Seconds to span and load it with no Crossbows skill; the skill halves it (the Arbalest: 3.5).", 0.5, 20),
            S(BoneCrossbow, "Bolt Recipe", "BoneFragments:8", "What a batch of Blunted Bone Bolts costs, as item:amount, separated by commas; made at the crossbow's workbench level."),
            S(BoneCrossbow, "Bolts Per Craft", 20, "Blunted Bone Bolts made at once.", 1, 100),
            S(BoneCrossbow, "Bolt Damage", 27f, "Blunt damage a Blunted Bone Bolt adds to the crossbow's own: a flint arrow's, as blunt (flint arrows 27 pierce; the game's bone bolt 32 pierce).", 0, 500),

            S(Arsenal, "Enabled", true, "All the arsenal skeletons, the crossbowman too, the master switch. Off: none of them spawns, whatever their own switches below say; ones already in the world stay."),
            ArsenalRow("Cutthroat", "bone dagger"),
            ArsenalRow("Swordsman", "bone sword"),
            ArsenalRow("Axeman", "bone axe"),
            ArsenalRow("Bonebreaker", "bone mace"),
            ArsenalRow("Spearman", "bone spear"),
            ArsenalRow("Halberdier", "bone atgeir"),
            ArsenalRow("Bowman", "bone bow"),
            S(Arsenal, "Skeleton Crossbowman", true, "The Skeleton Crossbowman (bone crossbow, blunt bone bolts; its shot's numbers are in section 6) spawns in place of some of the game's skeletons that could be archers, wherever and whenever they spawn. Off: no new ones; ones already in the world stay."),

            S(Executioner, "Enabled", true, "The Crypt Executioner: a skeleton headsman with a bone greataxe, a mini boss waiting in some of the Black Forest's burial chambers. Off: no chamber gets one, and the ones already placed raise none."),
            S(Executioner, "Chambers", 25f, "Percent of burial chambers that hold one, decided once per chamber: when it is generated, or for chambers from before the mod when first loaded.", 0, 100),
            S(Executioner, "Respawn Days", 3f, "Game days before it comes back to its chamber after it died.", 0.1, 100),
            S(Executioner, "Health", 900f, "Its health before stars (a troll: 600).", 1, 100000),
            S(Executioner, "Axehead Chance", 50f, "Percent chance it drops its axehead, from which the Executioner's Greataxe is made.", 0, 100),
            S(Executioner, "Slam Damage", 60f, "Slash damage of the overhead slam, where the axe lands.", 0, 1000),
            S(Executioner, "Sweep Damage", 35f, "Slash damage of the low sweep across its front, where the axe head passes.", 0, 1000),
            S(Executioner, "Spin Damage", 50f, "Slash damage of the spin, where its axe head passes.", 0, 1000),
            S(Executioner, "Throw Damage", 55f, "Slash damage of the thrown axe.", 0, 1000),
            S(Executioner, "Rear Strike Damage", 55f, "Slash damage of the strike behind it, used when two or more foes are near and one is behind.", 0, 1000),
            S(Executioner, "Axe Head Radius", ExecutionerReach.DefaultAxeRadius, "Meters round the blade's edge that hit in the slam (where the axe lands) and the spin (the ring the head draws; inside it is safe).", 0.1, 2),
            S(Executioner, "Slam Windup Speed", 1.35f, "How much faster than its animation the slam is raised and cocked before it comes down (1 = as animated).", 0.5, 3),
            S(Executioner, "Summons", 2, "Skeletons it may have raised at once from its thrown axe's bones; 0 raises none.", 0, 10),

            S(Greataxe, "Recipe", true, "Whether the greataxe can be made at the workbench."),
            S(Greataxe, "Slash", 55f, "Slash damage (a fully upgraded Bronze Axe: 55).", 0, 1000),
            S(Greataxe, "Chop", 49f, "Chop damage, for trees (a fully upgraded Bronze Axe: 49).", 0, 1000),
            S(Greataxe, "Spines", 1, "Spines (the bone weapons' drop from skeletons) the recipe takes besides the axehead; without the skeleton arsenal, bone fragments take their place.", 0, 100),
            S(Greataxe, "Bone Fragments", 10, "Bone fragments the recipe takes.", 0, 100),
            S(Greataxe, "Workbench Level", 3, "Workbench level required to craft the Executioner Greataxe.", 1, 5),


            S(Ballista, "Enabled", true, "A small ballista of bones in the hammer's Misc. tab beside the game's ballista (3 spines, 25 bone fragments), and its Bone Missiles at the workbench (level 2: 5 bone fragments and 2 feathers make 20). Hold it with the use key, aim with the mouse (45 degrees either way), shoot with attack; it reloads from the missiles you carry. Off: neither can be made; ballistas already built stay and still shoot."),

            S(Workshop, "Data Folder", "BepInEx/config/Meadworks/bestiary/workshop", "Folder of the workshop files (creatures, attacks, AI, factions, projectiles, localization, textures), relative to the game folder and inside BepInEx/config."),
            S(Workshop, "Hot Reload", true, "Workshop files reload when saved; creatures already loaded keep what they have completed."),
            S(Workshop, "Texture Sync", true, "The server sends its workshop textures to clients, which keep them in a cache by content hash."),
            S(Workshop, "Texture Cache Limit", 256, "Megabytes of synced workshop textures this client keeps.", 16, 4096, L),
        };

        private static SettingSpec ArsenalRow(string title, string weapon) =>
            S(Arsenal, "Skeleton " + title, true, $"The Skeleton {title} ({weapon}) spawns in place of some of the game's skeletons, wherever and whenever they spawn. Off: no new ones; ones already in the world stay.");
    }
}
