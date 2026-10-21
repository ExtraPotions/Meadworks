using System;
using UnityEditor;

namespace Workshop
{
    /// <summary>
    /// Batch-mode entry for a creature staged in Assets/Creatures/&lt;name&gt;/ (its FBX and manifest):
    ///   Unity -batchmode -projectPath unity -executeMethod Workshop.CreatureBuild.Run -workshopCreature &lt;name&gt;
    /// Imports the rig and clips, builds the animator controller and the prefab, then the preview scene.
    /// </summary>
    public static class CreatureBuild
    {
        public static void Run()
        {
            int code = 1;
            try
            {
                Build(Argument("-workshopCreature"));
                code = 0;
            }
            catch (Exception e)
            {
                Log.Error("creature build failed: " + e);
            }
            EditorApplication.Exit(code);
        }

        public static void Build(string name)
        {
            string folder = "Assets/Creatures/" + name;
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var info = CreatureManifest.Read(folder);
            CreatureImport.Model(folder, info);
            var controller = CreatureAnimator.Build(folder, info);
            string prefab = CreaturePrefab.Build(folder, info, controller);
            if (!RootMotionCheck.Check(name))
                throw new InvalidOperationException("an attack's root motion carries the creature backwards");
            PreviewScene.Build(prefab, info);
            AssetDatabase.SaveAssets();
        }

        private static string Argument(string name)
        {
            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, name);
            if (index < 0 || index + 1 >= args.Length)
                throw new ArgumentException("missing argument " + name);
            return args[index + 1];
        }
    }
}
