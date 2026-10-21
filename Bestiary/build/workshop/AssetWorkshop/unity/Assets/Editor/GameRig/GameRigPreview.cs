using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using Workshop.Crossbow;

namespace Workshop.GameRig
{
    /// <summary>
    /// The playback: the game's creature with its own body and the game's creature wearing ours, side by side, put
    /// through the game's own controller (GameRigScript: waking, idle, walk, run, every attack, stagger) at 30 frames a
    /// second, measured every frame (GameRigMotion). With a preview folder it also takes stills at each phase's moment
    /// (both creatures from the front, ours from its front left; stills.txt lists them in order) and every frame of a
    /// video; with a Blender folder it records our body frame by frame as a point cache (the crossbowman's XbowCache)
    /// for blender/workshop/gamerig_scene.py.
    /// </summary>
    public static class GameRigPreview
    {
        public const float Fps = 30f;

        public static void Run(GameRigModel model, string prefabPath, string previewFolder, string blenderFolder)
        {
            Camera camera = GameRigStage.Build();
            var actors = GameRigActors.Create(model, prefabPath);
            var motion = new GameRigMotion(actors);
            var cache = string.IsNullOrEmpty(blenderFolder) ? null : new XbowCache(new Renderer[] { actors.WornBody });
            var stills = new List<string>();
            int frame = 0;
            foreach (GameRigPhase phase in GameRigScript.Phases(actors.Controller))
            {
                motion.Begin(phase.Label);
                cache?.Mark(phase.Label);
                foreach (Animator animator in actors.Animators)
                    phase.Begin(animator);
                frame = Phase(phase, actors, motion, cache, previewFolder, camera, stills, frame);
            }
            motion.Report();
            Finish(previewFolder, blenderFolder, cache, stills, frame);
            actors.Destroy();
        }

        private static int Phase(GameRigPhase phase, GameRigActors actors, GameRigMotion motion, XbowCache cache,
            string folder, Camera camera, List<string> stills, int frame)
        {
            int steps = Mathf.RoundToInt(phase.Seconds * Fps), still = Mathf.RoundToInt(phase.Still * Fps);
            for (int step = 0; step < steps; step++, frame++)
            {
                foreach (Animator animator in actors.Animators)
                    animator.Update(1f / Fps);
                motion.Measure();
                cache?.Capture(0f);
                if (string.IsNullOrEmpty(folder))
                    continue;
                if (step == still)
                    stills.AddRange(Stills(folder, phase.Label, stills.Count / 2, camera));
                Video(folder, camera, frame);
            }
            return frame;
        }

        /// <summary>Two stills: both creatures from the front, and ours alone from its front left.</summary>
        private static IEnumerable<string> Stills(string folder, string label, int index, Camera camera)
        {
            string name = $"{index:00}_{label.Replace(' ', '_').Replace('/', '_')}";
            GameRigStage.Aim(camera, new Vector3(0f, 1.25f, 6.2f), new Vector3(0f, 1.0f, 0f));
            GameRigStage.Shoot(Path.Combine(folder, "stills", name + "_pair.png"), camera, 800, 600);
            GameRigStage.Aim(camera, new Vector3(-GameRigActors.Apart - 2.3f, 1.55f, 2.9f), new Vector3(-GameRigActors.Apart, 1.0f, 0.1f));
            GameRigStage.Shoot(Path.Combine(folder, "stills", name + "_ours.png"), camera, 800, 600);
            yield return name + "_pair|" + label + ": the game's body (left) and ours (right)";
            yield return name + "_ours|" + label + ": ours from its front left";
        }

        private static void Video(string folder, Camera camera, int frame)
        {
            GameRigStage.Aim(camera, new Vector3(-2.4f, 1.55f, 5.6f), new Vector3(-0.1f, 1.0f, 0f));
            GameRigStage.Shoot(Path.Combine(folder, "frames", $"frame_{frame:0000}.png"), camera, 960, 540);
        }

        private static void Finish(string previewFolder, string blenderFolder, XbowCache cache, List<string> stills, int frames)
        {
            if (!string.IsNullOrEmpty(previewFolder))
            {
                File.WriteAllLines(Path.Combine(previewFolder, "stills", "stills.txt"), stills);
                GameRigReport.Line($"preview: {stills.Count} stills and {frames} video frames in {previewFolder}");
            }
            if (cache != null)
                cache.Write(blenderFolder, Fps);
        }

        public static void Prepare(string folder)
        {
            if (string.IsNullOrEmpty(folder))
                return;
            foreach (string sub in new[] { "stills", "frames" })
            {
                string path = Path.Combine(folder, sub);
                if (Directory.Exists(path))
                    Directory.Delete(path, true);
                Directory.CreateDirectory(path);
            }
        }
    }
}
