// Delve - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original. Setting names, defaults and ranges follow design section 4; the source names they mirror
// (RestockRegen, Max_Dungeon_Rooms, MoreDungeonResets, Valheim_Admin_Tool, Elite Creatures Reborn) are recorded in the
// design's Source column and in the converter (names only; descriptions are Delve's own).
using System;
using System.Collections.Generic;
using System.Linq;
using ExtraPotions.Delve.Engine;
using static ExtraPotions.Delve.Config.SettingScope;

namespace ExtraPotions.Delve.Config
{
    /// <summary>The settings one regeneration kind runs on: its switch, clock, delay and dry run, plus radius and clearance for world objects.</summary>
    public sealed class KindSettings
    {
        internal KindSettings(DelveKind kind, DelveSetting<bool> enabled, DelveSetting<ClockKind> clock, DelveSetting<float> delay, DelveSetting<bool> dryRun,
            DelveSetting<float>? visitRadius = null, DelveSetting<float>? buildClearance = null)
        {
            Kind = kind;
            Enabled = enabled;
            Clock = clock;
            Delay = delay;
            DryRun = dryRun;
            VisitRadius = visitRadius;
            BuildClearance = buildClearance;
        }

        public DelveKind Kind { get; }
        public DelveSetting<bool> Enabled { get; }
        public DelveSetting<ClockKind> Clock { get; }
        public DelveSetting<float> Delay { get; }
        public DelveSetting<bool> DryRun { get; }

        /// <summary>World objects only (design 4.6): a player this close to a spot counts as a visit.</summary>
        public DelveSetting<float>? VisitRadius { get; }

        /// <summary>World objects only (design 4.6): nothing returns where a player built this close; 0 turns the check off.</summary>
        public DelveSetting<float>? BuildClearance { get; }

        public IEnumerable<DelveSetting> Entries()
        {
            yield return Enabled;
            yield return Clock;
            yield return Delay;
            yield return DryRun;
            if (VisitRadius != null) yield return VisitRadius;
            if (BuildClearance != null) yield return BuildClearance;
        }
    }

    /// <summary>Every Delve setting (design section 4): ServerAuthoritative (the authority's own) plus one server-bound cheat.</summary>
    public static class DelveSettings
    {
        /// <summary>Clock delays: the setting range covers both clocks; <c>ClockRules.Clamp</c> applies the clock's own range.</summary>
        public const float MinDelay = 0.25f;
        public const float MaxDelay = 87600f;

        /// <summary>The C05 languages (Crucible <c>MeadworksLanguages.Supported</c>), for plain-text notices to clients without Crucible.</summary>
        public static IReadOnlyList<string> Languages { get; } = Array.AsReadOnly(new[] { "English", "Korean", "Spanish", "Chinese", "Russian", "French", "German" });

        public static class General
        {
            const string Section = "General";
            public static readonly DelveSetting<bool> Enabled = DelveSetting.Toggle(Section, "Enabled", true, ServerAuthoritative,
                "Off stops every Delve regeneration and room count change. Nothing already restored is undone.");
            public static readonly DelveSetting<bool> DryRunAll = DelveSetting.Toggle(Section, "Dry Run All", false, ServerAuthoritative,
                "On: every kind only reports what it would do and changes nothing (visits and remembered spots are still recorded).");
            public static readonly DelveSetting<int> CheckIntervalSeconds = DelveSetting.Integer(Section, "Check Interval Seconds", 30, 5, 300, ServerAuthoritative,
                "How often Delve checks visits and real-time clocks. In-game day clocks are also checked at each dawn.");
            public static readonly DelveSetting<int> MaxActionsPerTick = DelveSetting.Integer(Section, "Max Actions Per Tick", 4, 1, 64, ServerAuthoritative,
                "At most this many sites are restored or reset per check, so the server never stalls; the rest wait for the next check.");
            public static readonly DelveSetting<bool> PlayerBaseGuard = DelveSetting.Toggle(Section, "Player Base Guard", true, ServerAuthoritative,
                "Never touch a site inside a player base or a ward area.");
            public static readonly DelveSetting<bool> KickCommandFallback = DelveSetting.Toggle(Section, "Kick Command Fallback", true, ServerAuthoritative,
                "Admins without Crucible can send Delve commands through the game's kick console command.");
            public static readonly DelveSetting<int> StatusReportLines = DelveSetting.Integer(Section, "Status Report Lines", 20, 5, 200, ServerAuthoritative,
                "Lines of recent actions the status report keeps.");
        }

        public static class Rooms
        {
            const string Section = "Rooms";
            public static readonly DelveSetting<bool> Enabled = DelveSetting.Toggle(Section, "Room Counts Enabled", true, ServerAuthoritative,
                "Off leaves every dungeon generator as the game made it. Only new dungeons and Delve full resets use room counts.");
            public static readonly DelveSetting<int> MinRooms = MinOf(Section, "Min Rooms", 20, "every vanilla dungeon except Hildir's");
            public static readonly DelveSetting<int> MaxRooms = MaxOf(Section, "Max Rooms", 40, "every vanilla dungeon except Hildir's");
            public static readonly DelveSetting<bool> ForestCryptOverride = Override(Section, "Forest Crypt", "Burial Chambers");
            public static readonly DelveSetting<int> ForestCryptMin = MinOf(Section, "Forest Crypt Min Rooms", 20, "the Burial Chambers");
            public static readonly DelveSetting<int> ForestCryptMax = MaxOf(Section, "Forest Crypt Max Rooms", 40, "the Burial Chambers");
            public static readonly DelveSetting<bool> SunkenCryptOverride = Override(Section, "Sunken Crypt", "Sunken Crypt");
            public static readonly DelveSetting<int> SunkenCryptMin = MinOf(Section, "Sunken Crypt Min Rooms", 20, "a Sunken Crypt");
            public static readonly DelveSetting<int> SunkenCryptMax = MaxOf(Section, "Sunken Crypt Max Rooms", 30, "a Sunken Crypt");
            public static readonly DelveSetting<bool> CaveOverride = Override(Section, "Cave", "Frost Caves");
            public static readonly DelveSetting<int> CaveMin = MinOf(Section, "Cave Min Rooms", 20, "the Frost Caves");
            public static readonly DelveSetting<int> CaveMax = MaxOf(Section, "Cave Max Rooms", 40, "the Frost Caves");

            static DelveSetting<bool> Override(string section, string name, string gameName) => DelveSetting.Toggle(section, name + " Override", false, ServerAuthoritative,
                "On: " + gameName + " dungeons use their own Min and Max below instead of the general pair.");

            static DelveSetting<int> MinOf(string section, string key, int value, string what) => DelveSetting.Integer(section, key, value, 10, 120, ServerAuthoritative,
                "The generator stops placing rooms once all required rooms exist and more than this many are placed, in " + what + ". Capped at 120 (larger values have corrupted worlds).");

            static DelveSetting<int> MaxOf(string section, string key, int value, string what) => DelveSetting.Integer(section, key, value, 10, 1000, ServerAuthoritative,
                "Attempts the generator makes to place rooms in " + what + ". Never below the Min.");
        }

        public static class Dungeons
        {
            const string Section = "Dungeons";
            public static readonly DelveSetting<ClockKind> Clock = ClockOf(Section, "Full Reset Clock");
            public static readonly DelveSetting<float> Delay = DelayOf(Section, "Full Reset Delay", 30f, "since the last visit before a dungeon set to Full in Delve.Dungeons.yml is wiped and generated again");
            public static readonly DelveSetting<bool> DryRun = DryRunOf(Section, "Full Reset Dry Run");
            public static readonly DelveSetting<bool> BuildClearance = DelveSetting.Toggle(Section, "Full Reset Build Clearance", true, ServerAuthoritative,
                "Any player piece inside a dungeon interior blocks its full reset; it is checked again at the next evaluation.");
            public static readonly DelveSetting<bool> WarnAtEntrance = DelveSetting.Toggle(Section, "Warn At Entrance", true, ServerAuthoritative,
                "Tell players at the entrance when the dungeon resets and that anything left inside is lost.");
        }

        public static class Chests
        {
            const string Section = "Chests";
            public const string DefaultExclude = "TreasureChest_forestcrypt_hildir,TreasureChest_mountaincave_hildir,TreasureChest_plainsfortress_hildir";
            public static readonly DelveSetting<bool> Enabled = DelveSetting.Toggle(Section, "Chests", true, ServerAuthoritative,
                "Loot chests (world chests with a loot table) refill with a fresh roll after the delay. Player-built chests are never touched.");
            public static readonly DelveSetting<ClockKind> Clock = ClockOf(Section, "Chest Clock");
            public static readonly DelveSetting<float> Delay = DelayOf(Section, "Chest Delay", 30f, "from the first opening (or from being found empty) to the refill");
            public static readonly DelveSetting<bool> ResetOpened = DelveSetting.Toggle(Section, "Reset Opened", true, ServerAuthoritative,
                "On: an opened chest refills after the delay even with leftovers inside, which are replaced. Off: only empty chests are on a clock.");
            public static readonly DelveSetting<bool> RestockLoaded = DelveSetting.Toggle(Section, "Restock Loaded", true, ServerAuthoritative,
                "Refill chests a player has loaded too, unless someone has the chest open. Chests near a base are loaded most of the time.");
            public static readonly DelveSetting<bool> DryRun = DryRunOf(Section, "Chest Dry Run");
            public static readonly DelveSetting<string> Exclude = DelveSetting.Text(Section, "Exclude", DefaultExclude, ServerAuthoritative,
                "Chest prefabs never refilled, separated by commas (Hildir's quest chests by default). Changes apply at the next chest check or observation without restarting.", NormalizeList);
            public static readonly DelveSetting<bool> History = DelveSetting.Toggle(Section, "History", true, ServerAuthoritative,
                "Tell the next opener who opened the chest first and last, and when.");
        }

        /// <summary>User decision R-Q2 (2026-10-08): a per-spot build clearance for dungeon resource regrow.</summary>
        public static class DungeonResources
        {
            public static readonly DelveSetting<float> BuildClearance = DelveSetting.Number("Dungeon Resources", "Resource Build Clearance", 8f, 0f, 64f, ServerAuthoritative,
                "A dungeon resource does not return where a player built this close to its spot (meters, flat); 0 turns the check off.");
        }

        /// <summary>User decision R-Q5 (2026-10-08): the spawner reset's player radius is a server setting.</summary>
        public static class CreatureRespawn
        {
            public static readonly DelveSetting<float> PlayerRadius = DelveSetting.Number("Creature Respawn", "Respawn Player Radius", 100f, 10f, 500f, ServerAuthoritative,
                "A cleared spawner is not reset while a player is this close (meters, flat).");
        }

        public static class Bosses
        {
            const string Section = "Bosses";
            public static readonly DelveSetting<bool> QueenReturn = DelveSetting.Toggle(Section, "Queen Return", true, ServerAuthoritative,
                "The Queen returns to her citadel after her death and the delay without visitors. A living Queen's citadel is never touched.");
            public static readonly DelveSetting<ClockKind> QueenClock = ClockOf(Section, "Queen Clock");
            public static readonly DelveSetting<float> QueenDelay = DelayOf(Section, "Queen Delay", 30f, "without visitors before the Queen returns");
            public static readonly DelveSetting<bool> QueenDryRun = DryRunOf(Section, "Queen Dry Run");
            public static readonly DelveSetting<bool> CloseSealedDoor = DelveSetting.Toggle(Section, "Close Sealed Door", true, ServerAuthoritative,
                "Lock the citadel's sealed door again when the Queen returns.");
            public static readonly DelveSetting<bool> KallReturn = DelveSetting.Toggle(Section, "Kall Return", true, ServerAuthoritative,
                "Kall Fimbulbringer returns asleep to his Deep North boss room after his death and the delay without visitors. A room with Kall alive in any phase, or with a player inside, is never touched.");
            public static readonly DelveSetting<ClockKind> KallClock = ClockOf(Section, "Kall Clock");
            public static readonly DelveSetting<float> KallDelay = DelayOf(Section, "Kall Delay", 30f, "without visitors before Kall returns");
            public static readonly DelveSetting<bool> KallDryRun = DryRunOf(Section, "Kall Dry Run");
        }

        public static class Surface
        {
            const string Section = "Surface";
            public static readonly DelveSetting<bool> AllowSurfaceReset = DelveSetting.Toggle(Section, "Allow Surface Reset", true, ServerAuthoritative,
                "Admins may bring back felled trees, bushes, mined rocks and ore, and picked stones, flint and branches in generated zones around them by command (delve-surface, Thing's Reset resources and Regenerate nature). Never automatic.");
            public static readonly DelveSetting<float> BaseClearance = DelveSetting.Number(Section, "Surface Base Clearance", 50f, 0f, 200f, ServerAuthoritative,
                "Meters (flat) around anything a player built where a surface reset brings nothing back; ward and base areas always count. 0 turns the building check off.");
            public static readonly DelveSetting<float> Radius = DelveSetting.Number(Section, "Surface Reset Radius", 64f, 16f, 256f, ServerAuthoritative,
                "A surface reset covers the generated zones this many meters around the admin who asks for it (zones must be loaded on the server).");
        }

        public static class Notices
        {
            const string Section = "Notices";
            public static readonly DelveSetting<bool> Notify = DelveSetting.Toggle(Section, "Notify", true, ServerAuthoritative,
                "Tell players what comes back and when (chests, dungeon entrances, biome entry). Off silences every Delve notice.");
            public static readonly DelveSetting<float> BiomeCooldownMinutes = DelveSetting.Number(Section, "Biome Notice Cooldown Minutes", 10f, 1f, 120f, ServerAuthoritative,
                "A player is told about a biome's regeneration at most once in this many minutes.");
            public static readonly DelveSetting<string> ServerLanguage = DelveSetting.Text(Section, "Server Language", "English", ServerAuthoritative,
                "Language of plain-text notices for players without Crucible: English, Korean, Spanish, Chinese, Russian, French or German.", NormalizeLanguage);
        }

        /// <summary>
        /// User decision 2026-10-08 (a change from design 5): one cheat. Cheat scope is server-bound (the authority's value
        /// applies everywhere), safe value off, Mischief category World (it changes the shared world).
        /// </summary>
        public static class Cheats
        {
            public const string Category = "World";
            public static readonly DelveSetting<bool> RegenerateWithoutWaiting = DelveSetting.Toggle("Cheats", "Regenerate Without Waiting", false, Cheat,
                "On: an admin's regenerate-now command brings a site back at once, without waiting for its delay. Players near and player buildings still block it.",
                safe: false);
        }

        public static class Import
        {
            public static readonly DelveSetting<string> ImportedSources = DelveSetting.Text("Import", "Imported Sources", "", Local,
                "Managed by Delve: source configs already imported.", v => (v ?? "").Trim(), hidden: true);
        }

        static DelveSetting<ClockKind> ClockOf(string section, string key) => DelveSetting.Choice(section, key, ClockKind.InGameDays, ServerAuthoritative,
            "InGameDays counts world days since the trigger; RealHours counts wall-clock hours (UTC), also while the server is off.");

        static DelveSetting<float> DelayOf(string section, string key, float value, string what) => DelveSetting.Number(section, key, value, MinDelay, MaxDelay, ServerAuthoritative,
            "Delay in the clock's unit " + what + ". In-game days 0.25 to 3650, real hours 0.25 to 87600.");

        static DelveSetting<bool> DryRunOf(string section, string key) => DelveSetting.Toggle(section, key, false, ServerAuthoritative,
            "On: only report what would come back; change nothing.");

        static KindSettings Clocked(DelveKind kind, string section, string name, bool on, float delay, string what, bool world = false) => new KindSettings(kind,
            DelveSetting.Toggle(section, name, on, ServerAuthoritative, "Bring back " + what + "."),
            ClockOf(section, name + " Clock"),
            DelayOf(section, name + " Delay", delay, "before " + what + " come back"),
            DryRunOf(section, name + " Dry Run"),
            world ? DelveSetting.Number(section, name + " Visit Radius", 100f, 10f, 500f, ServerAuthoritative, "A player this close to a spot (meters, flat) counts as a visit and restarts its clock.") : null,
            world ? DelveSetting.Number(section, name + " Build Clearance", 8f, 0f, 64f, ServerAuthoritative, "Nothing returns where a player built this close (meters); 0 turns the check off.") : null);

        private static readonly Lazy<KindSettings[]> kinds = new Lazy<KindSettings[]>(() => new[]
        {
            // Full resets have no switch of their own (design 4.3): Delve.Dungeons.yml modes decide per dungeon, under General.Enabled.
            new KindSettings(DelveKind.FullReset, General.Enabled, Dungeons.Clock, Dungeons.Delay, Dungeons.DryRun),
            new KindSettings(DelveKind.Chests, Chests.Enabled, Chests.Clock, Chests.Delay, Chests.DryRun),
            Clocked(DelveKind.MudPiles, "Dungeon Resources", "Mud Piles", true, 30f, "Sunken Crypt Muddy Scrap Piles"),
            Clocked(DelveKind.FrostCaveCrystals, "Dungeon Resources", "Frost Cave Crystals", true, 30f, "Frost Caves crystals"),
            Clocked(DelveKind.BlackCores, "Dungeon Resources", "Black Cores", true, 30f, "infested mine black cores"),
            Clocked(DelveKind.Morkhalla, "Dungeon Resources", "Morkhalla", true, 30f, "Morkhalla gems, coin piles and the black ice"),
            Clocked(DelveKind.Hole, "Dungeon Resources", "Hole", true, 30f, "the Hole's nests and trash piles"),
            Clocked(DelveKind.AncientArmor, "World Objects", "Ancient Armor", true, 30f, "Mistlands ancient armor", world: true),
            Clocked(DelveKind.Obsidian, "World Objects", "Obsidian", true, 30f, "Mountain obsidian", world: true),
            Clocked(DelveKind.Flametal, "World Objects", "Flametal", true, 30f, "Ashlands flametal spires", world: true),
            Clocked(DelveKind.Ice, "World Objects", "Ice", true, 30f, "Deep North ice ponds and shore ice", world: true),
            new KindSettings(DelveKind.Queen, Bosses.QueenReturn, Bosses.QueenClock, Bosses.QueenDelay, Bosses.QueenDryRun),
            Clocked(DelveKind.CampSpawners, "Creature Respawn", "Camps", false, 5f, "the creatures of cleared camps and villages (one-shot spawners outside dungeons)"),
            Clocked(DelveKind.DungeonSpawners, "Creature Respawn", "Dungeon Spawners", false, 7f, "the creatures of cleared dungeons (one-shot spawners inside interiors)"),
            new KindSettings(DelveKind.Kall, Bosses.KallReturn, Bosses.KallClock, Bosses.KallDelay, Bosses.KallDryRun),
        });

        /// <summary>The settings of one kind.</summary>
        public static KindSettings Kind(DelveKind kind) => kinds.Value[(int)kind];

        /// <summary>The five dungeon resource kinds (design 4.5).</summary>
        public static IReadOnlyList<KindSettings> Resources => kinds.Value.Skip((int)DelveKind.MudPiles).Take(5).ToArray();

        /// <summary>The four world object kinds (design 4.6).</summary>
        public static IReadOnlyList<KindSettings> WorldObjects => kinds.Value.Skip((int)DelveKind.AncientArmor).Take(4).ToArray();

        private static readonly Lazy<DelveSetting[]> all = new Lazy<DelveSetting[]>(() => new DelveSetting[]
            {
                General.Enabled, General.DryRunAll, General.CheckIntervalSeconds, General.MaxActionsPerTick, General.PlayerBaseGuard,
                General.KickCommandFallback, General.StatusReportLines,
                Rooms.Enabled, Rooms.MinRooms, Rooms.MaxRooms, Rooms.ForestCryptOverride, Rooms.ForestCryptMin, Rooms.ForestCryptMax,
                Rooms.SunkenCryptOverride, Rooms.SunkenCryptMin, Rooms.SunkenCryptMax, Rooms.CaveOverride, Rooms.CaveMin, Rooms.CaveMax,
                Dungeons.Clock, Dungeons.Delay, Dungeons.DryRun, Dungeons.BuildClearance, Dungeons.WarnAtEntrance,
                Chests.Enabled, Chests.Clock, Chests.Delay, Chests.ResetOpened, Chests.RestockLoaded, Chests.DryRun, Chests.Exclude, Chests.History,
            }
            .Concat(Resources.SelectMany(k => k.Entries()))
            .Concat(new DelveSetting[] { DungeonResources.BuildClearance })
            .Concat(WorldObjects.SelectMany(k => k.Entries()))
            .Concat(new DelveSetting[]
            {
                Bosses.QueenReturn, Bosses.QueenClock, Bosses.QueenDelay, Bosses.QueenDryRun, Bosses.CloseSealedDoor,
                Bosses.KallReturn, Bosses.KallClock, Bosses.KallDelay, Bosses.KallDryRun,
                Surface.AllowSurfaceReset, Surface.BaseClearance, Surface.Radius,
                Notices.Notify, Notices.BiomeCooldownMinutes, Notices.ServerLanguage,
                Cheats.RegenerateWithoutWaiting,
            })
            .Concat(Kind(DelveKind.CampSpawners).Entries())
            .Concat(Kind(DelveKind.DungeonSpawners).Entries())
            .Concat(new DelveSetting[] { CreatureRespawn.PlayerRadius })
            .Concat(new DelveSetting[] { Import.ImportedSources })
            .ToArray());

        private static readonly Lazy<Dictionary<string, DelveSetting>> byId = new Lazy<Dictionary<string, DelveSetting>>(
            () => all.Value.ToDictionary(s => s.Id, StringComparer.OrdinalIgnoreCase));

        /// <summary>Every setting the plugin binds, in file order (the hidden import marker last).</summary>
        public static IReadOnlyList<DelveSetting> All => all.Value;

        /// <summary>The settings configuration UIs list.</summary>
        public static IReadOnlyList<DelveSetting> Visible => all.Value.Where(s => !s.Hidden).ToArray();

        public static DelveSetting? Find(string? id) => id != null && byId.Value.TryGetValue(id.Trim(), out var setting) ? setting : null;

        /// <summary>A comma list with each entry trimmed and empty entries dropped.</summary>
        public static string NormalizeList(string? value) =>
            string.Join(",", (value ?? "").Split(',').Select(p => p.Trim()).Where(p => p.Length > 0));

        /// <summary>One of <see cref="Languages"/> (any case), else English.</summary>
        public static string NormalizeLanguage(string? value)
        {
            var trimmed = (value ?? "").Trim();
            return Languages.FirstOrDefault(l => string.Equals(l, trimmed, StringComparison.OrdinalIgnoreCase)) ?? "English";
        }
    }
}
