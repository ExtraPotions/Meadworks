using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Workshop
{
    /// <summary>
    /// Imports a creature FBX as a generic rig: metres, Blender's axes baked in, root motion from the root bone,
    /// one clip per Blender action (looping as the manifest says) with an OnAttackTrigger event on each bite frame,
    /// and the part materials remapped to bare Standard-shader materials (the manifest's colours and glow, no texture).
    /// The model and prefab therefore depend on nothing of the game's and can go into a bundle as they are; the game's
    /// textures are put on only in the preview scene (<see cref="PreviewMaterials"/>) and, in game, by the mod.
    /// </summary>
    public static class CreatureImport
    {
        public static void Model(string folder, CreatureManifest info)
        {
            string path = folder + "/" + info.fbx;
            var importer = (ModelImporter)AssetImporter.GetAtPath(path);
            importer.globalScale = 1f;
            importer.useFileScale = true;
            importer.bakeAxisConversion = true;
            importer.animationType = ModelImporterAnimationType.Generic;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.importAnimation = true;
            importer.importCameras = false;
            importer.importLights = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            importer.SaveAndReimport();
            importer.motionNodeName = string.IsNullOrEmpty(info.rootBone) ? "" : RootPath(path, info.rootBone);
            importer.clipAnimations = importer.defaultClipAnimations.Select(c => Clip(c, info)).ToArray();
            foreach (var material in info.materials)
                importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), material.name),
                    CreateMaterial(folder, material));
            importer.SaveAndReimport();
            Log.Info($"creature {info.asset}: root motion from '{(importer.motionNodeName.Length > 0 ? importer.motionNodeName : "none")}', {importer.clipAnimations.Length} clips");
        }

        /// <summary>The root bone's path below the model root, which is how Unity names the motion node.</summary>
        private static string RootPath(string fbx, string bone)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(fbx);
            var found = model.GetComponentsInChildren<Transform>().FirstOrDefault(t => t.name == bone);
            if (found == null)
                throw new System.InvalidOperationException($"{fbx} has no bone named {bone}");
            return AnimationUtility.CalculateTransformPath(found, model.transform);
        }

        private static ModelImporterClipAnimation Clip(ModelImporterClipAnimation take, CreatureManifest info)
        {
            string name = take.takeName.Split('|').Last();
            var clip = info.clips.First(c => c.name == name);
            take.name = name;
            take.loopTime = clip.loop;
            take.lockRootRotation = true;
            take.lockRootHeightY = true;
            take.keepOriginalOrientation = true;
            take.keepOriginalPositionY = true;
            float frames = Mathf.Max(1f, take.lastFrame - take.firstFrame);
            take.events = clip.events.Select(f => new AnimationEvent { functionName = "OnAttackTrigger", time = f / frames }).ToArray();
            return take;
        }

        private static Material CreateMaterial(string folder, MaterialInfo info)
        {
            var material = new Material(Shader.Find("Standard")) { name = info.name, color = info.Color };
            material.SetFloat("_Glossiness", info.smoothness);
            if (info.emissionStrength > 0)
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", info.Emission);
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            AssetDatabase.CreateAsset(material, folder + "/" + info.name + ".mat");
            return material;
        }
    }
}
