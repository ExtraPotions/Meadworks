// Bestiary bundle build: kits from exported transform trees. Copyright (C) 2026 expDARE (ExtraPotions Meadworks).
// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Bestiary.Build
{
    /// <summary>A bundle's prefab transform trees as unity_build.py stages them (from ecp_bundles.py export-kits).</summary>
    [Serializable]
    public class KitTree
    {
        public KitNode[] nodes;
    }

    [Serializable]
    public class KitNode
    {
        public string name, path;
        public int parent;
        public float[] position, rotation, scale;
    }

    /// <summary>
    /// The crossbowman's and the slinger's bundles. The pinned workshop measures their kits on the game's Skeleton and
    /// Greydwarf from its local game export, which Bestiary never uses (design 10.1 rule 7); instead each kit is laid
    /// out from the transform tree of EliteCreaturesPack's own bundle (names, parents, local position, rotation, scale:
    /// the author's measurements, exported by ecp_bundles.py), and every leaf named after a rebuilt mesh gets that mesh
    /// and its part material. The clips come from the authored-clip export (clip_curves.py), the nine excluded clips
    /// never among them.
    /// </summary>
    public static class BestiaryKits
    {
        public static string[] Crossbowman(string bundle) => KitBundle(bundle, true);

        public static string[] Slinger(string bundle) => KitBundle(bundle, false);

        /// <summary>Parts, then every tree root (a part's own prefab when it is one, else a kit), clips and icons.</summary>
        private static string[] KitBundle(string bundle, bool partRoots)
        {
            string folder = BestiaryBundles.BundlesFolder + "/" + bundle;
            var assets = new List<string>();
            var parts = new Dictionary<string, string>();
            var meshes = new Dictionary<string, (Mesh mesh, Material material)>();
            foreach (string part in BestiaryBundles.PartFolders(bundle))
            {
                string prefab = Workshop.PrefabBuilder.Build(part);
                parts[Path.GetFileName(part)] = prefab;
                AddMeshes(prefab, meshes);
                string icon = Workshop.PrefabBuilder.Icon(part);
                if (icon != null)
                    assets.Add(icon);
            }
            KitTree tree = ReadTree(folder);
            foreach (KitNode root in tree.nodes.Where(n => n.parent < 0))
            {
                if (partRoots && parts.TryGetValue(root.name, out string prefab))
                    assets.Add(prefab);
                else
                    assets.Add(SaveTree(tree, root, meshes, folder + "/_built"));
            }
            assets.AddRange(Clips(folder));
            return assets.ToArray();
        }

        /// <summary>Every rebuilt clip of the bundle, from its staged curve records.</summary>
        public static IEnumerable<string> Clips(string folder)
        {
            string curves = folder + "/_curves";
            if (!Directory.Exists(curves))
                yield break;
            Directory.CreateDirectory(folder + "/_clips");
            foreach (string record in Directory.GetFiles(curves, "*.json").OrderBy(p => p, StringComparer.Ordinal))
            {
                string path = $"{folder}/_clips/{Path.GetFileNameWithoutExtension(record)}.anim";
                BestiaryClips.Build(record, path);
                yield return path;
            }
        }

        /// <summary>Each renderer of a part prefab, by its GameObject's name, with its mesh and material.</summary>
        public static void AddMeshes(string prefab, Dictionary<string, (Mesh, Material)> meshes)
        {
            var root = AssetDatabase.LoadAssetAtPath<GameObject>(prefab);
            foreach (var filter in root.GetComponentsInChildren<MeshFilter>(true))
            {
                var renderer = filter.GetComponent<MeshRenderer>();
                if (renderer != null)
                    meshes[filter.gameObject.name] = (filter.sharedMesh, renderer.sharedMaterial);
            }
        }

        public static KitTree ReadTree(string folder) =>
            JsonUtility.FromJson<KitTree>(File.ReadAllText(folder + "/_kit.json"));

        /// <summary>Saves the tree under root as a prefab; a leaf named (or pathed) after a known mesh renders it.</summary>
        public static string SaveTree(KitTree tree, KitNode root, Dictionary<string, (Mesh mesh, Material material)> meshes, string outFolder)
        {
            Directory.CreateDirectory(outFolder);
            int rootIndex = Array.IndexOf(tree.nodes, root);
            var made = new Dictionary<int, GameObject>();
            int renderers = 0;
            for (int i = 0; i < tree.nodes.Length; i++)
            {
                if (!Under(tree, i, rootIndex))
                    continue;
                KitNode node = tree.nodes[i];
                var go = new GameObject(node.name);
                if (i != rootIndex)
                    go.transform.SetParent(made[node.parent].transform, false);
                go.transform.localPosition = new Vector3(node.position[0], node.position[1], node.position[2]);
                go.transform.localRotation = new Quaternion(node.rotation[0], node.rotation[1], node.rotation[2], node.rotation[3]);
                go.transform.localScale = new Vector3(node.scale[0], node.scale[1], node.scale[2]);
                made[i] = go;
                bool leaf = !tree.nodes.Any(n => n.parent == i);
                if (leaf && (meshes.TryGetValue(node.path, out var part) || meshes.TryGetValue(node.name, out part)))
                {
                    go.AddComponent<MeshFilter>().sharedMesh = part.mesh;
                    go.AddComponent<MeshRenderer>().sharedMaterial = part.material;
                    renderers++;
                }
            }
            string path = $"{outFolder}/{root.name}.prefab";
            PrefabUtility.SaveAsPrefabAsset(made[rootIndex], path);
            UnityEngine.Object.DestroyImmediate(made[rootIndex]);
            BestiaryBundles.Line($"kit {root.name}: {made.Count} transforms, {renderers} renderers");
            return path;
        }

        /// <summary>Whether node i is root or below it (parents come before children in the staged list).</summary>
        private static bool Under(KitTree tree, int i, int root)
        {
            for (int n = i; n >= 0; n = tree.nodes[n].parent)
                if (n == root)
                    return true;
            return false;
        }
    }
}
