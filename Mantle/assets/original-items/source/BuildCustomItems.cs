// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace MeadWorks.CustomItems
{
    public static class BuildCustomItems
    {
        const string Generated = "Assets/CustomItemsGenerated";
        static Texture2D atlas;
        static float[] atlasMean;
        [Serializable] public sealed class Source { public int schema; public string product, units; public Surface[] materials; public Asset[] assets; }
        [Serializable] public sealed class Surface { public string id, nativePrefab; public float[] color, emission; public float roughness = .85f, metallic; }
        [Serializable] public sealed class Asset { public string id, displayName, kind; public string[] prefabs; public Part[] parts; public View icon; public Anchor[] anchors; public Box collider; }
        [Serializable] public sealed class Part { public string name, material, state; public float[] vertices, normals, uv, position, rotation, scale; public int[] triangles; public bool collidable, dynamic; }
        [Serializable] public sealed class Anchor { public string name; public float[] position; }
        [Serializable] public sealed class View { public float[] position, target; public float orthographicSize; }
        [Serializable] public sealed class Box { public float[] center, size; }

        static Vector3 V(float[] p) => new Vector3(p[0], p[1], p[2]);
        static Color C(float[] p) => new Color(p[0], p[1], p[2], p.Length > 3 ? p[3] : 1f);
        static string Digest(string path) { using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-", ""); }
        static void Require(bool value, string message) { if (!value) throw new InvalidDataException(message); }
        static string Arg(string key)
        {
            var args = Environment.GetCommandLineArgs(); int index = Array.IndexOf(args, key);
            if (index < 0 || index + 1 >= args.Length) throw new ArgumentException("Missing " + key);
            return Path.GetFullPath(args[index + 1]);
        }

        public static void Build()
        {
            int code = 1;
            try
            {
                string input = Arg("-customItemsSource"), output = Arg("-customItemsOut");
                string atlasPath = Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(input)), "textures", "materials-atlas.png");
                string atlasHash = Digest(atlasPath);
                atlas = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                Require(atlas.LoadImage(File.ReadAllBytes(atlasPath), false) && atlas.width >= 256 && atlas.width == atlas.height * 2, "Four-by-two original material atlas required");
                atlasMean = new float[8];
                for (int tile = 0; tile < 8; tile++)
                {
                    float total = 0;
                    for (int y = 0; y < 64; y++) for (int x = 0; x < 64; x++) total += AtlasSample(tile, x, y);
                    atlasMean[tile] = Mathf.Max(.01f, total / 4096f);
                }
                Require(!Directory.Exists("Assets/Reference") && !Directory.Exists("Assets/BundleExtras"), "Exported native assets are excluded");
                string sourceHash = Digest(input);
                var source = JsonUtility.FromJson<Source>(File.ReadAllText(input));
                Require(source.schema == 1 && source.units == "meters" && source.assets.Length > 0, "Unsupported source");
                // Existing generated asset replacement can invalidate live mesh/material references during a rebuild.
                string product = source.product.ToLowerInvariant(), folder = Generated + "/" + product + "/" + Guid.NewGuid().ToString("N");
                Directory.CreateDirectory(folder); Directory.CreateDirectory(output);
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                var materials = new Dictionary<string, Material>();
                foreach (var surface in source.materials)
                {
                    var material = MakeMaterial(surface, folder);
                    Require(!materials.ContainsKey(surface.id), "Duplicate material " + surface.id);
                    materials.Add(surface.id, material);
                }
                var exports = new List<string>(); var ids = new HashSet<string>();
                foreach (var asset in source.assets)
                {
                    Require(ids.Add(asset.id) && asset.id.All(c => char.IsLetterOrDigit(c) || c == '_'), "Invalid or duplicate asset id");
                    GameObject root = new GameObject(asset.id);
                    try
                    {
                        int partIndex = 0; var dynamicRenderers = new HashSet<Renderer>();
                        foreach (var part in asset.parts)
                        {
                            Require(part.vertices.Length >= 9 && part.vertices.Length % 3 == 0 && part.triangles.Length % 3 == 0, asset.id + " mesh layout");
                            Require(part.vertices.All(x => !float.IsNaN(x) && !float.IsInfinity(x)), asset.id + " nonfinite vertices");
                            Require(part.triangles.All(i => i >= 0 && i < part.vertices.Length / 3), asset.id + " mesh indices");
                            var child = new GameObject(part.name); child.transform.SetParent(root.transform, false);
                            if (part.position != null) child.transform.localPosition = V(part.position);
                            if (part.rotation != null) child.transform.localRotation = Quaternion.Euler(V(part.rotation));
                            if (part.scale != null) child.transform.localScale = V(part.scale);
                            var mesh = new Mesh { name = asset.id + "_" + (partIndex++).ToString("D3") + "_" + part.name };
                            if (part.triangles.Length > 65535) mesh.indexFormat = IndexFormat.UInt32;
                            var inputVertices = Enumerable.Range(0, part.vertices.Length / 3).Select(i => new Vector3(part.vertices[i * 3], part.vertices[i * 3 + 1], part.vertices[i * 3 + 2])).ToArray();
                            mesh.vertices = part.triangles.Select(i => inputVertices[i]).ToArray();
                            mesh.triangles = Enumerable.Range(0, part.triangles.Length).ToArray();
                            Require(part.uv == null || part.uv.Length == inputVertices.Length * 2, asset.id + " source vertex UV layout");
                            if (part.uv != null)
                                mesh.uv = part.triangles.Select(i => new Vector2(part.uv[i * 2], part.uv[i * 2 + 1])).ToArray();
                            else mesh.uv = mesh.vertices.Select(p => new Vector2(p.x * 3f, (p.y + p.z) * 3f)).ToArray();
                            mesh.RecalculateNormals(); mesh.RecalculateBounds();
                            string meshPath = folder + "/" + mesh.name + ".asset";
                            AssetDatabase.CreateAsset(mesh, meshPath);
                            child.AddComponent<MeshFilter>().sharedMesh = mesh;
                            var renderer = child.AddComponent<MeshRenderer>(); renderer.sharedMaterial = materials[part.material];
                            if (part.dynamic) dynamicRenderers.Add(renderer);
                            if (part.collidable) child.AddComponent<MeshCollider>().sharedMesh = mesh;
                            if (part.state == "filled") child.SetActive(false);
                        }
                        foreach (var group in root.GetComponentsInChildren<MeshRenderer>(true).Where(r => r.gameObject.activeSelf && !dynamicRenderers.Contains(r)).GroupBy(r => r.sharedMaterial).ToArray())
                        {
                            var renderers = group.ToArray(); if (renderers.Length < 2) continue;
                            var combined = new Mesh { name = asset.id + "_combined_" + group.Key.name };
                            if (renderers.Sum(r => r.GetComponent<MeshFilter>().sharedMesh.vertexCount) > 65535) combined.indexFormat = IndexFormat.UInt32;
                            combined.CombineMeshes(renderers.Select(r => new CombineInstance { mesh = r.GetComponent<MeshFilter>().sharedMesh, transform = root.transform.worldToLocalMatrix * r.transform.localToWorldMatrix }).ToArray(), true, true);
                            AssetDatabase.CreateAsset(combined, folder + "/" + combined.name + ".asset");
                            var node = new GameObject("surface_" + group.Key.name); node.transform.SetParent(root.transform, false);
                            node.AddComponent<MeshFilter>().sharedMesh = combined; node.AddComponent<MeshRenderer>().sharedMaterial = group.Key;
                            foreach (var renderer in renderers)
                            {
                                if (renderer.GetComponent<Collider>() == null) Object.DestroyImmediate(renderer.GetComponent<MeshFilter>());
                                Object.DestroyImmediate(renderer);
                            }
                        }
                        if (!asset.parts.Any(p => p.collidable) && asset.collider != null && asset.collider.size != null)
                        {
                            var collider = root.AddComponent<BoxCollider>(); collider.center = V(asset.collider.center); collider.size = V(asset.collider.size);
                        }
                        if (asset.anchors != null) foreach (var anchor in asset.anchors)
                        {
                            var child = new GameObject(anchor.name); child.transform.SetParent(root.transform, false); child.transform.localPosition = V(anchor.position);
                        }
                        string prefab = folder + "/" + asset.id + ".prefab";
                        PrefabUtility.SaveAsPrefabAsset(root, prefab); exports.Add(prefab);
                        string icon = folder + "/" + asset.id + "_icon.png";
                        Render(root, asset.icon, icon, 256);
                        AssetDatabase.ImportAsset(icon, ImportAssetOptions.ForceSynchronousImport);
                        var importer = (TextureImporter)AssetImporter.GetAtPath(icon);
                        importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single;
                        importer.alphaIsTransparency = true; importer.mipmapEnabled = false; importer.textureCompression = TextureImporterCompression.Uncompressed;
                        importer.filterMode = FilterMode.Bilinear; importer.SaveAndReimport();
                        exports.Add(icon); File.Copy(icon, Path.Combine(output, asset.id + "_icon.png"), true);
                        File.WriteAllText(Path.Combine(output, asset.id + ".source.json"), JsonUtility.ToJson(asset, true));
                    }
                    finally { Object.DestroyImmediate(root); }
                }
                AssetDatabase.SaveAssets();
                string bundle = "mw_" + product + "_items";
                foreach (var platform in new[] { "windows", "linux" })
                {
                    var target = platform == "windows" ? BuildTarget.StandaloneWindows64 : BuildTarget.StandaloneLinux64;
                    Require(BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone, target), "Missing " + platform + " build support");
                    string targetFolder = Path.Combine(output, platform); Directory.CreateDirectory(targetFolder);
                    var build = new AssetBundleBuild { assetBundleName = bundle, assetNames = exports.ToArray() };
                    Require(BuildPipeline.BuildAssetBundles(targetFolder, new[] { build }, BuildAssetBundleOptions.ChunkBasedCompression | BuildAssetBundleOptions.StrictMode, target) != null, "Bundle build failed");
                    File.Copy(Path.Combine(targetFolder, bundle), Path.Combine(output, bundle + "." + platform), true);
                }
                Require(Digest(input) == sourceHash, "Source changed during asset build");
                Require(Digest(atlasPath) == atlasHash, "Original texture atlas changed during build");
                File.WriteAllText(Path.Combine(output, "build-result.json"), "{\"product\":\"" + source.product + "\",\"assets\":" + source.assets.Length + ",\"modelRenderedIcons\":true,\"platforms\":[\"windows\",\"linux\"],\"sourceSha256\":\"" + sourceHash + "\",\"atlasSha256\":\"" + atlasHash + "\",\"builderSha256\":\"" + Digest("Assets/Editor/CustomItems/BuildCustomItems.cs") + "\"}");
                code = 0;
            }
            catch (Exception error) { Debug.LogException(error); }
            finally { if (atlas != null) Object.DestroyImmediate(atlas); atlas = null; EditorApplication.Exit(code); }
        }

        static float AtlasSample(int tile, int x, int y) => atlas.GetPixelBilinear((tile % 4 + .02f + .96f * (x + .5f) / 64f) / 4f, 1f - (tile / 4 + .02f + .96f * (y + .5f) / 64f) / 2f).grayscale;
        static int Tile(string name)
        {
            string id = name.ToLowerInvariant();
            if (id.Contains("fur")) return 2;
            if (id.Contains("wood") || id.Contains("root") || id.Contains("cork")) return 0;
            if (id.Contains("leather") || id.Contains("hide") || id.Contains("strap") || id.Contains("carapace")) return 1;
            if (id.Contains("bone") || id.Contains("ivory") || id.Contains("beak")) return 3;
            if (id.Contains("iron") || id.Contains("metal") || id.Contains("silver") || id.Contains("bronze") || id.Contains("copper") || id.Contains("gold") || id.Contains("coin")) return 4;
            if (id.Contains("stone") || id.Contains("obsidian")) return 5;
            if (id.Contains("paper") || id.Contains("parchment")) return 6;
            if (id.Contains("linen") || id.Contains("jute") || id.Contains("cloth") || id.Contains("rope") || id.Contains("thread")) return 7;
            return -1;
        }

        static Material MakeMaterial(Surface surface, string folder)
        {
            var shader = Shader.Find("Standard"); Require(shader != null, "Standard shader unavailable");
            var material = new Material(shader) { name = surface.id };
            material.SetFloat("_Glossiness", Mathf.Clamp01(1f - surface.roughness)); material.SetFloat("_Metallic", surface.metallic);
            Color color = C(surface.color);
            var texture = new Texture2D(64, 64, TextureFormat.RGBA32, true) { name = surface.id + "_albedo", wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Point };
            var pixels = new Color[4096]; uint seed = 2166136261;
            int tile = Tile(surface.id);
            foreach (char letter in surface.id) seed = (seed ^ letter) * 16777619;
            for (int y = 0; y < 64; y++) for (int x = 0; x < 64; x++)
            {
                seed ^= seed << 13; seed ^= seed >> 17; seed ^= seed << 5;
                float grain = .88f + (seed & 255) / 255f * .17f + Mathf.Sin(x * .42f + Mathf.Sin(y * .16f)) * .025f;
                if (tile >= 0) grain = .55f + .45f * Mathf.Clamp(AtlasSample(tile, x, y) / atlasMean[tile], .35f, 1.5f);
                pixels[y * 64 + x] = new Color(color.r * grain, color.g * grain, color.b * grain, color.a);
            }
            texture.SetPixels(pixels); texture.Apply();
            AssetDatabase.CreateAsset(texture, folder + "/" + surface.id + "_albedo.asset"); material.mainTexture = texture; material.color = Color.white;
            if (surface.emission != null && surface.emission.Take(3).Any(c => c > 0))
            { material.EnableKeyword("_EMISSION"); material.SetColor("_EmissionColor", C(surface.emission)); }
            AssetDatabase.CreateAsset(material, folder + "/" + surface.id + ".mat");
            return material;
        }

        static void Render(GameObject root, View view, string path, int size)
        {
            Require(SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null, "Icons require the graphics-enabled editor; omit -nographics");
            var cameraObject = new GameObject("CustomItemsIconCamera"); var lightObject = new GameObject("CustomItemsIconKey");
            var target = new RenderTexture(size * 2, size * 2, 24, RenderTextureFormat.ARGB32);
            RenderTexture prior = RenderTexture.active;
            Color priorAmbient = RenderSettings.ambientLight; var priorMode = RenderSettings.ambientMode;
            var sceneLights = Object.FindObjectsByType<Light>(FindObjectsSortMode.None).Where(light => light.enabled).ToArray();
            try
            {
                foreach (var light in sceneLights) light.enabled = false;
                foreach (var child in root.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = 31;
                var camera = cameraObject.AddComponent<Camera>(); camera.orthographic = true;
                camera.transform.position = root.transform.position + V(view.position); camera.transform.LookAt(root.transform.position + V(view.target));
                camera.orthographicSize = view.orthographicSize; camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0, 0, 0, 0); camera.nearClipPlane = .001f; camera.farClipPlane = 30;
                camera.targetTexture = target; camera.allowHDR = false; camera.allowMSAA = false;
                camera.cullingMask = 1 << 31;
                var key = lightObject.AddComponent<Light>(); key.type = LightType.Directional; key.intensity = 1.15f;
                key.cullingMask = 1 << 31;
                lightObject.transform.rotation = camera.transform.rotation * Quaternion.Euler(25, -35, 0);
                RenderSettings.ambientMode = AmbientMode.Flat; RenderSettings.ambientLight = new Color(.54f, .55f, .58f);
                camera.Render(); RenderTexture.active = target;
                var large = new Texture2D(size * 2, size * 2, TextureFormat.RGBA32, false);
                large.ReadPixels(new Rect(0, 0, size * 2, size * 2), 0, 0); large.Apply();
                var small = new Texture2D(size, size, TextureFormat.RGBA32, false);
                for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
                    small.SetPixel(x, y, (large.GetPixel(x * 2, y * 2) + large.GetPixel(x * 2 + 1, y * 2) + large.GetPixel(x * 2, y * 2 + 1) + large.GetPixel(x * 2 + 1, y * 2 + 1)) / 4f);
                small.Apply(); File.WriteAllBytes(path, small.EncodeToPNG()); Object.DestroyImmediate(large); Object.DestroyImmediate(small);
            }
            finally
            {
                RenderTexture.active = prior; RenderSettings.ambientLight = priorAmbient; RenderSettings.ambientMode = priorMode;
                foreach (var light in sceneLights) if (light != null) light.enabled = true;
                root.transform.position = Vector3.zero; Object.DestroyImmediate(cameraObject); Object.DestroyImmediate(lightObject); target.Release(); Object.DestroyImmediate(target);
            }
        }
    }
}
