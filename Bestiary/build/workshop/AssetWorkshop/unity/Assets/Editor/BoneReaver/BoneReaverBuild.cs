using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Workshop
{
    public static class BoneReaverBuild
    {
        private const string Folder = "Assets/Creatures/ecp_bone_reaver";
        private const string Local = "Assets/Reference/BoneReaver";

        public static void Run()
        {
            try
            {
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                Build(Folder, "ecp_bone_reaver", false);
                Build(Local, "ecp_bone_reaver_preview", true);
                BoneReaverEffects.Build(Folder);
                AssetDatabase.SaveAssets();
                EditorApplication.Exit(0);
            }
            catch (Exception error)
            {
                Debug.LogException(error);
                EditorApplication.Exit(1);
            }
        }

        private static void Build(string folder, string name, bool reference)
        {
            var info = JsonUtility.FromJson<ReaverManifest>(File.ReadAllText(folder + "/" + name + ".json"));
            string path = folder + "/" + name + ".fbx";
            Import(path, folder, info);
            var clips = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>()
                .Where(c => !c.name.StartsWith("__preview__")).ToArray();
            if (clips.Length != info.clips.Length) throw new Exception("Missing animation clips: " + path);
            var controller = Controller(folder, name, clips, info);
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var instance = UnityEngine.Object.Instantiate(model);
            instance.name = name;
            instance.GetComponent<Animator>().runtimeAnimatorController = controller;
            instance.GetComponent<Animator>().applyRootMotion = false;
            PrefabUtility.SaveAsPrefabAsset(instance, folder + "/" + name + ".prefab");
            Validate(instance, info, reference);
            UnityEngine.Object.DestroyImmediate(instance);
            File.WriteAllText(folder + "/validation.txt", $"Imported {clips.Length} clips; height multiplier 1.25; reference body: {reference}\n");
        }

        private static void Import(string path, string folder, ReaverManifest info)
        {
            var importer = (ModelImporter)AssetImporter.GetAtPath(path);
            importer.globalScale = 1; importer.useFileScale = true;
            importer.bakeAxisConversion = true;
            importer.animationType = ModelImporterAnimationType.Generic;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.importAnimation = true; importer.importCameras = false; importer.importLights = false;
            importer.animationCompression = ModelImporterAnimationCompression.Off;
            importer.SaveAndReimport();
            importer.clipAnimations = importer.defaultClipAnimations.Select(c => Clip(c, info)).ToArray();
            foreach (var material in info.materials)
                importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), material.name),
                    Material(folder, material));
            importer.SaveAndReimport();
        }

        private static ModelImporterClipAnimation Clip(ModelImporterClipAnimation clip, ReaverManifest info)
        {
            clip.name = clip.takeName.Split('|').Last();
            var spec = info.clips.First(c => c.name == clip.name);
            clip.loopTime = spec.loop;
            clip.lockRootRotation = true; clip.lockRootHeightY = true; clip.lockRootPositionXZ = true;
            clip.keepOriginalOrientation = true; clip.keepOriginalPositionY = true;
            clip.events = spec.cues.Select(c => new AnimationEvent {
                functionName = "ReaverCue", stringParameter = c.@event, time = (float)c.frame / spec.last
            }).ToArray();
            return clip;
        }

        private static Material Material(string folder, ReaverMaterial info)
        {
            string path = folder + "/" + info.name + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null) { mat = new Material(Shader.Find("Standard")); AssetDatabase.CreateAsset(mat, path); }
            mat.color = new Color(info.color[0], info.color[1], info.color[2]).gamma;
            mat.SetFloat("_Glossiness", .12f);
            if (!string.IsNullOrEmpty(info.texture)) Texture(mat, folder, info.texture);
            if (info.emissionStrength > 0)
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", new Color(info.emission[0], info.emission[1], info.emission[2]) * info.emissionStrength);
            }
            return mat;
        }

        private static void Texture(Material mat, string folder, string file)
        {
            string path = folder + "/" + file;
            if (!File.Exists(path)) path = Folder + "/" + file;
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.filterMode = FilterMode.Point; importer.wrapMode = TextureWrapMode.Repeat;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
            mat.mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static AnimatorController Controller(string folder, string name, AnimationClip[] clips, ReaverManifest info)
        {
            string path = folder + "/" + name + ".controller";
            AssetDatabase.DeleteAsset(path);
            var result = AnimatorController.CreateAnimatorControllerAtPath(path);
            var machine = result.layers[0].stateMachine;
            var states = clips.ToDictionary(c => c.name, c => machine.AddState(c.name));
            foreach (var clip in clips) states[clip.name].motion = clip;
            machine.defaultState = states["idle_R"];
            foreach (var spec in info.clips)
            {
                result.AddParameter(spec.name, AnimatorControllerParameterType.Trigger);
                var enter = machine.AddAnyStateTransition(states[spec.name]);
                enter.AddCondition(AnimatorConditionMode.If, 0, spec.name); enter.duration = .08f;
                enter.canTransitionToSelf = false; states[spec.name].tag = spec.tag;
                if (spec.loop) continue;
                var leave = states[spec.name].AddTransition(states["idle_" + spec.endSide]);
                leave.hasExitTime = true; leave.exitTime = 1; leave.duration = .06f;
            }
            return result;
        }

        private static void Validate(GameObject model, ReaverManifest info, bool reference)
        {
            var bones = model.GetComponentsInChildren<Transform>(true);
            foreach (string name in new[] { "Hips", "Head", "LeftHand", "RightHand", "axe", "daggers" })
                if (!bones.Any(b => b.name == name)) throw new Exception("Missing bone: " + name);
            foreach (var skin in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                if (skin.sharedMesh.vertexCount == 0 || skin.bones.Any(b => b == null))
                    throw new Exception("Invalid skin: " + skin.name);
            if (!reference && AssetDatabase.GetDependencies(Folder + "/ecp_bone_reaver.prefab", true)
                .Any(p => p.StartsWith("Assets/Reference"))) throw new Exception("Reference asset leaked into kit");
            Debug.Log($"Bone Reaver validated: {bones.Length} transforms, {info.clips.Length} clips, reference={reference}");
        }
    }

    [Serializable] public class ReaverManifest { public ReaverClip[] clips; public ReaverMaterial[] materials; }
    [Serializable] public class ReaverClip { public string name, tag, startSide, endSide; public int last; public bool loop; public ReaverCue[] cues; }
    [Serializable] public class ReaverCue { public int frame; public string @event; }
    [Serializable] public class ReaverMaterial { public string name, texture; public float[] color, emission; public float emissionStrength; }
}
