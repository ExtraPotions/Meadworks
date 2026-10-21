using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Workshop
{
    /// <summary>
    /// Builds Assets/Preview/MimicPreview.unity: the crypt set, the mimic prefab with the game's crypt chest hung on its
    /// body and lid bones (from the reference export, preview only), a stand-in player, and the replay of the
    /// choreographed fight with its HUD, which runs in Play mode.
    /// </summary>
    public static class PreviewScene
    {
        public const string ScenePath = "Assets/Preview/MimicPreview.unity";
        private const string Chest = "world/Props/Chests/";
        private const string Fonts = "3rd party/TextMesh Pro/Resources/Fonts/";

        public static void Build(string prefabPath, CreatureManifest info)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            PreviewStage.Build();
            var mimic = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath));
            AttachChest(mimic.transform);
            PreviewMaterials.Dress(mimic, info);
            mimic.AddComponent<PreviewEvents>();
            Transform sword;
            var player = PreviewStage.StandIn(out sword);
            Fight(prefabPath, mimic.GetComponent<Animator>(), player, sword);
            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            EditorSceneManager.SaveScene(scene, ScenePath);
            Log.Info("preview scene " + ScenePath);
        }

        /// <summary>The replay of the choreographed fight (&lt;asset&gt;_fight.json beside the prefab) and its HUD.</summary>
        private static void Fight(string prefabPath, Animator mimic, Transform player, Transform sword)
        {
            string json = Path.ChangeExtension(prefabPath, null) + "_fight.json";
            var fight = AssetDatabase.LoadAssetAtPath<TextAsset>(json);
            if (fight == null)
            {
                Log.Error("no " + json + "; the preview scene has no fight to replay");
                return;
            }
            var replay = new GameObject("Fight").AddComponent<FightReplay>();
            replay.fightJson = fight;
            replay.mimic = mimic;
            replay.player = player;
            replay.sword = sword;
            replay.view = Camera.main;
            var hud = replay.gameObject.AddComponent<FightHud>();
            hud.replay = replay;
            hud.nameFont = ReferenceAssets.Font(Fonts + "Norse/Norsebold.otf");
            hud.textFont = ReferenceAssets.Font(Fonts + "Averia_Serif_Libre/AveriaSerifLibre-Bold.ttf");
        }

        /// <summary>The chest's base follows the body bone and its lid the lid bone, placed as in the rest pose.</summary>
        private static void AttachChest(Transform mimic)
        {
            var stone = new Material(Shader.Find("Standard")) { name = "stonechest_preview" };
            stone.SetTexture("_MainTex", ReferenceAssets.Texture(Chest + "materials/stonechest_d.png", false));
            stone.SetTexture("_BumpMap", ReferenceAssets.Texture(Chest + "materials/stonechest_n.png", true));
            stone.EnableKeyword("_NORMALMAP");
            stone.SetFloat("_Glossiness", 0.1f);
            AssetDatabase.CreateAsset(stone, ReferenceAssets.Folder + "/stonechest_preview.mat");
            Hang(mimic, "body", "chest_base", ReferenceAssets.Mesh(Chest + "models/stonechest.asset"), stone);
            Hang(mimic, "lid", "chest_lid", ReferenceAssets.Mesh(Chest + "models/stonechesttop.asset"), stone);
        }

        private static void Hang(Transform root, string bone, string name, Mesh mesh, Material material)
        {
            if (mesh == null)
            {
                Log.Error("reference mesh for " + name + " did not load; the preview shows the mimic without its chest");
                return;
            }
            var part = new GameObject(name);
            part.transform.SetPositionAndRotation(root.position, root.rotation);
            part.transform.SetParent(CreaturePrefab.Find(root, bone), true);
            part.AddComponent<MeshFilter>().sharedMesh = mesh;
            part.AddComponent<MeshRenderer>().sharedMaterial = material;
        }
    }
}
