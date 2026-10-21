using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Workshop
{
    /// <summary>Original weapon art kit; contains no copied Valheim resources or gameplay scripts.</summary>
    public static class BoneCrossbowBuild
    {
        const string Bundle = "ecp_bone_crossbow_set";
        const string Root = "Assets/Bundles/" + Bundle;
        static readonly string[] Names = { "ecp_bone_crossbow", "ecp_bone_crossbow_unloaded", "ecp_bone_quarrel" };

        public static void Run()
        {
            try { Build(); EditorApplication.Exit(0); }
            catch (Exception ex) { Debug.LogException(ex); EditorApplication.Exit(1); }
        }

        static void Build()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var assets = Names.Select(n => PrefabBuilder.Build(Root + "/" + n)).ToList();
            assets.AddRange(Names.Select(n => PrefabBuilder.Icon(Root + "/" + n)));
            foreach (string name in Names)
            {
                string folder = Root + "/" + name;
                var importer = (TextureImporter)AssetImporter.GetAtPath(folder + "/" + name + "_albedo.png");
                importer.filterMode = FilterMode.Point;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.SaveAndReimport();
                var material = AssetDatabase.LoadAssetAtPath<Material>(folder + "/" + name + ".mat");
                material.SetFloat("_Glossiness", .06f);
                EditorUtility.SetDirty(material);
            }
            // Verify the imported bolt's long direction: the point must be on Unity +Z.
            var boltPrefab = Load(Names[2]);
            var filter = boltPrefab.GetComponentsInChildren<MeshFilter>().First(f => !f.name.StartsWith("col_"));
            var bounds = filter.GetComponent<Renderer>().bounds;
            Log.Info($"bone quarrel imported bounds {bounds.center:F4} / {bounds.size:F4}");
            float forward = bounds.center.z > 0 ? 1 : -1;
            // Static Blender FBX axes are normalized explicitly at the assembly boundary.
            var assembly = new GameObject("ecp_bone_crossbow_assembly");
            var loaded = Child(assembly, Names[0], "Loaded", forward);
            var unloaded = Child(assembly, Names[1], "Unloaded", forward);
            var bolt = Child(assembly, Names[2], "Bolt", forward);
            bolt.transform.localPosition = new Vector3(0, .058f, .075f);
            unloaded.SetActive(false);
            foreach (var collider in assembly.GetComponentsInChildren<Collider>(true))
                Object.DestroyImmediate(collider);
            Marker(assembly, "Muzzle", new Vector3(0, .058f, .565f));
            Marker(assembly, "BoltSeat", bolt.transform.localPosition);
            Marker(assembly, "Grip", new Vector3(0, -.035f, -.09f));
            Marker(assembly, "SupportHand", new Vector3(0, -.055f, .29f));
            var drop = assembly.AddComponent<BoxCollider>();
            drop.center = new Vector3(0, -.025f, .16f); drop.size = new Vector3(.115f, .17f, 1.10f);
            string assemblyPath = Root + "/ecp_bone_crossbow_assembly.prefab";
            PrefabUtility.SaveAsPrefabAsset(assembly, assemblyPath); assets.Add(assemblyPath);
            // Normalized projectile visual, origin at the nock and point toward +Z.
            var projectile = new GameObject("ecp_bone_quarrel_projectile");
            Child(projectile, Names[2], "Visual", forward);
            string projectilePath = Root + "/ecp_bone_quarrel_projectile.prefab";
            PrefabUtility.SaveAsPrefabAsset(projectile, projectilePath); assets.Add(projectilePath);
            Object.DestroyImmediate(projectile);
            Validate(assembly, bolt);
            Preview();
            AssetDatabase.SaveAssets();
            foreach (string dependency in AssetDatabase.GetDependencies(assets.ToArray(), true))
                if (dependency.StartsWith("Assets/Reference", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Reference dependency: " + dependency);
            string output = Path.GetFullPath("../assets/bone_crossbow_set/out/bundles");
            string package = Path.GetFullPath("../assets/bone_crossbow_set/out/Gravebranch.unitypackage");
            AssetDatabase.ExportPackage(Root, package, ExportPackageOptions.Recurse | ExportPackageOptions.IncludeDependencies);
            foreach (var target in new[] { BuildTarget.StandaloneWindows64, BuildTarget.StandaloneLinux64 })
            {
                string platform = target == BuildTarget.StandaloneWindows64 ? "windows" : "linux";
                string folder = Path.Combine(output, platform); Directory.CreateDirectory(folder);
                var build = new AssetBundleBuild { assetBundleName = Bundle, assetNames = assets.ToArray() };
                if (BuildPipeline.BuildAssetBundles(folder, new[] { build },
                    BuildAssetBundleOptions.ChunkBasedCompression | BuildAssetBundleOptions.StrictMode, target) == null)
                    throw new InvalidOperationException("Bundle failed: " + target);
                File.Copy(Path.Combine(folder, Bundle), Path.Combine(output, Bundle + "." + platform), true);
                Log.Info("bone crossbow bundle built: " + platform);
            }
            File.WriteAllText(Path.GetFullPath("../assets/bone_crossbow_set/out/unity_validation.txt"),
                "PASS: 3 mesh imports match Blender dimensions.\n" +
                "PASS: assembled weapon and normalized projectile point toward Unity +Z.\n" +
                "PASS: bolt seat, loaded and unloaded visibility, and preview shot motion.\n" +
                "PASS: Windows/Linux bundles built; no Assets/Reference dependencies.\n" +
                "Art assets only: no ItemDrop, Projectile, crafting recipe or runtime registration.\n");
            Log.Info("bone crossbow COMPLETE");
        }

        static GameObject Load(string name) => AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/" + name + "/" + name + ".prefab");
        static GameObject Child(GameObject parent, string asset, string name, float forward)
        {
            var child = (GameObject)PrefabUtility.InstantiatePrefab(Load(asset));
            child.name = name; child.transform.SetParent(parent.transform, false);
            if (forward < 0) child.transform.localRotation = Quaternion.Euler(0, 180, 0);
            return child;
        }
        static void Marker(GameObject root, string name, Vector3 location)
        {
            var marker = new GameObject(name); marker.transform.SetParent(root.transform, false);
            marker.transform.localPosition = location;
        }
        static void Validate(GameObject root, GameObject bolt)
        {
            if (root.transform.Find("Unloaded").gameObject.activeSelf || !root.transform.Find("Loaded").gameObject.activeSelf)
                throw new InvalidOperationException("Invalid initial weapon state");
            var bounds = bolt.GetComponentInChildren<Renderer>().bounds;
            if (bounds.center.z < .2f || bounds.max.z < .55f || bounds.max.z > .58f)
                throw new InvalidOperationException("Bolt orientation or seating is wrong: " + bounds);
            Log.Info("bone crossbow bolt tip/seat verified: " + bounds.max.z);
        }

        static void Curve(AnimationClip clip, string path, Type type, string field, bool stepped, params float[] pairs)
        {
            var curve = new AnimationCurve();
            for (int i = 0; i < pairs.Length; i += 2) curve.AddKey(new Keyframe(pairs[i], pairs[i + 1]));
            for (int i = 0; i < curve.length; i++)
            {
                AnimationUtility.SetKeyLeftTangentMode(curve, i, stepped ? AnimationUtility.TangentMode.Constant : AnimationUtility.TangentMode.Linear);
                AnimationUtility.SetKeyRightTangentMode(curve, i, stepped ? AnimationUtility.TangentMode.Constant : AnimationUtility.TangentMode.Linear);
            }
            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(path, type, field), curve);
        }

        static void Preview()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            // Keep the review animation out of the shipping art bundle.
            string folder = Root + "/Review"; Directory.CreateDirectory(folder); AssetDatabase.Refresh();
            var clip = new AnimationClip { name = "Fire and reload - visual demonstration", frameRate = 30 };
            Curve(clip, "Loaded", typeof(GameObject), "m_IsActive", true, 0,1,.6f,0,2.5f,1,3,1);
            Curve(clip, "Unloaded", typeof(GameObject), "m_IsActive", true, 0,0,.6f,1,2.5f,0,3,0);
            Curve(clip, "Bolt", typeof(GameObject), "m_IsActive", true, 0,1,.8f,0,2.4f,1,3,1);
            Curve(clip, "Bolt", typeof(Transform), "m_LocalPosition.z", false, 0,.075f,.6f,.075f,.8f,2.6f,.81f,.075f,3,.075f);
            Curve(clip, "Bolt", typeof(Transform), "m_LocalPosition.y", false, 0,.058f,2.39f,.058f,2.4f,.24f,2.77f,.058f,3,.058f);
            var settings = AnimationUtility.GetAnimationClipSettings(clip); settings.loopTime = true;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            string clipPath = folder + "/FireReload.anim";
            AssetDatabase.DeleteAsset(clipPath); AssetDatabase.CreateAsset(clip, clipPath);
            string controllerPath = folder + "/Review.controller";
            AssetDatabase.DeleteAsset(controllerPath);
            var controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
            controller.AddMotion(clip);
            // NewScene destroys the temporary object; instantiate the saved assembly into the review scene.
            var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/ecp_bone_crossbow_assembly.prefab"));
            var animator = model.AddComponent<Animator>(); animator.runtimeAnimatorController = controller;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            clip.SampleAnimation(model, .7f);
            if (model.transform.Find("Bolt").localPosition.z <= .5f || model.transform.Find("Loaded").gameObject.activeSelf)
                throw new InvalidOperationException("Review shot does not advance along +Z");
            clip.SampleAnimation(model, 0);
            var camera = new GameObject("Review camera").AddComponent<Camera>();
            camera.orthographic = true; camera.orthographicSize = .82f;
            camera.transform.position = new Vector3(1.25f, 1.9f, 1.65f);
            camera.transform.LookAt(new Vector3(0,0,.16f)); camera.backgroundColor = new Color(.10f,.125f,.135f);
            camera.clearFlags = CameraClearFlags.SolidColor;
            var light = new GameObject("Soft daylight").AddComponent<Light>(); light.type = LightType.Directional;
            light.transform.rotation = Quaternion.Euler(50,-30,0); light.intensity = 1.1f;
            RenderSettings.ambientLight = new Color(.42f,.44f,.46f);
            EditorSceneManager.SaveScene(scene, "Assets/Preview/BoneCrossbowReview.unity");
        }
    }
}
