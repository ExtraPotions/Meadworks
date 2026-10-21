// Portions Copyright (C) 2026 Gerald Glasgow. EliteCreaturesPack 0.9.0 at ValheimMods 3bb778330f23645ccddb42fbc0c6ffab72c842b6
// (Ballista/BallistaCrafting.cs, Ballista/BallistaPrefabs.cs, Ballista/BallistaWords.cs).
// Changed by expDARE, 2026: Bestiary namespace and lifecycle (owned recipe, hammer entry taken out at shutdown); no
// bundle; words from Bestiary's seven-language catalog.
// SPDX-License-Identifier: GPL-3.0-or-later
using System.Collections.Generic;
using System.Linq;
using ExtraPotions.Bestiary.Runtime.Adopted.BundlePrefabs;
using ExtraPotions.Bestiary.Runtime.Native.Arsenal;
using ExtraPotions.Bestiary.Runtime.Native.Core;
using HarmonyLib;
using UnityEngine;

namespace ExtraPotions.Bestiary.Runtime.Native.Ballista
{
    /// <summary>
    /// How the Bone Ballista and its missiles are made, while the switch is on (<see cref="BallistaSettings"/>): the
    /// ballista in the hammer right after the game's ballista (3 spines and 25 bone fragments, at the game ballista's
    /// station), and Bone Missiles at the workbench, level 2 like the skeleton arsenal, twenty at a time like the game's
    /// wooden missiles. Off: both leave; built ballistas stay. Put in every ObjectDB the game builds or copies once the
    /// prefabs exist, and again when the settings change (Bestiary's owned refresh). Every peer.
    /// </summary>
    public static class BallistaCrafting
    {
        private const string Hammer = "Hammer", Workbench = "piece_workbench";
        private const int MissilesPerCraft = 20, WorkbenchLevel = 2;
        private static readonly (string item, int amount)[] PieceCost = { (ArsenalItems.SpineName, 3), ("BoneFragments", 25) };
        private static readonly (string item, int amount)[] MissileCost = { ("BoneFragments", 5), ("Feathers", 2) };

        private static Recipe? recipe;
        private static readonly List<List<GameObject>> hammers = new List<List<GameObject>>();

        [HarmonyPatch(typeof(ObjectDB), nameof(ObjectDB.Awake))]
        public static class Built
        {
            [HarmonyPriority(Priority.Last)]
            private static void Postfix(ObjectDB __instance) => SafeCall.Run("bone ballista crafting", Refresh, __instance);
        }

        [HarmonyPatch(typeof(ObjectDB), nameof(ObjectDB.CopyOtherDB))]
        public static class Copied
        {
            [HarmonyPriority(Priority.Last)]
            private static void Postfix(ObjectDB __instance) => SafeCall.Run("bone ballista crafting", Refresh, __instance);
        }

        /// <summary>The hammer and the missile recipe in step with the switch (idempotent).</summary>
        public static void Refresh(ObjectDB? db)
        {
            // No ZNetScene: the main menu's database, where nothing is built and no station exists yet.
            if (db == null || ZNetScene.instance == null)
            {
                return;
            }
            if (BallistaPiece.Prefab != null)
            {
                Hammered(db, BallistaPiece.Prefab);
            }
            if (BallistaMissile.Item != null)
            {
                Recipe(db, BallistaMissile.Item);
            }
        }

        /// <summary>Bestiary's shutdown: the ballista leaves every hammer it was put in.</summary>
        internal static void Remove()
        {
            foreach (List<GameObject> pieces in hammers)
            {
                pieces?.Remove(BallistaPiece.Prefab!);
            }
            hammers.Clear();
        }

        private static void Hammered(ObjectDB db, GameObject ballista)
        {
            List<GameObject>? pieces = db.GetItemPrefab(Hammer)?.GetComponent<ItemDrop>()?.m_itemData.m_shared.m_buildPieces?.m_pieces;
            if (pieces == null)
            {
                return;
            }
            pieces.Remove(ballista);
            ballista.GetComponent<Piece>().m_resources = Cost(db, PieceCost);
            if (BallistaSettings.Enabled)
            {
                int game = pieces.FindIndex(p => p != null && p.name == BallistaPiece.GamePiece);
                pieces.Insert(game < 0 ? pieces.Count : game + 1, ballista);
                if (!hammers.Contains(pieces))
                {
                    hammers.Add(pieces);
                }
            }
        }

        private static void Recipe(ObjectDB db, GameObject missile)
        {
            if (recipe == null)
            {
                recipe = ScriptableObject.CreateInstance<Recipe>();
                (recipe.name, recipe.m_item, recipe.m_amount) = ("Recipe_" + missile.name, missile.GetComponent<ItemDrop>(), MissilesPerCraft);
            }
            GameObject? bench = ZNetScene.instance.GetPrefab(Workbench);
            recipe.m_craftingStation = bench != null ? bench.GetComponent<CraftingStation>() : null;
            recipe.m_minStationLevel = WorkbenchLevel;
            recipe.m_resources = Cost(db, MissileCost);
            recipe.m_enabled = BallistaSettings.Enabled && recipe.m_craftingStation != null;
            if (!db.m_recipes.Contains(recipe))
            {
                db.m_recipes.Add(recipe);
                Adapters.NativeRuntime.Recipe(db, recipe);
            }
        }

        private static Piece.Requirement[] Cost(ObjectDB db, (string item, int amount)[] cost)
        {
            var made = new List<Piece.Requirement>();
            foreach ((string name, int amount) in cost)
            {
                ItemDrop? item = ArsenalItems.Find(db, name);
                if (item == null)
                {
                    Log.Warn($"Bone ballista: the game has no {name}; it is left out of the cost.");
                    continue;
                }
                made.Add(new Piece.Requirement { m_resItem = item, m_amount = amount, m_recover = true });
            }
            return made.ToArray();
        }
    }

    /// <summary>
    /// Builds the Bone Ballista, its Bone Missiles and their flight once, and registers them whenever ZNetScene wakes,
    /// identically on the server and every client, whether the switch is on or not (ballistas already built always
    /// load). Models, sounds and icons are the game's own (<see cref="BallistaPiece"/>, <see cref="BallistaMissile"/>).
    /// </summary>
    public static class BallistaPrefabs
    {
        private static bool built;

        public static void Install(Harmony harmony)
        {
            NetPrefabs.OnSceneAwake(harmony, scene => Adapters.Guard.Run("BallistaPrefabs.Build", () => Build(scene, harmony)));
        }

        private static void Build(ZNetScene scene, Harmony harmony)
        {
            if (!built)
            {
                built = true;
                BallistaMissile.Build(scene);
                BallistaPiece.Build(scene);
                if (BallistaMissile.Item != null)
                {
                    ItemPrefabs.Register(harmony, BallistaMissile.Item);
                }
                Log.Info($"Bone ballista ready: {(BallistaPiece.Prefab != null ? "piece" : "no piece")}, {(BallistaMissile.Item != null ? "missiles" : "no missiles")}.");
            }
            foreach (GameObject prefab in new[] { BallistaPiece.Prefab, BallistaMissile.Item, BallistaMissile.Shot }.OfType<GameObject>())
            {
                NetPrefabs.Register(scene, prefab);
            }
            BallistaWords.Add(Localization.instance);
            BallistaCrafting.Refresh(ObjectDB.instance);
        }
    }

    /// <summary>
    /// The Bone Ballista's words for the game's translation table: the piece, its missiles, its hover and its messages,
    /// from Bestiary's catalog in the selected language. Added again after each language setup, which rebuilds the table.
    /// </summary>
    [HarmonyPatch(typeof(Localization), nameof(Localization.SetupLanguage))]
    public static class BallistaWords
    {
        public static void Add(Localization localization, string? language = null)
        {
            if (localization == null)
            {
                return;
            }
            Adapters.NativeWords.Add(localization, BallistaPiece.Word, "Bone Ballista", language);
            Adapters.NativeWords.Add(localization, BallistaPiece.Word + "_description",
                "A small ballista of bones. Hold it to aim and shoot, a turn of 45 degrees either way; it reloads from the bone missiles you carry.", language);
            Adapters.NativeWords.Add(localization, BallistaMissile.Word, "Bone Missile", language);
            Adapters.NativeWords.Add(localization, BallistaMissile.Word + "_description",
                "A missile for the bone ballista: a long bone shaft that breaks what it hits.", language);
            Adapters.NativeWords.Add(localization, "hud_ecp_bal_hold", "Hold", language);
            Adapters.NativeWords.Add(localization, "hud_ecp_bal_letgo", "Let go", language);
            Adapters.NativeWords.Add(localization, "hud_ecp_bal_loaded", "loaded", language);
            Adapters.NativeWords.Add(localization, "hud_ecp_bal_empty", "empty", language);
            Adapters.NativeWords.Add(localization, "msg_ecp_bal_nomissiles", "No bone missiles", language);
            Adapters.NativeWords.Add(localization, "msg_ecp_bal_behind", "Stand behind it to hold it", language);
        }

        private static void Postfix(Localization __instance, string language) =>
            SafeCall.Run("Localization.SetupLanguage bone ballista words", () => Add(__instance, language));
    }
}
