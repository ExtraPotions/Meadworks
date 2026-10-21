using System.IO;
using UnityEngine;
using Workshop.Slinger;

namespace Workshop.RimeGiant
{
    /// <summary>
    /// The frames of the preview video, 30 a second from the giant's right front: a moment of idle, a walk cycle and
    /// the punch. The clips cut rather than blend; blending is the game's job.
    /// </summary>
    public static class RimeFrames
    {
        private const float Fps = 30f;
        private static readonly (string state, float seconds)[] Sequence = { ("Idle", 1.5f), ("Walk", 3.06f), ("Punch", 1.63f) };

        public static void Render(string folder, RimePoser poser)
        {
            Directory.CreateDirectory(folder);
            var camera = Camera.main;
            SlingerStage.Aim(camera, new Vector3(10f, 5.5f, 14f), new Vector3(0f, 3.2f, 0.5f));
            int frame = 0;
            foreach (var (state, seconds) in Sequence)
            {
                for (float t = 0f; t < seconds; t += 1f / Fps)
                {
                    poser.Pose(state, t);
                    SlingerStage.Shoot(folder, $"frame_{frame++:0000}", camera, 960, 540);
                }
            }
            Log.Info($"video frames: {frame}");
        }
    }
}
