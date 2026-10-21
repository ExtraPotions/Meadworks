// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MeadWorks.RaidChest
{
    public static class BuildRaidChest
    {
        const string Generated = "Assets/RaidChestGenerated";
        [Serializable] sealed class Source
        {
            public int schema, textureSize;
            public string asset;
            public string[] palette;
            public Node[] nodes;
            public MeshData[] meshes;
            public Box[] boxes;
            public float[] boundsMin, boundsMax;
        }
        [Serializable] sealed class Node { public string name, parent, mesh; public float[] position, rotation; }
        [Serializable] sealed class MeshData { public string name; public float[] vertices, normals, uv; public int[] triangles; }
        [Serializable] sealed class Box { public string name; public float[] center, size; }

        static Vector3 V(float[] value) => new Vector3(value[0], value[1], value[2]);
        static void Require(bool condition, string text) { if (!condition) throw new InvalidOperationException(text); }
        static string Argument(string key)
        {
            var args = Environment.GetCommandLineArgs();int at = Array.IndexOf(args, key);
            if (at < 0 || at + 1 >= args.Length) throw new ArgumentException("Missing " + key);
            return args[at + 1];
        }

        public static void Build()
        {
            int code = 1;GameObject root = null;
            try
            {
                Require(!Directory.Exists("Assets/Reference") && !Directory.Exists("Assets/BundleExtras"), "No exported game data permitted");
                string sourcePath = Path.GetFullPath(Argument("-raidChestSource"));
                string iconPath = Path.GetFullPath(Argument("-raidChestIcon"));
                string output = Path.GetFullPath(Argument("-raidChestOut"));
                var source = JsonUtility.FromJson<Source>(File.ReadAllText(sourcePath));
                Require(source.schema == 1 && source.asset == "mw_raid_chest_model", "Not the one-chest schema");
                Require(source.meshes.Length == 3 && source.nodes.Length == 5 && source.boxes.Length == 3, "Chest parts/colliders differ");
                Require(source.textureSize == 256 && source.palette.Length == 4, "Chest atlas contract");
                Directory.CreateDirectory(Generated);Directory.CreateDirectory(output);
                File.Delete(Path.Combine(output, "raid-chest.report.txt"));
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                var material = Material(source);
                var meshes = new Dictionary<string, Mesh>();
                foreach (var data in source.meshes)
                {
                    Require(new[]{"body_mesh","lid_mesh","horn_mesh"}.Contains(data.name), "Unknown chest mesh name");
                    Require(data.vertices.Length % 3 == 0 && data.normals.Length == data.vertices.Length && data.uv.Length == data.vertices.Length / 3 * 2, "Mesh arrays");
                    Require(data.vertices.All(Finite) && data.normals.All(Finite) && data.uv.All(Finite), "Nonfinite mesh");
                    Require(data.triangles.Length % 3 == 0 && data.triangles.All(i => i >= 0 && i < data.vertices.Length / 3), "Triangle indices");
                    var mesh = new Mesh { name = data.name };
                    mesh.vertices = Enumerable.Range(0, data.vertices.Length / 3).Select(i => new Vector3(data.vertices[i*3], data.vertices[i*3+1], data.vertices[i*3+2])).ToArray();
                    mesh.normals = Enumerable.Range(0, data.normals.Length / 3).Select(i => new Vector3(data.normals[i*3], data.normals[i*3+1], data.normals[i*3+2])).ToArray();
                    mesh.uv = Enumerable.Range(0, data.uv.Length / 2).Select(i => new Vector2(data.uv[i*2], data.uv[i*2+1])).ToArray();
                    mesh.triangles = data.triangles;mesh.RecalculateBounds();
                    Save(mesh, Generated + "/" + data.name + ".asset");meshes.Add(data.name, mesh);
                }
                Require(source.meshes.Sum(mesh => mesh.triangles.Length / 3) < 8000, "Triangle budget");
                root = new GameObject(source.asset);
                var nodes = new Dictionary<string, Transform>();
                foreach (var node in source.nodes)
                {
                    Require(node.position.Length == 3 && node.rotation.Length == 3 && node.position.All(Finite) && node.rotation.All(Finite), "Node transform");
                    var part = new GameObject(node.name).transform;
                    part.SetParent(string.IsNullOrEmpty(node.parent) ? root.transform : nodes[node.parent], false);
                    part.localPosition = V(node.position);part.localRotation = Quaternion.Euler(V(node.rotation));nodes.Add(node.name, part);
                    if (!string.IsNullOrEmpty(node.mesh))
                    {
                        part.gameObject.AddComponent<MeshFilter>().sharedMesh = meshes[node.mesh];
                        part.gameObject.AddComponent<MeshRenderer>().sharedMaterial = material;
                    }
                }
                foreach (var box in source.boxes)
                {
                    Require(box.center.Length == 3 && box.size.Length == 3 && box.center.All(Finite) && box.size.All(x => Finite(x) && x > 0), "Root box dimensions");
                    var collider = root.AddComponent<BoxCollider>();collider.center = V(box.center);collider.size = V(box.size);
                }
                Validate(root, nodes, source);
                string prefab = Generated + "/" + source.asset + ".prefab";
                PrefabUtility.SaveAsPrefabAsset(root, prefab);
                string icon = Icon(iconPath);
                AssetDatabase.SaveAssets();
                string[] assets = { prefab, icon };
                string[] dependencies = AssetDatabase.GetDependencies(assets, true);
                Require(dependencies.All(path => path.StartsWith(Generated + "/", StringComparison.Ordinal) || path == "Resources/unity_builtin_extra" || path == "Library/unity default resources"), "Unexpected asset dependency: " + string.Join(", ", dependencies.Where(path => !path.StartsWith(Generated + "/", StringComparison.Ordinal))));
                var report = new List<string> { "asset " + source.asset, "triangles " + source.meshes.Sum(m => m.triangles.Length / 3), "root boxes 3", "lid/socket/opening validation PASS" };
                report.AddRange(dependencies.Select(path => "dependency " + path));
                foreach (string platform in Argument("-raidChestPlatforms").Split(','))
                {
                    BuildTarget target = platform == "windows" ? BuildTarget.StandaloneWindows64 : platform == "linux" ? BuildTarget.StandaloneLinux64 : throw new ArgumentException("Unknown platform " + platform);
                    Require(BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone, target), "Build support missing: " + platform);
                    string folder = Path.Combine(output, platform);Directory.CreateDirectory(folder);
                    var build = new AssetBundleBuild { assetBundleName = "mw_raid_chest", assetNames = assets };
                    var result = BuildPipeline.BuildAssetBundles(folder, new[] { build }, BuildAssetBundleOptions.ChunkBasedCompression | BuildAssetBundleOptions.StrictMode, target);
                    Require(result != null && File.Exists(Path.Combine(folder, "mw_raid_chest")), "Bundle build failed: " + platform);
                    File.Copy(Path.Combine(folder, "mw_raid_chest"), Path.Combine(output, platform + ".bundle"), true);report.Add("built " + platform);
                }
                File.WriteAllLines(Path.Combine(output, "raid-chest.report.txt"), report);code = 0;
            }
            catch (Exception error) { Debug.LogException(error); }
            finally { if (root != null) Object.DestroyImmediate(root);EditorApplication.Exit(code); }
        }

        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        static void Save(Object value, string path) { AssetDatabase.DeleteAsset(path);AssetDatabase.CreateAsset(value, path); }
        static Material Material(Source source)
        {
            var image = new Texture2D(256,256,TextureFormat.RGBA32,false);
            var pixels = new Color32[256*256];
            for (int y=0;y<256;y++) for (int x=0;x<256;x++)
            {
                Require(ColorUtility.TryParseHtmlString(source.palette[x/64],out Color color), "Palette color");
                int variation = x/64 == 0 ? (int)(((uint)(x*17+y*11) ^ (uint)(y*97))%13)-6 : 0;
                var c=(Color32)color;pixels[y*256+x]=new Color32((byte)Mathf.Clamp(c.r+variation,0,255),(byte)Mathf.Clamp(c.g+variation,0,255),(byte)Mathf.Clamp(c.b+variation,0,255),255);
            }
            image.SetPixels32(pixels);image.Apply();string path=Generated+"/raid-chest-albedo.png";File.WriteAllBytes(path,image.EncodeToPNG());Object.DestroyImmediate(image);
            AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
            var importer=(TextureImporter)AssetImporter.GetAtPath(path);importer.mipmapEnabled=true;importer.wrapMode=TextureWrapMode.Clamp;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.SaveAndReimport();
            var material=new Material(Shader.Find("Standard")){name="mw_raid_chest_atlas"};material.mainTexture=AssetDatabase.LoadAssetAtPath<Texture2D>(path);material.SetFloat("_Glossiness",.15f);Save(material,Generated+"/raid-chest.mat");return material;
        }

        static string Icon(string source)
        {
            XDocument document;using(var reader=XmlReader.Create(source,new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null}))document=XDocument.Load(reader);XNamespace ns="http://www.w3.org/2000/svg";
            Require(document.Root.Name==ns+"svg" && (string)document.Root.Attribute("viewBox")=="0 0 256 256", "Icon canvas");
            Require(document.Descendants().All(node=>node.Name==ns+"svg"||node.Name==ns+"title"||node.Name==ns+"metadata"||node.Name==ns+"polygon"),"Icon supports original polygons only");
            var pixels=new Color32[256*256];
            foreach(var polygon in document.Descendants(ns+"polygon"))
            {
                Require(ColorUtility.TryParseHtmlString((string)polygon.Attribute("fill"),out Color color),"Icon fill");
                float[] values=((string)polygon.Attribute("points")).Split(new[]{' ',',','\n','\r','\t'},StringSplitOptions.RemoveEmptyEntries).Select(value=>float.Parse(value,CultureInfo.InvariantCulture)).ToArray();
                Require(values.Length>=6&&values.Length%2==0&&values.All(Finite),"Icon polygon");
                for(int y=0;y<256;y++)for(int x=0;x<256;x++)if(Inside(x+.5f,y+.5f,values))pixels[(255-y)*256+x]=(Color32)color;
            }
            var image=new Texture2D(256,256,TextureFormat.RGBA32,false);image.SetPixels32(pixels);image.Apply();
            string path=Generated+"/mw_raid_chest_icon.png";File.WriteAllBytes(path,image.EncodeToPNG());Object.DestroyImmediate(image);
            AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);var importer=(TextureImporter)AssetImporter.GetAtPath(path);importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;importer.alphaIsTransparency=true;importer.mipmapEnabled=false;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.SaveAndReimport();return path;
        }
        static bool Inside(float x,float y,float[] points)
        {
            bool inside=false;int last=points.Length-2;
            for(int at=0;at<points.Length;at+=2){float ax=points[at],ay=points[at+1],bx=points[last],by=points[last+1];if((ay>y)!=(by>y)&&x<(bx-ax)*(y-ay)/(by-ay)+ax)inside=!inside;last=at;}return inside;
        }
        static void Validate(GameObject root,Dictionary<string,Transform> nodes,Source source)
        {
            Require(nodes["lid"].parent==nodes["model"]&&nodes["horn"].parent==nodes["lid"]&&nodes["horn_mouth"].parent==nodes["horn"],"Moving hierarchy");
            Require(nodes["lid"].localRotation==Quaternion.identity,"Lid closed rotation");
            Require(root.GetComponents<BoxCollider>().Length==3&&root.GetComponentsInChildren<Collider>().Length==3,"Three root colliders");
            var all=root.GetComponentsInChildren<MeshRenderer>();Bounds bounds=all[0].bounds;foreach(var renderer in all)bounds.Encapsulate(renderer.bounds);
            Require(Vector3.Distance(bounds.min,V(source.boundsMin))<.0001f&&Vector3.Distance(bounds.max,V(source.boundsMax))<.0001f,"Closed bounds");
            Bounds body=nodes["body"].GetComponent<MeshRenderer>().bounds;var lid=nodes["lid"];
            try{for(int angle=0;angle>=-100;angle-=5){lid.localRotation=Quaternion.Euler(angle,0,0);foreach(string name in new[]{"lid","horn"}){Bounds part=nodes[name].GetComponent<MeshRenderer>().bounds;Vector3 overlap=Vector3.Min(part.max,body.max)-Vector3.Max(part.min,body.min);Require(overlap.x<=.00001f||overlap.y<=.00001f||overlap.z<=.00001f,"Opening intersects body: "+angle);}}}
            finally{lid.localRotation=Quaternion.identity;}
        }
    }
}
