using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Workshop.Crossbow;
using Workshop.Greataxe;

namespace Workshop.Headsman
{
    /// <summary>
    /// The Crypt Executioner's bundle for Elite Creatures Pack, ecp_headsman.windows and .linux (HeadsmanBuild with
    /// -workshopBundle &lt;folder&gt;; assets/ecp_headsman/build.ps1 -Bundle [-Install]):
    ///   - the nine clips, the attacks carrying their hit events, and <see cref="HeadsmanController"/>;
    ///   - ecp_headsman_kit: a "RightHand" mount holding the axe as the right fist holds it, measured on the game's
    ///     Skeleton at its own size (the mod hangs the mount on the creature's RightHand with no offset, as the
    ///     crossbowman's kit hangs); the axe (ecp_headsman_axe) is its pieces (<see cref="HeadsmanPieces"/>), bottom up;
    ///   - ecp_greataxe_held: the players' greataxe in the frame of a hand's attach point (<see cref="GreataxeModel"/>);
    ///   - ecp_greataxe_axehead: the axehead the boss drops, the pieces above the haft, centred;
    ///   - the sprites ecp_greataxe_icon and ecp_greataxe_axehead_icon (assets/ecp_headsman/icons.py).
    /// Every material is a placeholder the mod dresses onto the game's own.
    /// </summary>
    public static class HeadsmanBundle
    {
        public const string Bundle = "ecp_headsman";
        public const string Kit = "ecp_headsman_kit", Axe = "ecp_headsman_axe", Held = "ecp_greataxe_held", Axehead = "ecp_greataxe_axehead";
        public static readonly string[] IconNames = { "ecp_greataxe_icon", "ecp_greataxe_axehead_icon" };
        private const string Out = HeadsmanBuild.Folder + "/bundle";
        private static readonly (BuildTarget target, string suffix)[] Targets =
            { (BuildTarget.StandaloneWindows64, "windows"), (BuildTarget.StandaloneLinux64, "linux") };

        public static void Build(GameObject skeleton, HeadsmanGrip grip, HeadsmanMove[] moves, string outFolder)
        {
            AssetDatabase.DeleteAsset(Out);
            AssetDatabase.CreateFolder(HeadsmanBuild.Folder, "bundle");
            var (mesh, material, frame) = HeadsmanPieces.Axe();
            List<(string name, Mesh mesh)> pieces = HeadsmanPieces.Split(mesh, frame).Select(p => (p.name, Save(HeadsmanPieces.Piece(mesh, frame, p.triangles, p.name)))).ToList();
            var assets = new List<string> { KitPrefab(skeleton, grip, pieces, material), HeldPrefab(), AxeheadPrefab(pieces, material), HeadsmanController.Build(moves) };
            assets.AddRange(HeadsmanBuild.ClipNames(moves).Select(HeadsmanBuild.ClipPath));
            assets.AddRange(Icons());
            Log.Info($"headsman bundle: {pieces.Count} axe pieces, {assets.Count} assets");
            Bundles(assets.ToArray(), outFolder);
        }

        private static Mesh Save(Mesh mesh)
        {
            AssetDatabase.CreateAsset(mesh, $"{Out}/{mesh.name}.asset");
            return mesh;
        }

        /// <summary>The axe laid together from its pieces, each on its own renderer, in the axe's frame.</summary>
        private static GameObject Assembled(string name, IEnumerable<(string name, Mesh mesh)> pieces, Material material)
        {
            var axe = new GameObject(name);
            foreach (var (piece, mesh) in pieces)
            {
                var part = new GameObject(piece);
                part.transform.SetParent(axe.transform, false);
                part.AddComponent<MeshFilter>().sharedMesh = mesh;
                part.AddComponent<MeshRenderer>().sharedMaterial = material;
            }
            return axe;
        }

        private static string KitPrefab(GameObject skeleton, HeadsmanGrip grip, List<(string name, Mesh mesh)> pieces, Material material)
        {
            Transform hand = XbowReference.Bone(skeleton, "RightHand");
            Pose at = grip.AxeIn(hand);
            var kit = new GameObject(Kit);
            var mount = new GameObject("RightHand").transform;
            mount.SetParent(kit.transform, false);
            Transform axe = Assembled(Axe, pieces, material).transform;
            axe.SetParent(mount, false);
            axe.localPosition = hand.InverseTransformPoint(at.position);
            axe.localRotation = Quaternion.Inverse(hand.rotation) * at.rotation;
            axe.localScale = Vector3.one * (skeleton.transform.lossyScale.x / hand.lossyScale.x);
            return SavePrefab(kit);
        }

        private static string HeldPrefab()
        {
            GameObject held = GreataxeModel.Attach();
            held.name = Held;
            return SavePrefab(held);
        }

        /// <summary>The pieces above the haft (the blade's shards, the back, the vertebrae through the head) as one mesh, centred.</summary>
        private static string AxeheadPrefab(List<(string name, Mesh mesh)> pieces, Material material)
        {
            CombineInstance[] parts = pieces.Where(p => p.mesh.bounds.center.y > HeadsmanPieces.HeadStart)
                .Select(p => new CombineInstance { mesh = p.mesh, transform = Matrix4x4.identity }).ToArray();
            var head = new Mesh { name = Axehead };
            head.CombineMeshes(parts, true, true);
            Vector3 middle = head.bounds.center;
            head.vertices = head.vertices.Select(v => (v - middle) * GreataxeModel.Scale).ToArray();
            head.RecalculateBounds();
            Save(head);
            var root = new GameObject(Axehead);
            var model = new GameObject("model");
            model.transform.SetParent(root.transform, false);
            model.AddComponent<MeshFilter>().sharedMesh = head;
            model.AddComponent<MeshRenderer>().sharedMaterial = material;
            return SavePrefab(root);
        }

        private static string SavePrefab(GameObject root)
        {
            string path = $"{Out}/{root.name}.prefab";
            PrefabUtility.SaveAsPrefabAsset(root, path);
            UnityEngine.Object.DestroyImmediate(root);
            return path;
        }

        /// <summary>The inventory icons, staged by build.ps1 from icons.py, as sprites.</summary>
        private static IEnumerable<string> Icons()
        {
            foreach (string icon in IconNames)
            {
                string path = $"{HeadsmanBuild.Folder}/icons/{icon}.png";
                if (!File.Exists(path))
                    throw new InvalidOperationException($"no {path}: run assets/ecp_headsman/icons.py (build.ps1 -Bundle does)");
                ImportSettings.Sprite(path);
                yield return path;
            }
        }

        private static void Bundles(string[] assets, string outFolder)
        {
            string[] leaked = AssetDatabase.GetDependencies(assets, true).Where(a => a.StartsWith(ReferenceAssets.Folder)).ToArray();
            if (leaked.Length > 0)
                throw new InvalidOperationException("the bundle would carry the game's assets: " + string.Join(", ", leaked));
            var build = new AssetBundleBuild { assetBundleName = Bundle, assetNames = assets };
            var options = BuildAssetBundleOptions.ChunkBasedCompression | BuildAssetBundleOptions.StrictMode;
            foreach (var (target, suffix) in Targets)
            {
                string folder = Path.Combine(outFolder, suffix);
                Directory.CreateDirectory(folder);
                if (BuildPipeline.BuildAssetBundles(folder, new[] { build }, options, target) == null)
                    throw new InvalidOperationException("bundle build failed for " + target);
                string file = Path.Combine(outFolder, Bundle + "." + suffix);
                File.Copy(Path.Combine(folder, Bundle), file, true);
                Log.Info($"bundle {file}: {new FileInfo(file).Length} bytes");
            }
        }
    }
}
