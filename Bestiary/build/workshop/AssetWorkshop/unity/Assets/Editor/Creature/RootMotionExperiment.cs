using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Workshop
{
    /// <summary>
    /// Diagnostic: re-imports the creature's clips under each combination of the root-motion import flags and measures
    /// the lunge each time, to find which setting makes Unity extract the leap in the direction the bone travels.
    ///   Unity -batchmode -projectPath unity -executeMethod Workshop.RootMotionExperiment.Run
    /// </summary>
    public static class RootMotionExperiment
    {
        public static void Run()
        {
            try
            {
                string folder = "Assets/Creatures/crypt_mimic";
                var info = CreatureManifest.Read(folder);
                string fbx = folder + "/" + info.fbx;
                Sample(fbx);
            }
            catch (Exception e)
            {
                Log.Error("experiment failed: " + e);
            }
            EditorApplication.Exit(0);
        }

        /// <summary>Where the bones are when the lunge is sampled straight onto the rig, with no root motion at all.</summary>
        private static void Sample(string fbx)
        {
            var creature = (GameObject)UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(fbx));
            var clip = AssetDatabase.LoadAllAssetsAtPath(fbx).OfType<AnimationClip>().First(c => c.name == "lunge");
            foreach (float t in new[] { 0f, 0.73f, 1.03f, 2.0f })
            {
                clip.SampleAnimation(creature, t);
                Vector3 root = CreaturePrefab.Find(creature.transform, "mimic_rig").position;
                Vector3 eyes = CreaturePrefab.Find(creature.transform, "eyes").position;
                Log.Info($"sampled lunge t={t:F2}: root {root:F2}, eyes {eyes:F2}");
            }
            UnityEngine.Object.DestroyImmediate(creature);
        }

        private static void Try(string fbx, CreatureManifest info, bool original, bool lockRotation, bool originalXZ)
        {
            var importer = (ModelImporter)AssetImporter.GetAtPath(fbx);
            var clips = importer.clipAnimations;
            foreach (var clip in clips)
            {
                clip.keepOriginalOrientation = original;
                clip.lockRootRotation = lockRotation;
                clip.keepOriginalPositionXZ = originalXZ;
            }
            importer.clipAnimations = clips;
            importer.SaveAndReimport();
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(fbx.Replace(".fbx", ".prefab"));
            var creature = (GameObject)UnityEngine.Object.Instantiate(prefab);
            var animator = creature.GetComponent<Animator>();
            animator.applyRootMotion = true;
            animator.SetBool("sleeping", false);
            animator.Update(0.2f);
            Vector3 start = creature.transform.position;
            animator.SetTrigger("lunge");
            for (float t = 0; t < 3f; t += 1f / 60f)
                animator.Update(1f / 60f);
            Log.Info($"original={original} lockRotation={lockRotation} originalXZ={originalXZ}: moved {creature.transform.position - start:F2}");
            UnityEngine.Object.DestroyImmediate(creature);
        }
    }
}
