using UnityEditor;
using UnityEngine;

namespace Workshop
{
    /// <summary>The preview set: a rock floor and wall in the game's texture, torchlight and moonlight, the camera, and a
    /// 1.8 m stand-in player with a sword. Unity axes: the mimic sits at the origin facing +Z.</summary>
    public static class PreviewStage
    {
        private const string Rock = "GameElements/Items/_res/stone/rock_256.png";
        private static readonly Color Torch = new Color(1f, 0.62f, 0.32f);

        public static void Build()
        {
            var rock = ReferenceAssets.Texture(Rock, false);
            Block("Floor", PrimitiveType.Plane, new Vector3(0, 0, 4), new Vector3(3, 1, 3), MakeMaterial("floor", rock, 12f, 12f));
            Block("Wall", PrimitiveType.Cube, new Vector3(0, 2.5f, -2.6f), new Vector3(30, 5, 0.4f), MakeMaterial("wall", rock, 12f, 2f));
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.16f, 0.18f, 0.21f);
            MakeLight("Moonlight", LightType.Directional, Vector3.zero, new Color(0.75f, 0.85f, 1f), 0.35f)
                .transform.rotation = Quaternion.Euler(50, 150, 0);
            MakeLight("Torch left", LightType.Point, new Vector3(2.4f, 2.3f, 3.2f), Torch, 1.6f);
            MakeLight("Torch right", LightType.Point, new Vector3(-2.8f, 2.4f, -0.9f), Torch, 1.3f);
            MakeLight("Torch far", LightType.Point, new Vector3(-2.2f, 2.3f, 8.4f), Torch, 1.5f);
            MakeLight("Torch side", LightType.Point, new Vector3(4.5f, 2.4f, 6.5f), Torch, 1.3f);
            MakeCamera();
        }

        /// <summary>Returns the player's root; `sword` is the shoulder pivot the director swings.</summary>
        public static Transform StandIn(out Transform sword)
        {
            var root = new GameObject("Player").transform;
            root.SetPositionAndRotation(new Vector3(0, 0, 4.2f), Quaternion.Euler(0, 180, 0));
            var cloth = MakeMaterial("player", null, 1, 1);
            cloth.color = new Color(0.25f, 0.32f, 0.45f);
            Part(PrimitiveType.Capsule, root, new Vector3(0, 0.725f, 0), new Vector3(0.42f, 0.725f, 0.42f), cloth);
            Part(PrimitiveType.Sphere, root, new Vector3(0, 1.63f, 0), Vector3.one * 0.3f, cloth);
            sword = new GameObject("Sword pivot").transform;
            sword.SetParent(root, false);
            sword.localPosition = new Vector3(0.27f, 1.3f, 0);
            var steel = MakeMaterial("sword", null, 1, 1);
            steel.color = new Color(0.72f, 0.74f, 0.78f);
            steel.SetFloat("_Metallic", 0.9f);
            Part(PrimitiveType.Cube, sword, new Vector3(0, 0.45f, 0), new Vector3(0.05f, 0.95f, 0.02f), steel);
            return root;
        }

        private static void MakeCamera()
        {
            var camera = new GameObject("Camera", typeof(Camera), typeof(AudioListener)).GetComponent<Camera>();
            camera.tag = "MainCamera";
            camera.transform.position = new Vector3(-5.4f, 3.1f, 7.4f);
            camera.transform.LookAt(new Vector3(0.3f, 0.45f, 2.0f));
            camera.fieldOfView = 36;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.05f, 0.075f, 0.09f);
        }

        private static Light MakeLight(string name, LightType type, Vector3 position, Color color, float intensity)
        {
            var light = new GameObject(name).AddComponent<Light>();
            light.type = type;
            light.transform.position = position;
            light.color = color;
            light.intensity = intensity;
            light.range = 12;
            light.shadows = LightShadows.Soft;
            return light;
        }

        private static Material MakeMaterial(string name, Texture2D texture, float tileX, float tileY)
        {
            var material = new Material(Shader.Find("Standard")) { name = name };
            material.SetFloat("_Glossiness", 0.1f);
            if (texture != null)
            {
                material.mainTexture = texture;
                material.mainTextureScale = new Vector2(tileX, tileY);
            }
            AssetDatabase.CreateAsset(material, ReferenceAssets.Folder + "/" + name + "_preview.mat");
            return material;
        }

        private static void Block(string name, PrimitiveType type, Vector3 position, Vector3 scale, Material material)
        {
            var block = GameObject.CreatePrimitive(type);
            block.name = name;
            block.transform.position = position;
            block.transform.localScale = scale;
            block.GetComponent<Renderer>().sharedMaterial = material;
        }

        private static void Part(PrimitiveType type, Transform parent, Vector3 position, Vector3 scale, Material material)
        {
            var part = GameObject.CreatePrimitive(type);
            Object.DestroyImmediate(part.GetComponent<Collider>());
            part.transform.SetParent(parent, false);
            part.transform.localPosition = position;
            part.transform.localScale = scale;
            part.GetComponent<Renderer>().sharedMaterial = material;
        }
    }
}
