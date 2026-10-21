using System;
using System.IO;
using UnityEngine;

namespace Workshop.Slinger
{
    /// <summary>
    /// The slinger's preview set: a mossy forest floor, a low sun through the trees, and the camera on the slinger's
    /// right front, where the draw reads best. Unity axes: the slinger stands at the origin facing +Z.
    /// </summary>
    public static class SlingerStage
    {
        public static void Build()
        {
            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "Floor";
            floor.transform.localScale = new Vector3(4, 1, 4);
            floor.GetComponent<Renderer>().sharedMaterial = Plain("floor", new Color(0.16f, 0.2f, 0.11f));
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.34f, 0.38f, 0.36f);
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.93f, 0.8f);
            sun.intensity = 1.1f;
            sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(40, -30, 0);
            var camera = new GameObject("Camera", typeof(Camera)).GetComponent<Camera>();
            camera.tag = "MainCamera";
            camera.fieldOfView = 30;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.42f, 0.5f, 0.52f);
            Aim(camera, new Vector3(2.6f, 1.5f, 2.9f), new Vector3(0f, 0.95f, 0.1f));
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

        public static void Shoot(string folder, string name, Camera camera, int width = 960, int height = 720)
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
            File.WriteAllBytes(Path.Combine(folder, name + ".png"), image.EncodeToPNG());
        }

        public static string Argument(string name)
        {
            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, name);
            return index >= 0 && index + 1 < args.Length ? args[index + 1] : throw new ArgumentException("missing " + name);
        }
    }
}
