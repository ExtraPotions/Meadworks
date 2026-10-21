using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Workshop.Slinger;

namespace Workshop.RimeGiant
{
    /// <summary>
    /// Renders the Rime Giant as the game will show it: the game's Troll (reference, preview only) at the game's size and
    /// tinted as the mod tints it, the kit hung on its bones the way the mod hangs it. Stills: the idle from front and
    /// back, the punch and the slam as they land, the throw with the boulder leaving, the walk, half its plates broken,
    /// asleep under the crust from close by, from the front (the face in the snow) and from 15 m (a snowy outcrop), and
    /// beside a 1.8 m player at the mod's 1.4 scale; then the frames of a short video (idle, walk, punch).
    /// </summary>
    public static class RimePreview
    {
        private const int Width = 1280, Height = 960;
        private const float Scale = 1.4f;   // the mod's size for the giant, used for the still beside the player
        private const float GameRockScale = 0.2293f;   // troll_throw_projectile's visual: the stone mesh at this scale
        private static readonly Vector3 Body = new Vector3(0f, 3.4f, 0f);

        private static readonly (string name, string state, float time, Vector3 eye, Vector3 look, bool asleep, int broken)[] Stills =
        {
            ("a_idle_front", "Idle", 0f, new Vector3(8f, 6f, 13f), Body, false, 0),
            ("b_idle_back", "Idle", 0f, new Vector3(-8f, 7f, -12f), Body, false, 0),
            ("c_punch", "Punch", 1.05f, new Vector3(-9f, 5f, 12f), new Vector3(0f, 3f, 1f), false, 0),
            ("d_slam", "Slam", 0.9f, new Vector3(11f, 5f, 10f), new Vector3(0f, 2.6f, 1f), false, 0),
            ("e_sleeping", "Sleeping", 0f, new Vector3(8f, 5f, 9f), new Vector3(0f, 2f, 0.2f), true, 0),
            ("e_sleeping_face", "Sleeping", 0f, new Vector3(1.5f, 3f, 10f), new Vector3(0f, 2f, 0.5f), true, 0),
            ("e_sleeping_back", "Sleeping", 0f, new Vector3(-7f, 6f, -9f), new Vector3(0f, 2f, 0f), true, 0),
            ("e_sleeping_15m", "Sleeping", 0f, new Vector3(9f, 4.5f, 12f), new Vector3(0f, 1.8f, 0f), true, 0),
            ("f_walk", "Walk", 0.5f, new Vector3(11f, 5f, 9f), Body, false, 0),
            ("g_half_broken", "Idle", 0f, new Vector3(8f, 6f, 13f), Body, false, 4),
            ("g_half_broken_back", "Idle", 0f, new Vector3(-8f, 7f, -12f), Body, false, 4),
            ("i_throw", "Throw", 0.95f, new Vector3(12f, 5f, 12f), new Vector3(0f, 4.5f, 2.5f), false, 0),
            ("j_run", "Run", 0.3f, new Vector3(11f, 5f, 9f), Body, false, 0),
            ("k_closeup", "Idle", 0f, new Vector3(-4.5f, 6.4f, 6f), new Vector3(-1.2f, 4.4f, 0.4f), false, 0),
        };

        public static void Render(string folder, RimeFitData fit)
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RimeStage.Build();
            var troll = RimeReference.Troll();
            var poser = new RimePoser(troll);
            var dress = new RimeDress(troll);
            Directory.CreateDirectory(folder);
            var boulder = Boulder();
            foreach (var still in Stills)
                Still(folder, troll, poser, dress, boulder, still);
            ScaleStill(folder, troll, poser, dress);
            BoulderStill(folder, troll);
            RimeClearance.Report(troll, poser, dress);
            dress.Asleep(false);
            RimeFrames.Render(Path.Combine(folder, "frames"), poser);
        }

        private static void Still(string folder, GameObject troll, RimePoser poser, RimeDress dress, GameObject boulder,
            (string name, string state, float time, Vector3 eye, Vector3 look, bool asleep, int broken) still)
        {
            poser.Pose(still.state, still.time);
            dress.Asleep(still.asleep);
            dress.Broken(still.broken);
            boulder.SetActive(still.state == "Throw");
            SlingerStage.Aim(Camera.main, still.eye, still.look);
            SlingerStage.Shoot(folder, still.name, Camera.main, Width, Height);
        }

        /// <summary>At the mod's 1.4 scale, beside a 1.8 m player, with a boulder at the player's feet for its size.</summary>
        private static void ScaleStill(string folder, GameObject troll, RimePoser poser, RimeDress dress)
        {
            troll.transform.localScale = Vector3.one * Scale;
            poser.Pose("Idle", 0f);
            dress.Asleep(false);
            dress.Broken(0);
            var player = RimeStage.Player(new Vector3(4.5f, 0f, 5f));
            var rock = Boulder();
            rock.transform.position = new Vector3(6f, 0.55f, 5.5f);
            SlingerStage.Aim(Camera.main, new Vector3(16f, 5f, 17f), new Vector3(2f, 4f, 2f));
            SlingerStage.Shoot(folder, "h_scale_player", Camera.main, Width, Height);
            Object.DestroyImmediate(player);
            Object.DestroyImmediate(rock);
            troll.transform.localScale = Vector3.one;
        }

        /// <summary>
        /// The boulder just after the troll's throw lets go: the game spawns the rock 6.3 m up and 2.16 m in front of the
        /// troll (troll_throw's attack height and range), and it has flown on a little.
        /// </summary>
        private static GameObject Boulder()
        {
            var boulder = (GameObject)Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(RimeBuild.PartPrefab(RimeBuild.Boulder)));
            boulder.transform.SetPositionAndRotation(new Vector3(0.2f, 6.2f, 4.2f), Quaternion.Euler(20f, 35f, 10f));
            return boulder;
        }

        /// <summary>The boulder on the ground beside the troll's own thrown rock (the game's mesh at its visual's scale), for size.</summary>
        private static void BoulderStill(string folder, GameObject troll)
        {
            troll.SetActive(false);
            var boulder = Boulder();
            boulder.transform.SetPositionAndRotation(new Vector3(-0.9f, 0.55f, 6f), Quaternion.Euler(0f, 30f, 0f));
            var rock = SlingStone.Make("game_rock");
            rock.transform.SetPositionAndRotation(new Vector3(0.9f, 0.27f, 6f), Quaternion.identity);   // the mesh reaches 1.17 below its origin
            rock.transform.localScale = Vector3.one * GameRockScale;
            var player = RimeStage.Player(new Vector3(2.6f, 0f, 6.2f));
            SlingerStage.Aim(Camera.main, new Vector3(1.5f, 2.2f, 11.5f), new Vector3(0.4f, 0.7f, 6f));
            SlingerStage.Shoot(folder, "l_boulder_beside_game_rock", Camera.main, Width, Height);
            Object.DestroyImmediate(boulder);
            Object.DestroyImmediate(rock);
            Object.DestroyImmediate(player);
            troll.SetActive(true);
        }
    }
}
