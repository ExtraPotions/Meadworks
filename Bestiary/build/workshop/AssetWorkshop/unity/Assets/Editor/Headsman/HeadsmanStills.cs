using System.IO;
using UnityEngine;
using Workshop.Crossbow;
using Workshop.Slinger;

namespace Workshop.Headsman
{
    /// <summary>
    /// Contact sheets for judging the clips: each attack as the Animator plays it, twelve moments from two sides, on
    /// the game's Skeleton with the axe hung as the mod will hang it (<see cref="HeadsmanPlayback"/>), one sheet per
    /// attack and side (sheet_&lt;move&gt;_&lt;side&gt;.png), each frame stamped with its time by its place in the grid (row
    /// by row, evenly from the first frame to the last).
    /// </summary>
    public static class HeadsmanStills
    {
        private const int Size = 300, Columns = 4, Rows = 3;
        private static readonly (string name, Vector3 eye, Vector3 look)[] Views =
        {
            ("front", new Vector3(2.8f, 1.7f, 3.6f), new Vector3(0f, 1.0f, 0.2f)),
            ("side", new Vector3(-4.4f, 1.5f, 0.2f), new Vector3(0f, 1.0f, 0.2f)),
        };

        public static void Render(string folder, GameObject skeleton, HeadsmanGrip grip, HeadsmanMove[] moves, XbowPoser played)
        {
            Directory.CreateDirectory(folder);
            SlingerStage.Build();
            Camera camera = Camera.main;
            camera.fieldOfView = 52f;
            var playback = new HeadsmanPlayback(skeleton, grip);
            Closeups(folder, skeleton, played, camera);
            foreach (HeadsmanMove move in moves)
            {
                foreach (var (name, eye, look) in Views)
                {
                    SlingerStage.Aim(camera, eye, look);
                    Sheet(folder, $"sheet_{move.Name}_{name}", move, t => Pose(skeleton, played, playback, move, t), camera);
                }
                SlingerStage.Aim(camera, Views[0].eye, Views[0].look);
                foreach (var (time, moment) in move.Timeline())
                {
                    Pose(skeleton, played, playback, move, time);
                    SlingerStage.Shoot(folder, $"moment_{move.Name}_{time:0.00}_{moment.Replace(' ', '_')}", camera, 480, 480);
                }
            }
            skeleton.transform.rotation = Quaternion.identity;
        }

        private static readonly (string name, Vector3 eye, Vector3 look)[] Close =
        {
            ("front", new Vector3(0.7f, 1.35f, 1.7f), new Vector3(0f, 1.15f, 0.2f)),
            ("left", new Vector3(-1.7f, 1.25f, 0.5f), new Vector3(0f, 1.1f, 0.2f)),
            ("right", new Vector3(1.7f, 1.25f, 0.6f), new Vector3(0f, 1.1f, 0.2f)),
        };

        /// <summary>The ready hold up close from three sides, to judge the fists on the haft.</summary>
        private static void Closeups(string folder, GameObject skeleton, XbowPoser played, Camera camera)
        {
            skeleton.transform.rotation = Quaternion.identity;
            played.Pose(HeadsmanAuthor.CarryClips[0].name, 0f);
            foreach (var (name, eye, look) in Close)
            {
                SlingerStage.Aim(camera, eye, look);
                SlingerStage.Shoot(folder, "ready_" + name, camera, 600, 600);
            }
        }

        private static void Pose(GameObject skeleton, XbowPoser played, HeadsmanPlayback playback, HeadsmanMove move, float time)
        {
            skeleton.transform.rotation = Quaternion.AngleAxis(move.Keys.RootYaw(time), Vector3.up);
            played.Pose(move.Clip, time);
            playback.Dress(move, time);
        }

        private static void Sheet(string folder, string name, HeadsmanMove move, System.Action<float> pose, Camera camera)
        {
            var sheet = new Texture2D(Size * Columns, Size * Rows, TextureFormat.RGB24, false);
            var target = new RenderTexture(Size, Size, 24) { antiAliasing = 4 };
            for (int i = 0; i < Columns * Rows; i++)
            {
                pose(move.Length * i / (Columns * Rows - 1));
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                sheet.ReadPixels(new Rect(0, 0, Size, Size), i % Columns * Size, (Rows - 1 - i / Columns) * Size);
            }
            sheet.Apply();
            camera.targetTexture = null;
            RenderTexture.active = null;
            Object.DestroyImmediate(target);
            File.WriteAllBytes(Path.Combine(folder, name + ".png"), sheet.EncodeToPNG());
        }
    }
}
