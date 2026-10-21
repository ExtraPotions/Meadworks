using System;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;

namespace Workshop.GameRig
{
    /// <summary>
    /// The preview set: a mossy floor with a 1 m grid of darker lines (so feet can be seen to stand on it and sliding
    /// shows), a low sun from the front left, flat sky light, and a camera. Unity axes: the creatures face +Z.
    /// </summary>
    public static class GameRigStage
    {
        public static Camera Build()
        {
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.4f, 0.43f, 0.42f);
            Floor();
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.94f, 0.82f);
            sun.intensity = 1.15f;
            sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(42, 150, 0);
            var camera = new GameObject("Camera", typeof(Camera)).GetComponent<Camera>();
            camera.fieldOfView = 30;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.45f, 0.53f, 0.56f);
            return camera;
        }

        public static void Aim(Camera camera, Vector3 position, Vector3 target)
        {
            camera.transform.position = position;
            camera.transform.LookAt(target);
        }

        public static Material Plain(string name, Color colour)
        {
            var material = new Material(Shader.Find("Standard")) { name = name, color = colour };
            material.SetFloat("_Glossiness", 0.05f);
            return material;
        }

        public static void Shoot(string file, Camera camera, int width, int height)
        {
            var target = new RenderTexture(width, height, 24) { antiAliasing = 4 };
            camera.targetTexture = target;
            camera.Render();
            RenderTexture.active = target;
            var image = new Texture2D(width, height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            image.Apply();
            camera.targetTexture = null;
            RenderTexture.active = null;
            UnityEngine.Object.DestroyImmediate(target);
            File.WriteAllBytes(file, image.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(image);
        }

        public static string Argument(string name, string fallback = null)
        {
            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, name);
            if (index >= 0 && index + 1 < args.Length)
                return args[index + 1];
            return fallback ?? throw new ArgumentException("missing " + name);
        }

        private static void Floor()
        {
            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "Floor";
            floor.transform.localScale = new Vector3(3, 1, 3);
            floor.GetComponent<Renderer>().sharedMaterial = Plain("floor", new Color(0.2f, 0.24f, 0.14f));
            var line = Plain("grid", new Color(0.13f, 0.16f, 0.09f));
            for (int i = -6; i <= 6; i++)
            {
                Line(new Vector3(i, 0.002f, 0f), new Vector3(0.012f, 0.001f, 14f), line);
                Line(new Vector3(0f, 0.002f, i), new Vector3(14f, 0.001f, 0.012f), line);
            }
        }

        private static void Line(Vector3 position, Vector3 size, Material material)
        {
            var bar = GameObject.CreatePrimitive(PrimitiveType.Cube);
            UnityEngine.Object.DestroyImmediate(bar.GetComponent<Collider>());
            bar.transform.position = position;
            bar.transform.localScale = size;
            bar.GetComponent<Renderer>().sharedMaterial = material;
        }
    }
}
