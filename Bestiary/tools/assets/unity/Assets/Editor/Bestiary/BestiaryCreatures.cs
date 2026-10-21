// Bestiary bundle build: the kraken and the crypt mimic. Copyright (C) 2026 expDARE (ExtraPotions Meadworks).
// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using UnityEditor.SceneManagement;
using UnityEngine;
using Workshop;
using Workshop.Kraken;

namespace Bestiary.Build
{
    /// <summary>
    /// The two bundles the pinned scripts build without the game: the kraken (KrakenBuild's steps, minus its previews,
    /// which stage the game's ships) and the crypt mimic (the creature pipeline on its own rig, minus the preview scene).
    /// </summary>
    public static class BestiaryCreatures
    {
        public const string MimicCreature = "crypt_mimic";

        /// <summary>KrakenBuild.Run's prefab, check and report steps; the bundle is written by BestiaryBundles.</summary>
        public static string[] Kraken(string bundle)
        {
            if (bundle != KrakenBuild.Bundle)
                throw new ArgumentException($"the kraken bundle is {KrakenBuild.Bundle}, not {bundle}");
            string folder = KrakenBuild.Folder;
            KrakenReport.Begin();
            string[] inks = KrakenContract.Inks.Select(n => folder + "/" + n + ".png").ToArray();
            foreach (string ink in inks)
            {
                var texture = KrakenAssets.Overlay(ink);
                KrakenReport.Line($"texture {texture.name}: {texture.width}x{texture.height} {texture.format}, sRGB, no mipmaps, clamped");
            }
            var tentacle = KrakenModel.Read(folder + "/" + KrakenContract.Tentacle + ".json");
            var head = KrakenModel.Read(folder + "/" + KrakenContract.Head + ".json");
            // The culling boxes KrakenBuild gives the two prefabs (see its comment): any pose stays inside.
            string t = KrakenPrefab.Build(folder, tentacle, new Bounds(Vector3.zero, Vector3.one * 20f));
            string h = KrakenPrefab.Build(folder, head, new Bounds(new Vector3(0f, 1.1f, -0.2f), new Vector3(10.6f, 13.2f, 11.2f)));
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            GameObject tentacleCopy = KrakenBuild.Spawn(t), headCopy = KrakenBuild.Spawn(h);
            KrakenCheck.Run(tentacleCopy, headCopy, folder);
            KrakenReport.Prefab(tentacleCopy);
            KrakenReport.Radii(tentacleCopy);
            KrakenReport.Prefab(headCopy);
            UnityEngine.Object.DestroyImmediate(tentacleCopy);
            UnityEngine.Object.DestroyImmediate(headCopy);
            KrakenReport.Save(Path.Combine(BestiaryBundles.Argument("-bestiaryOut"), bundle + ".kraken-check.txt"));
            BestiaryBundles.Line("kraken contract check passed (KrakenCheck)");
            return new[] { t, h }.Concat(inks).ToArray();
        }

        /// <summary>CreatureBuild's import, controller, prefab and root-motion steps for the mimic (no preview scene).</summary>
        public static string[] Mimic(string bundle)
        {
            string folder = "Assets/Creatures/" + MimicCreature;
            var info = CreatureManifest.Read(folder);
            CreatureImport.Model(folder, info);
            var controller = CreatureAnimator.Build(folder, info);
            string prefab = CreaturePrefab.Build(folder, info, controller);
            if (!RootMotionCheck.Check(MimicCreature))
                throw new InvalidOperationException("an attack's root motion carries the mimic backwards");
            BestiaryBundles.Line($"mimic: {info.clips.Length} clips from its own rig, root motion check passed");
            return new[] { prefab };
        }
    }
}
