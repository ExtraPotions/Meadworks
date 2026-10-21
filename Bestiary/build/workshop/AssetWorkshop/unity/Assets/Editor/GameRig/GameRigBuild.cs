using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Workshop.GameRig
{
    /// <summary>
    /// Route (b) in Unity: a new body on a game skeleton, from Blender's contract to the bundle.
    ///   Unity -batchmode -projectPath unity -executeMethod Workshop.GameRig.GameRigBuild.Run -workshopAsset &lt;name&gt;
    ///         -workshopOut &lt;folder&gt; [-workshopPreview &lt;folder&gt;] [-workshopBlender &lt;folder&gt;]
    /// The asset's build.ps1 stages Blender's out/&lt;name&gt;.json and PNGs in Assets/Bundles/&lt;name&gt;. This builds the prefab
    /// (GameRigPrefab), checks it against the contract and against the game's own creature as Unity imports it from the
    /// reference export (GameRigCheck, GameRigPlacement), plays both through the game's controller (GameRigPreview,
    /// GameRigMotion), and bundles it as &lt;name&gt;.windows and &lt;name&gt;.linux; report.txt beside them. Any failed check
    /// fails the build (exit code 2, after the previews and the bundle are written; 1 when it stopped on an error). A preview folder gets stills and video frames (run without -nographics for those); a Blender
    /// folder gets our body's point cache for blender/workshop/gamerig_scene.py.
    /// </summary>
    public static class GameRigBuild
    {
        /// <summary>The exit code when the build ran to the end but checks failed (1 is an exception): previews still usable.</summary>
        public const int ChecksFailed = 2;

        public static void Run()
        {
            int code = 1;
            string outFolder = "";
            try
            {
                string asset = GameRigStage.Argument("-workshopAsset");
                outFolder = GameRigStage.Argument("-workshopOut");
                GameRigReport.Begin();
                Build(asset, outFolder, GameRigStage.Argument("-workshopPreview", ""), GameRigStage.Argument("-workshopBlender", ""));
                code = GameRigReport.Failures == 0 ? 0 : ChecksFailed;
                if (code != 0)
                    Log.Error($"{GameRigReport.Failures} checks failed; see report.txt");
            }
            catch (Exception e)
            {
                GameRigReport.Line("FAIL build: " + e);
                Log.Error("game rig build failed: " + e);
            }
            if (outFolder != "")
                GameRigReport.Save(Path.Combine(outFolder, "report.txt"));
            EditorApplication.Exit(code);
        }

        public static void Build(string asset, string outFolder, string previewFolder, string blenderFolder)
        {
            string folder = "Assets/Bundles/" + asset;
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var model = GameRigModel.Read(folder + "/" + asset + ".json");
            GameRigReport.Line($"contract {asset}: on {model.source}, {model.transforms.Length} transforms, {model.bones.Length} bones "
                + $"({model.boneOrder} order), {model.triangles} triangles, sockets {string.Join(", ", model.sockets)}");
            string prefab = GameRigPrefab.Build(folder, model);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Verify(model, prefab);
            GameRigPreview.Prepare(previewFolder);
            GameRigPreview.Run(model, prefab, previewFolder, blenderFolder);
            GameRigBundle.Build(asset.ToLowerInvariant(), Assets(folder, model, prefab), outFolder);
        }

        /// <summary>The contract on our prefab, then against the game's creature at rest (before anything animates).</summary>
        private static void Verify(GameRigModel model, string prefabPath)
        {
            var ours = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath));
            ours.name = model.asset;
            GameObject game = GameRigStaging.Creature(model.reference, "game_" + model.asset);
            GameRigCheck.Contract(ours, model);
            GameRigCheck.Game(ours, game, model);
            GameRigPlacement.Check(ours.GetComponentInChildren<SkinnedMeshRenderer>(), GameRigStaging.Body(game, model.renderer));
            UnityEngine.Object.DestroyImmediate(ours);
            UnityEngine.Object.DestroyImmediate(game);
        }

        /// <summary>The prefab, and the regions mask a mod recolours by when the model has one.</summary>
        private static string[] Assets(string folder, GameRigModel model, string prefab)
        {
            var assets = new List<string> { prefab };
            if (!string.IsNullOrEmpty(model.material.regions))
            {
                string mask = folder + "/" + model.material.regions;
                GameRigAssets.Mask(mask);
                assets.Add(mask);
            }
            return assets.ToArray();
        }
    }
}
