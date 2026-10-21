using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Workshop.GameRig
{
    /// <summary>
    /// The game creature a body is built for, staged from the reference export (rip-reference.ps1) for the checks and
    /// previews only: the game prefab itself, its body mesh, avatar, the body's albedo and every clip its controller
    /// plays, each kept under the game's GUID and reused when another workshop tool already brought it in (so no two
    /// copies fight for a GUID), plus a copy of the controller under a GUID of its own in Assets/Reference/GameRig (the
    /// staged prefab keeps no controller, as other tools expect). Nothing here ever goes into a bundle.
    /// </summary>
    public static class GameRigStaging
    {
        public const string Subfolder = "GameRig";

        private static string Root => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "ValheimReference", "ExportedProject", "Assets");

        /// <summary>A fresh instance of the game creature, scripts and colliders stripped, dressed in a preview material.</summary>
        public static GameObject Creature(GameRigReference reference, string name)
        {
            foreach (string path in new[] { reference.bodyMesh, reference.avatar }.Concat(reference.clips))
                Stage(path);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Stage(reference.prefab));
            var creature = UnityEngine.Object.Instantiate(prefab);
            creature.name = name;
            creature.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            Strip(creature);
            Dress(creature, reference);
            Animator animator = creature.GetComponentInChildren<Animator>(true);
            animator.runtimeAnimatorController = Controller(reference.controller);
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;   // no camera looks at it while it is baked
            animator.applyRootMotion = false;
            return creature;
        }

        /// <summary>The project path of a reference file: the copy already in the project (by the game's GUID), else a new one.</summary>
        public static string Stage(string referencePath)
        {
            string guid = GameGuid(referencePath);
            string existing = AssetDatabase.GUIDToAssetPath(guid);
            if (!string.IsNullOrEmpty(existing) && File.Exists(existing))
                return existing;
            return ReferenceAssets.Import(referencePath, Subfolder);
        }

        /// <summary>The game's controller, copied once under a GUID of its own; its clips resolve by the game's GUIDs.</summary>
        public static AnimatorController Controller(string referencePath)
        {
            string folder = ReferenceAssets.Folder + "/" + Subfolder;
            Directory.CreateDirectory(folder);
            string target = folder + "/" + Path.GetFileName(referencePath);
            if (!File.Exists(target))
            {
                File.Copy(Path.Combine(Root, referencePath), target);
                AssetDatabase.ImportAsset(target, ImportAssetOptions.ForceSynchronousImport);
            }
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(target);
            if (controller == null)
                throw new InvalidOperationException("the game's controller did not import: " + referencePath);
            return controller;
        }

        /// <summary>The body renderer of a staged creature: the one at the contract's path.</summary>
        public static SkinnedMeshRenderer Body(GameObject creature, string rendererPath)
        {
            Transform node = creature.transform.Find(rendererPath);
            var body = node == null ? null : node.GetComponent<SkinnedMeshRenderer>();
            return body != null ? body : throw new InvalidOperationException("no body renderer at " + rendererPath);
        }

        /// <summary>A game texture, staged by GUID, point filtered like the game's.</summary>
        public static Texture2D Texture(string referencePath)
        {
            string path = Stage(referencePath);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (importer.filterMode != FilterMode.Point)
            {
                importer.filterMode = FilterMode.Point;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static string GameGuid(string referencePath)
        {
            string meta = Path.Combine(Root, referencePath + ".meta");
            if (!File.Exists(meta))
                throw new FileNotFoundException("not in the reference export; run rip-reference.ps1", meta);
            Match match = Regex.Match(File.ReadAllText(meta), @"guid: (\w+)");
            return match.Success ? match.Groups[1].Value : "";
        }

        /// <summary>The game's scripts are missing here; its colliders, LODs and particles would only get in the way.</summary>
        private static void Strip(GameObject creature)
        {
            foreach (var child in creature.GetComponentsInChildren<Transform>(true))
                GameObjectUtility.RemoveMonoBehavioursWithMissingScript(child.gameObject);
            foreach (var collider in creature.GetComponentsInChildren<Collider>(true))
                UnityEngine.Object.DestroyImmediate(collider);
            foreach (var body in creature.GetComponentsInChildren<Rigidbody>(true))
                UnityEngine.Object.DestroyImmediate(body);
            foreach (var particles in creature.GetComponentsInChildren<ParticleSystem>(true))
                UnityEngine.Object.DestroyImmediate(particles.gameObject);
            foreach (var group in creature.GetComponentsInChildren<LODGroup>(true))
                UnityEngine.Object.DestroyImmediate(group);
            foreach (var skin in creature.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                skin.forceMatrixRecalculationPerRender = true;   // batch mode skins once and keeps it otherwise
        }

        /// <summary>Standard materials on the game's own albedo (point filtered), for the side-by-side stills.</summary>
        private static void Dress(GameObject creature, GameRigReference reference)
        {
            var body = new Material(Shader.Find("Standard")) { name = "game_body_preview" };
            if (!string.IsNullOrEmpty(reference.bodyAlbedo))
                body.mainTexture = Texture(reference.bodyAlbedo);
            body.SetFloat("_Glossiness", 0.15f);
            var glow = new Material(Shader.Find("Standard")) { name = "game_glow_preview", color = new Color(1f, 0.35f, 0.1f) };
            glow.EnableKeyword("_EMISSION");
            glow.SetColor("_EmissionColor", new Color(2f, 0.7f, 0.2f));
            foreach (var renderer in creature.GetComponentsInChildren<Renderer>(true))
                renderer.sharedMaterials = renderer.sharedMaterials.Select(_ => renderer is SkinnedMeshRenderer ? body : glow).ToArray();
        }
    }
}
