using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace Workshop.Kraken
{
    /// <summary>
    /// The game's ships (VikingShip.prefab, Karve.prefab) from the reference export, for the kraken's scale previews only: the
    /// prefab and the meshes, materials and textures it references (found by GUID in the export's .meta files) are
    /// copied into Assets/Reference/KrakenShip keeping their paths and GUIDs, the game's scripts, colliders, effects and
    /// the other hull states are stripped, and every material becomes a Standard one on the game's own texture. None of
    /// it goes into a bundle.
    /// </summary>
    public static class KrakenShip
    {
        public const string Longship = "GameElements/Ships/VikingShip.prefab", Karve = "GameElements/Ships/Karve.prefab";
        private const string Target = ReferenceAssets.Folder + "/KrakenShip";
        private static readonly string[] Kinds = { ".asset", ".mat", ".png", ".tga", ".jpg", ".psd" };
        private static readonly string[] Hidden = { "watermask", "shadow", "water", "glow", "blackhole", "smoke", "insect", "flare" };
        private static readonly Regex Guid = new Regex(@"guid: ([0-9a-f]{32})");

        private static string Root => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "ValheimReference", "ExportedProject", "Assets");

        /// <summary>A ship prefab from the reference export (Longship or Karve), stripped and dressed, at the origin.</summary>
        public static GameObject Load(string shipPrefab)
        {
            index ??= Index();
            var files = new HashSet<string>();
            Collect(shipPrefab, index, files, 0);
            int copied = files.Count(Copy);
            Copy(shipPrefab);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Target + "/" + shipPrefab);
            var ship = UnityEngine.Object.Instantiate(prefab);
            ship.name = prefab.name;
            Strip(ship);
            Dress(ship);
            Log.Info($"ship: {files.Count} files referenced, {copied} copied; {ship.GetComponentsInChildren<Renderer>().Count(r => r.enabled)} renderers");
            return ship;
        }

        /// <summary>GUID -> path (relative to the export's Assets) of every file with a .meta in the export.</summary>
        private static Dictionary<string, string> index;

        private static Dictionary<string, string> Index()
        {
            var index = new Dictionary<string, string>();
            foreach (string meta in Directory.EnumerateFiles(Root, "*.meta", SearchOption.AllDirectories))
            {
                foreach (string line in File.ReadLines(meta).Take(3))
                {
                    if (!line.StartsWith("guid: "))
                        continue;
                    index[line.Substring(6).Trim()] = meta.Substring(Root.Length + 1, meta.Length - Root.Length - 6).Replace('\\', '/');
                    break;
                }
            }
            return index;
        }

        private static void Collect(string file, Dictionary<string, string> index, HashSet<string> files, int depth)
        {
            foreach (Match match in Guid.Matches(File.ReadAllText(Path.Combine(Root, file))))
            {
                if (!index.TryGetValue(match.Groups[1].Value, out string path) || !Kinds.Contains(Path.GetExtension(path).ToLowerInvariant()))
                    continue;
                if (files.Add(path) && path.EndsWith(".mat") && depth < 2)
                    Collect(path, index, files, depth + 1);
            }
        }

        /// <summary>Copies a file and its .meta (keeping the game's GUID) unless the project already has that GUID.</summary>
        private static bool Copy(string file)
        {
            string source = Path.Combine(Root, file), target = Target + "/" + file;
            string guid = File.ReadLines(source + ".meta").First(l => l.StartsWith("guid: ")).Substring(6).Trim();
            string existing = AssetDatabase.GUIDToAssetPath(guid);
            if (!string.IsNullOrEmpty(existing) && File.Exists(existing))
                return false;
            Directory.CreateDirectory(Path.GetDirectoryName(target));
            File.Copy(source, target, true);
            File.Copy(source + ".meta", target + ".meta", true);
            return true;
        }

        private static void Strip(GameObject ship)
        {
            foreach (var t in ship.GetComponentsInChildren<Transform>(true))
                GameObjectUtility.RemoveMonoBehavioursWithMissingScript(t.gameObject);
            foreach (var group in ship.GetComponentsInChildren<LODGroup>(true))
            {
                LOD[] lods = group.GetLODs();
                var near = new HashSet<Renderer>(lods.Length > 0 ? lods[0].renderers.Where(r => r != null) : Enumerable.Empty<Renderer>());
                foreach (var r in lods.Skip(1).SelectMany(l => l.renderers).Where(r => r != null && !near.Contains(r)))
                    r.enabled = false;
                UnityEngine.Object.DestroyImmediate(group);
            }
            foreach (var c in ship.GetComponentsInChildren<Component>(true).Where(Unwanted).ToArray())
                UnityEngine.Object.DestroyImmediate(c);
            foreach (var r in ship.GetComponentsInChildren<Renderer>(true))
                if (Hidden.Any(h => r.name.ToLowerInvariant().Contains(h) || r.sharedMaterials.Any(m => m != null && m.name.ToLowerInvariant().Contains(h))))
                    r.enabled = false;
            foreach (var skin in ship.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                skin.forceMatrixRecalculationPerRender = true;
        }

        private static bool Unwanted(Component c) =>
            c is Collider || c is Rigidbody || c is ParticleSystem || c is ParticleSystemRenderer || c is AudioSource
            || c is Light || c is Cloth || c is TrailRenderer || c is LineRenderer || c is Joint;

        private static void Dress(GameObject ship)
        {
            var made = new Dictionary<Material, Material>();
            foreach (var r in ship.GetComponentsInChildren<Renderer>(true))
                r.sharedMaterials = r.sharedMaterials.Select(m => m == null ? null : made.TryGetValue(m, out var d) ? d : made[m] = Standard(m)).ToArray();
        }

        private static Material Standard(Material game)
        {
            var material = new Material(Shader.Find("Standard")) { name = game.name + "_preview", mainTexture = MainTexture(game) };
            material.SetFloat("_Glossiness", 0.08f);
            material.SetFloat("_Mode", 1f);
            material.SetFloat("_Cutoff", 0.35f);
            material.EnableKeyword("_ALPHATEST_ON");
            material.renderQueue = 2450;
            return material;
        }

        /// <summary>The game material's _MainTex, read from its saved properties (its shader is not in this project).</summary>
        private static Texture MainTexture(Material material)
        {
            var textures = new SerializedObject(material).FindProperty("m_SavedProperties.m_TexEnvs");
            for (int i = 0; textures != null && i < textures.arraySize; i++)
            {
                var entry = textures.GetArrayElementAtIndex(i);
                if (entry.FindPropertyRelative("first").stringValue == "_MainTex")
                    return entry.FindPropertyRelative("second.m_Texture").objectReferenceValue as Texture;
            }
            return null;
        }

        /// <summary>The height of the deck floor above the ship's origin, by a ray down onto the hull (temporary colliders).</summary>
        public static float DeckHeight(GameObject ship, Vector3 localPoint) => Heights(ship, new[] { localPoint }).Max();

        /// <summary>The top of the rail: the highest hull point across the starboard half, midships.</summary>
        public static float RailHeight(GameObject ship, float halfWidth) =>
            Heights(ship, Enumerable.Range(0, 24).Select(i => new Vector3(halfWidth * (0.5f + 0.5f * i / 23f), 0f, 0.5f)).ToArray()).Max();

        private static float[] Heights(GameObject ship, Vector3[] localPoints)
        {
            var added = new List<MeshCollider>();
            foreach (var filter in ship.GetComponentsInChildren<MeshFilter>())
                if (filter.TryGetComponent<Renderer>(out var r) && r.enabled && filter.sharedMesh != null && r.name == "hull")
                    added.Add(filter.gameObject.AddComponent<MeshCollider>());
            Physics.SyncTransforms();
            float[] heights = localPoints.Select(p =>
            {
                Vector3 from = ship.transform.TransformPoint(p + Vector3.up * 20f);
                return Physics.Raycast(from, Vector3.down, out RaycastHit hit, 40f) ? ship.transform.InverseTransformPoint(hit.point).y : float.MinValue;
            }).ToArray();
            foreach (var c in added)
                UnityEngine.Object.DestroyImmediate(c);
            return heights;
        }
    }
}
