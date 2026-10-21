using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Workshop
{
    /// <summary>
    /// Renders stills of the preview scene without entering Play mode: poses the creature from a clip at a given time
    /// and renders the scene camera to a PNG. Run without -nographics so there is a GPU to render with:
    ///   Unity -batchmode -projectPath unity -executeMethod Workshop.PreviewShots.Run -workshopOut &lt;folder&gt;
    /// </summary>
    public static class PreviewShots
    {
        private static readonly (string clip, float seconds)[] Shots =
            { ("sleep", 0f), ("idle", 0.3f), ("lunge", 0.6f), ("lunge", 2.0f) };

        public static void Run()
        {
            int code = 1;
            try
            {
                Capture(Argument("-workshopOut"));
                code = 0;
            }
            catch (Exception e)
            {
                Log.Error("preview shots failed: " + e);
            }
            EditorApplication.Exit(code);
        }

        /// <summary>
        /// Steps the fight replay frame by frame, as Play mode would (cues, animator, placement, camera), and renders
        /// the given frames: -executeMethod Workshop.PreviewShots.RunFight -workshopOut &lt;folder&gt;
        /// </summary>
        public static void RunFight()
        {
            int code = 1;
            try
            {
                CaptureFight(Argument("-workshopOut"), new[] { 30, 54, 190, 205, 232, 357, 453, 520, 556, 600 });
                code = 0;
            }
            catch (Exception e)
            {
                Log.Error("fight shots failed: " + e);
            }
            EditorApplication.Exit(code);
        }

        private static void CaptureFight(string folder, int[] frames)
        {
            EditorSceneManager.OpenScene(PreviewScene.ScenePath);
            var replay = UnityEngine.Object.FindFirstObjectByType<FightReplay>();
            replay.Begin();
            Directory.CreateDirectory(folder);
            const float step = 1f / 30f;
            for (int frame = 2; frame <= frames.Max(); frame++)
            {
                replay.Advance(step);
                replay.mimic.Update(step);
                replay.Place();
                replay.Follow(1f);
                if (!frames.Contains(frame))
                    continue;
                File.WriteAllBytes(Path.Combine(folder, $"fight_{frame:0000}.png"), Render(replay.view, 960, 540).EncodeToPNG());
                Log.Info("fight shot " + frame);
            }
        }

        private static void Capture(string folder)
        {
            EditorSceneManager.OpenScene(PreviewScene.ScenePath);
            var animator = UnityEngine.Object.FindFirstObjectByType<Animator>();
            var clips = animator.runtimeAnimatorController.animationClips;
            var camera = Camera.main;
            Directory.CreateDirectory(folder);
            foreach (var (clip, seconds) in Shots)
            {
                clips.First(c => c.name == clip).SampleAnimation(animator.gameObject, seconds);
                string file = Path.Combine(folder, $"unity_{clip}_{seconds:0.0}.png");
                File.WriteAllBytes(file, Render(camera, 960, 540).EncodeToPNG());
                Log.Info("shot " + file);
            }
        }

        private static Texture2D Render(Camera camera, int width, int height)
        {
            var target = new RenderTexture(width, height, 24);
            camera.targetTexture = target;
            camera.Render();
            RenderTexture.active = target;
            var image = new Texture2D(width, height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            image.Apply();
            camera.targetTexture = null;
            RenderTexture.active = null;
            return image;
        }

        private static string Argument(string name)
        {
            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, name);
            return index >= 0 && index + 1 < args.Length ? args[index + 1] : throw new ArgumentException("missing " + name);
        }
    }
}
