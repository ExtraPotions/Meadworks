using System;
using UnityEditor;
using UnityEngine;

namespace Workshop
{
    /// <summary>
    /// Plays each attack clip through the creature's own animator with root motion on, the way the game drives it, and
    /// logs how far and which way the creature moved. A lunge must carry it forward (+Z), not back.
    ///   Unity -batchmode -projectPath unity -executeMethod Workshop.RootMotionCheck.Run -workshopCreature &lt;name&gt;
    /// </summary>
    public static class RootMotionCheck
    {
        public static void Run()
        {
            int code = 1;
            try
            {
                code = Check(Argument("-workshopCreature")) ? 0 : 1;
            }
            catch (Exception e)
            {
                Log.Error("root motion check failed: " + e);
            }
            EditorApplication.Exit(code);
        }

        public static bool Check(string name)
        {
            string folder = "Assets/Creatures/" + name;
            var info = CreatureManifest.Read(folder);
            bool ok = true;
            foreach (var clip in info.clips)
            {
                if (clip.tag == "attack")
                    ok &= Measure(folder + "/" + info.asset + ".prefab", clip.name, clip.last / (float)info.fps);
            }
            return ok;
        }

        private static bool Measure(string prefab, string trigger, float seconds)
        {
            var creature = (GameObject)UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(prefab));
            var animator = creature.GetComponent<Animator>();
            for (Transform t = CreaturePrefab.Find(creature.transform, "mimic_rig"); t != null && t != creature.transform; t = t.parent)
                Log.Info($"  {t.name}: local rotation {t.localRotation.eulerAngles:F0}, forward {t.forward:F2}");
            animator.applyRootMotion = true;
            animator.SetBool("sleeping", false);
            animator.Update(0.2f);                  // settle into the move state
            Vector3 start = creature.transform.position;
            animator.SetTrigger(trigger);
            for (float t = 0; t < seconds + 0.2f; t += 1f / 60f)
                animator.Update(1f / 60f);
            Vector3 moved = creature.transform.position - start;
            UnityEngine.Object.DestroyImmediate(creature);
            // Forward or in place only: no backwards, sideways, up or down travel (all three have happened on the way).
            bool forward = moved.z >= -0.05f && Mathf.Abs(moved.x) < 0.05f && Mathf.Abs(moved.y) < 0.05f;
            Log.Info($"root motion {trigger}: moved {moved:F2} ({(forward ? "forward or in place, OK" : "WRONG WAY")})");
            return forward;
        }

        private static string Argument(string name)
        {
            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, name);
            return index >= 0 && index + 1 < args.Length ? args[index + 1] : throw new ArgumentException("missing " + name);
        }
    }
}
