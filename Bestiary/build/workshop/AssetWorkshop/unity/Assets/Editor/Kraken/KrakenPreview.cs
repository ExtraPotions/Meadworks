using System.IO;
using UnityEditor.SceneManagement;
using UnityEngine;
using Workshop.Slinger;

namespace Workshop.Kraken
{
    /// <summary>
    /// Stills of the bundle's own prefabs, posed through their bones as the mod poses them: the head and its column from
    /// four sides, leaning forward 35 and back 18 degrees on kh_neck, the column bent 25 degrees at each of kh_body_1..3
    /// about X and about Z, the jaws closed and open; a tentacle straight and
    /// curled, from the side and from below; and the scenes the kraken is for (KrakenScene): the game's longship and its
    /// Karve (reference only) with the head beside them.
    /// </summary>
    public static class KrakenPreview
    {
        private const int Width = 1280, Height = 960;

        public static void Render(string folder, GameObject tentaclePrefab, GameObject headPrefab)
        {
            Directory.CreateDirectory(folder);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var camera = KrakenStage.Build();
            Head(folder, camera, headPrefab);
            Tentacle(folder, camera, tentaclePrefab);
            KrakenScene.Render(folder, tentaclePrefab, headPrefab, KrakenShip.Longship, "scene");
            KrakenScene.Render(folder, tentaclePrefab, headPrefab, KrakenShip.Karve, "karve");
            Log.Info("preview stills in " + folder);
        }

        public static void Shot(string folder, string name, Camera camera, Vector3 eye, Vector3 look, float fov = 35f)
        {
            camera.fieldOfView = fov;
            SlingerStage.Aim(camera, eye, look);
            SlingerStage.Shoot(folder, name, camera, Width, Height);
        }

        public static GameObject Spawn(GameObject prefab, Vector3 at, Quaternion rotation)
        {
            var copy = Object.Instantiate(prefab, at, rotation);
            copy.name = prefab.name;
            foreach (var skin in copy.GetComponentsInChildren<SkinnedMeshRenderer>())
                skin.forceMatrixRecalculationPerRender = true;   // batch mode skins once and keeps it otherwise
            return copy;
        }

        private static void Head(string folder, Camera camera, GameObject prefab)
        {
            var head = Spawn(prefab, Vector3.zero, Quaternion.identity);
            var centre = new Vector3(0f, 0.8f, -0.4f);
            Shot(folder, "head_front", camera, centre + new Vector3(0f, 0.8f, 22f), centre);
            Shot(folder, "head_three_quarter", camera, centre + new Vector3(15f, 3f, 16.5f), centre);
            Shot(folder, "head_side", camera, centre + new Vector3(22f, 1f, 0f), centre);
            Shot(folder, "head_back", camera, centre + new Vector3(-13.5f, 6f, -17f), centre);
            var neck = new Vector3(0f, 0.9f, 0.2f);
            KrakenPose.Neck(head, 35f);
            Shot(folder, "head_lean_forward_35", camera, neck + new Vector3(21f, 0.5f, 2f), neck + new Vector3(0, 0.2f, 0.8f));
            KrakenPose.Neck(head, -18f);
            Shot(folder, "head_lean_back_18", camera, neck + new Vector3(21f, 0.5f, 2f), neck + new Vector3(0, 0.6f, -0.6f));
            KrakenPose.Neck(head, 0f);
            KrakenPose.Body(head, new Vector3(25f, 0f, 0f));
            Shot(folder, "column_bent_x", camera, neck + new Vector3(22f, -0.5f, -1f), neck + new Vector3(0, -1.2f, -1.2f));
            KrakenPose.Body(head, new Vector3(0f, 0f, 25f));
            Shot(folder, "column_bent_z", camera, neck + new Vector3(0f, -0.5f, 22f), neck + new Vector3(-1.2f, -1.2f, 0f));
            KrakenPose.Body(head, Vector3.zero);
            var face = new Vector3(0.1f, 1.55f, 1.0f);
            Shot(folder, "beak_closed", camera, face + new Vector3(2.8f, 0.9f, 5.2f), face, 32f);
            KrakenPose.Beak(head, 35f);
            Shot(folder, "beak_open", camera, face + new Vector3(2.8f, 0.9f, 5.2f), face, 32f);
            Shot(folder, "beak_open_front", camera, face + new Vector3(0f, 0.4f, 5.8f), face, 32f);
            KrakenPose.Neck(head, 20f);
            Shot(folder, "head_lunge", camera, centre + new Vector3(13f, 1.5f, 14f), centre + new Vector3(0, 0.6f, 1f));
            Object.DestroyImmediate(head);
        }

        private static void Tentacle(string folder, Camera camera, GameObject prefab)
        {
            var tentacle = Spawn(prefab, Vector3.zero, Quaternion.identity);
            var middle = new Vector3(0f, 0f, KrakenContract.Length / 2f);
            Shot(folder, "tentacle_side_straight", camera, middle + new Vector3(-13.5f, 1.8f, 0f), middle, 38f);
            Shot(folder, "tentacle_below_straight", camera, middle + new Vector3(-2.5f, -12.5f, 0f), middle, 38f);
            Shot(folder, "tentacle_base_below", camera, new Vector3(-1.8f, -2f, -0.9f), new Vector3(0f, 0f, 1.8f), 45f);
            KrakenPose.Curl(tentacle, 6f, 1.6f);
            var curl = new Vector3(0f, -1.5f, 2.0f);
            Shot(folder, "tentacle_side_curl", camera, curl + new Vector3(-10f, 0.6f, 0f), curl, 38f);
            Shot(folder, "tentacle_below_curl", camera, curl + new Vector3(-4f, -7.5f, 2f), curl, 38f);
            Object.DestroyImmediate(tentacle);
        }
    }
}
