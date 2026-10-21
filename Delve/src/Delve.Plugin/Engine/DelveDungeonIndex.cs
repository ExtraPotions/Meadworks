// Delve - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: the dungeon generators of the world and the mode of each type (Delve.Dungeons.yml, design 4.3 and
// user decision Q3), read on the authority. Chests inside a dungeon set to Full are left to the full reset; resource
// kinds act only in Resources dungeons.
using System;
using System.Collections.Generic;
using System.IO;
using ExtraPotions.Delve.Data;
using ExtraPotions.Delve.Engine;
using UnityEngine;

namespace ExtraPotions.Delve.Runtime
{
    public static class DelveDungeonIndex
    {
        /// <summary>Character.InInterior: interiors are built above this height.</summary>
        public const float InteriorHeight = 3000f;

        private static readonly Dictionary<int, string> generatorNames = new Dictionary<int, string>();
        private static readonly List<(ZDO Zdo, string Name)> generators = new List<(ZDO, string)>();
        private static DungeonsFile modes = DungeonsFile.Empty;
        private static DateTime modesStamp = DateTime.MinValue;
        private static int builtFrame = -1;

        public static string ModesPath => Path.Combine(DelveImport.DataDirectory, DungeonsFile.FileName);

        public static void Index()
        {
            generatorNames.Clear();
            generators.Clear();
            builtFrame = -1;
            if (ZNetScene.instance == null) return;
            foreach (GameObject prefab in ZNetScene.instance.m_prefabs)
                if (prefab != null && prefab.GetComponent<DungeonGenerator>() != null)
                    generatorNames[prefab.name.GetStableHashCode()] = prefab.name;
        }

        /// <summary>Adds every indexed generator type the file does not list yet (modded dungeons too), at its default mode.</summary>
        public static void ListAll()
        {
            try
            {
                var path = ModesPath;
                var current = File.Exists(path) ? DungeonsFile.Parse(File.ReadAllText(path)) : DungeonsFile.Empty;
                if (current.Errors.Count > 0) return; // a rewrite would drop the lines the owner still has to fix
                var all = DungeonsFile.WithMissing(current.Modes, generatorNames.Values);
                if (all == null) return;
                Directory.CreateDirectory(DelveImport.DataDirectory);
                File.WriteAllText(path, DungeonsFile.Write(all));
                Plugin.Log?.Info("Delve " + DungeonsFile.FileName + ": listed " + (all.Count - current.Modes.Count) + " more dungeon types.");
            }
            catch (Exception error)
            {
                Plugin.Log?.Warning("Delve " + DungeonsFile.FileName + " not updated: " + error.GetType().Name + ": " + error.Message);
            }
        }

        public static bool IsGenerator(int prefab) => generatorNames.ContainsKey(prefab);

        public static string? NameOf(int prefab) => generatorNames.TryGetValue(prefab, out var n) ? n : null;

        /// <summary>Inventory from the last normal engine/guard lookup. Display reads never refresh this cache.</summary>
        internal static IReadOnlyList<(ZDO Zdo, string Name)> KnownGenerators => generators;

        /// <summary>Every generator in the world, rebuilt at most once per frame.</summary>
        public static IReadOnlyList<(ZDO Zdo, string Name)> Generators()
        {
            if (builtFrame == Time.frameCount || ZDOMan.instance == null) return generators;
            builtFrame = Time.frameCount;
            generators.Clear();
            foreach (ZDO zdo in ZDOMan.instance.m_objectsByID.Values)
                if (generatorNames.TryGetValue(zdo.GetPrefab(), out var name)) generators.Add((zdo, name));
            return generators;
        }

        /// <summary>The generator whose interior a point is in: above interior height, nearest flat within 300 m and 500 m of height.</summary>
        public static (ZDO Zdo, string Name)? GeneratorOf(Vector3 point)
        {
            if (point.y < InteriorHeight) return null;
            (ZDO, string)? best = null;
            float bestDistance = 300f;
            foreach (var g in Generators())
            {
                Vector3 p = g.Zdo.GetPosition();
                float d = new Vector2(p.x - point.x, p.z - point.z).magnitude;
                if (d < bestDistance && Mathf.Abs(p.y - point.y) < 500f)
                {
                    best = g;
                    bestDistance = d;
                }
            }
            return best;
        }

        /// <summary>The mode of a dungeon type; Delve.Dungeons.yml is read again when it changes.</summary>
        public static DungeonMode ModeOf(string generator)
        {
            try
            {
                var path = ModesPath;
                var stamp = File.Exists(path) ? File.GetLastWriteTimeUtc(path) : DateTime.MinValue;
                if (stamp != modesStamp)
                {
                    modesStamp = stamp;
                    modes = stamp == DateTime.MinValue ? DungeonsFile.Empty : DungeonsFile.Parse(File.ReadAllText(path));
                    foreach (var e in modes.Errors) Plugin.Log?.Warning("Delve " + DungeonsFile.FileName + ": " + e);
                }
            }
            catch (Exception error)
            {
                Plugin.Log?.Warning("Delve " + DungeonsFile.FileName + " unreadable: " + error.GetType().Name + "; keeping the modes read before.");
            }
            return modes.ModeOf(generator);
        }

        /// <summary>Whether a kind may act at a point: outside dungeons always; inside, by the dungeon type's mode.</summary>
        public static bool Allows(Vector3 point, DelveKind kind)
        {
            var g = GeneratorOf(point);
            return g == null || DungeonModes.Allows(ModeOf(g.Value.Name), kind);
        }

        public static void Clear()
        {
            generatorNames.Clear();
            generators.Clear();
            builtFrame = -1;
        }
    }
}
