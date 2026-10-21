using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Workshop.Slinger
{
    /// <summary>
    /// Renders the slinger as the game will show it: the game's Greydwarf (reference, preview only) at the game's size,
    /// the kit hung on its bones the way the mod hangs it, the pouch and bands moved like the mod moves them. Stills of
    /// the shot's moments from four sides, and the frames of two videos (from the right front, and from the side wide
    /// enough to follow the stone's arc): a moment of idle, the shot with the stone flying off, idle again.
    /// </summary>
    public static class SlingerPreview
    {
        private const float Fps = 30f;
        private const float IdleLead = 0.4f;
        private const float IdleTail = 1.2f;   // long enough to see the slower stone land
        private static readonly float[] Moments = { 0.48f, 0.7f, SlingClip.Load, SlingClip.FullDraw, 1.56f };
        private static readonly (string clip, float time)[] Clearance = { ("Idle", 0f), ("Idle", 1.5f), ("Dwarf Walk", 0f), ("Dwarf Walk", 0.3f), ("Dwarf Walk", 0.55f), ("Dwarf Walk", 0.85f) };
        private static readonly (string name, Vector3 eye, Vector3 look)[] Views =
        {
            ("front", new Vector3(2.6f, 1.5f, 5.2f), new Vector3(0f, 1.0f, 0.3f)),
            ("side", new Vector3(5.6f, 1.2f, 0.6f), new Vector3(0f, 1.0f, 0.4f)),
            ("behind", new Vector3(1.6f, 2.0f, -4.4f), new Vector3(0f, 1.1f, 0.8f)),
            ("top", new Vector3(0.01f, 6.5f, 0.6f), new Vector3(0f, 0f, 0.6f)),
        };

        public static void Render(string folder)
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            SlingerStage.Build();
            var greydwarf = SlingerBuild.GameSizedGreydwarf();
            Mount(greydwarf);
            var rig = new SlingRigPreview(greydwarf);
            var shot = AssetDatabase.LoadAssetAtPath<AnimationClip>(SlingerBuild.Folder + "/" + SlingClip.Name + ".anim");
            Directory.CreateDirectory(folder);
            Stills(folder, greydwarf, shot, rig);
            var flight = new StoneFlight(greydwarf);
            Frames(Path.Combine(folder, "frames"), greydwarf, shot, rig, flight, new Vector3(3.6f, 1.5f, 3.6f), new Vector3(0f, 1.0f, 0.6f));
            Frames(Path.Combine(folder, "frames_flight"), greydwarf, shot, rig, flight, new Vector3(7.5f, 1.8f, 3.4f), new Vector3(0f, 1.1f, 3.2f));
        }

        /// <summary>The kit's mounts onto the bones of the same names, as the mod does it.</summary>
        private static void Mount(GameObject greydwarf)
        {
            var kit = (GameObject)Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(SlingerBuild.Folder + "/" + SlingKit.Name + ".prefab"));
            foreach (Transform mount in kit.transform.Cast<Transform>().ToArray())
            {
                mount.SetParent(SlingerReference.Bone(greydwarf, mount.name), false);
                mount.localPosition = Vector3.zero;
                mount.localRotation = Quaternion.identity;
                mount.localScale = Vector3.one;
            }
            Object.DestroyImmediate(kit);
            foreach (string slot in new[] { "ecr_sling_stone", "ecr_sling_hand_stone" })
            {
                GameObject pebble = SlingStone.Make("preview_pebble");
                pebble.transform.SetParent(SlingerReference.Bone(greydwarf, slot), false);
                pebble.transform.localScale = Vector3.one * SlingStone.Scale / pebble.transform.parent.lossyScale.x;
            }
        }

        private static void Stills(string folder, GameObject greydwarf, AnimationClip shot, SlingRigPreview rig)
        {
            var camera = Camera.main;
            foreach (float moment in Moments)
            {
                shot.SampleAnimation(greydwarf, moment);
                rig.Update(moment);
                Report(greydwarf, moment);
                foreach (var (name, eye, look) in Views)
                {
                    SlingerStage.Aim(camera, eye, look);
                    SlingerStage.Shoot(folder, $"still_{moment:0.00}_{name}", camera);
                }
            }
            Log.Info($"stills: {Moments.Length} moments x {Views.Length} views");
            ClearanceStills(folder, greydwarf, rig, camera);
            SatchelClearance.Report(greydwarf);
            if (System.Environment.GetCommandLineArgs().Contains("-workshopSatchelSearch"))
                SatchelClearance.Search(greydwarf);
        }

        /// <summary>The idle and the walk from the side and behind, to see the arms and legs clear the satchel.</summary>
        private static void ClearanceStills(string folder, GameObject greydwarf, SlingRigPreview rig, Camera camera)
        {
            foreach (var (clip, time) in Clearance)
            {
                SlingerReference.Clip(clip).SampleAnimation(greydwarf, time);
                rig.Update(-1f);
                SlingerStage.Aim(camera, new Vector3(3.2f, 1.0f, -1.2f), new Vector3(0f, 0.75f, 0f));
                SlingerStage.Shoot(folder, $"clear_{clip.Replace(" ", "")}_{time:0.00}_side", camera);
                SlingerStage.Aim(camera, new Vector3(1.2f, 1.3f, -3.2f), new Vector3(0.1f, 0.75f, 0f));
                SlingerStage.Shoot(folder, $"clear_{clip.Replace(" ", "")}_{time:0.00}_behind", camera);
            }
        }

        private static void Report(GameObject greydwarf, float moment)
        {
            string At(string bone) => $"{bone} {SlingerReference.Bone(greydwarf, bone).position:F2}";
            Log.Info($"at {moment:0.00}: " + string.Join(", ", new[] { "head", "head_end", "l_arm1", "l_hand", "r_arm1", "r_hand",
                "ecr_sling_muzzle", "ecr_sling_pinch", "ecr_sling_pouch" }.Select(At)));
        }

        private static void Frames(string folder, GameObject greydwarf, AnimationClip shot, SlingRigPreview rig, StoneFlight flight,
            Vector3 eye, Vector3 look)
        {
            Directory.CreateDirectory(folder);
            var camera = Camera.main;
            SlingerStage.Aim(camera, eye, look);
            AnimationClip idle = SlingerReference.Clip("Idle");
            int count = Mathf.RoundToInt((IdleLead + SlingClip.Length + IdleTail) * Fps);
            for (int frame = 0; frame < count; frame++)
            {
                float time = frame / Fps - IdleLead;
                if (time < 0f || time > SlingClip.Length)
                    idle.SampleAnimation(greydwarf, Mathf.Repeat(time + IdleLead, idle.length));
                else
                    shot.SampleAnimation(greydwarf, time);
                rig.Update(time);
                flight.Update(time);
                SlingerStage.Shoot(folder, $"frame_{frame:0000}", camera, 960, 540);
            }
            Log.Info($"video frames: {count}");
        }
    }
}
