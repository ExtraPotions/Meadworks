using UnityEngine;
using UnityEngine.Rendering;
using Workshop.Slinger;

namespace Workshop.Kraken
{
    /// <summary>
    /// The kraken's preview set: an overcast northern sea - pale grey-blue sky and fog, a low warm sun, cool ambient
    /// light - and optionally a flat water plane at y = 0, a little see-through so what is under it shows dimly.
    /// </summary>
    public static class KrakenStage
    {
        public static readonly Color Sky = new Color(0.56f, 0.63f, 0.68f);

        public static Camera Build()
        {
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.52f, 0.58f, 0.65f);
            RenderSettings.ambientEquatorColor = new Color(0.40f, 0.43f, 0.46f);
            RenderSettings.ambientGroundColor = new Color(0.22f, 0.25f, 0.27f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = Sky;
            RenderSettings.fogStartDistance = 45f;
            RenderSettings.fogEndDistance = 240f;
            QualitySettings.shadowDistance = 90f;
            QualitySettings.shadowResolution = ShadowResolution.VeryHigh;
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.92f, 0.8f);
            sun.intensity = 1.15f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.8f;
            sun.transform.rotation = Quaternion.Euler(36f, -38f, 0f);
            var camera = new GameObject("Camera", typeof(Camera)).GetComponent<Camera>();
            camera.tag = "MainCamera";
            camera.fieldOfView = 35f;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 800f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Sky;
            return camera;
        }

        public static GameObject Water()
        {
            var water = GameObject.CreatePrimitive(PrimitiveType.Plane);
            water.name = "Water";
            Object.DestroyImmediate(water.GetComponent<Collider>());
            water.transform.localScale = new Vector3(60f, 1f, 60f);
            var material = new Material(Shader.Find("Standard")) { name = "water", color = new Color(0.07f, 0.17f, 0.2f, 0.84f) };
            material.SetFloat("_Mode", 3f);
            material.SetFloat("_Glossiness", 0.88f);
            material.SetFloat("_Metallic", 0.05f);
            material.SetInt("_SrcBlend", (int)BlendMode.One);
            material.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            material.SetInt("_ZWrite", 0);
            material.EnableKeyword("_ALPHAPREMULTIPLY_ON");
            material.renderQueue = 3000;
            water.GetComponent<Renderer>().sharedMaterial = material;
            return water;
        }

        /// <summary>A plain 1.8 m stand-in for a player, standing at `at` and facing `towards`.</summary>
        public static GameObject Player(Vector3 at, Vector3 towards)
        {
            var root = new GameObject("Player");
            root.transform.position = at;
            Vector3 look = towards - at;
            look.y = 0;
            root.transform.rotation = Quaternion.LookRotation(look);
            var cloth = SlingerStage.Plain("player", new Color(0.33f, 0.25f, 0.17f));
            Part(PrimitiveType.Capsule, root.transform, new Vector3(0, 0.75f, 0), new Vector3(0.45f, 0.75f, 0.45f), cloth);
            Part(PrimitiveType.Sphere, root.transform, new Vector3(0, 1.62f, 0), Vector3.one * 0.3f, cloth);
            Part(PrimitiveType.Cube, root.transform, new Vector3(0.28f, 0.95f, 0.3f), new Vector3(0.06f, 0.06f, 0.9f),
                 SlingerStage.Plain("spear", new Color(0.45f, 0.4f, 0.35f)));
            return root;
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
