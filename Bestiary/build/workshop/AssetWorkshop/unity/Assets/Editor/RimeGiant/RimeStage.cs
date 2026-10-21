using UnityEngine;
using Workshop.Slinger;

namespace Workshop.RimeGiant
{
    /// <summary>
    /// The Rime Giant's preview set: a snowfield on a mountain ridge under a pale overcast sky, a low cold sun, and a
    /// camera with a narrow lens for a creature this big. Unity axes: the giant stands at the origin facing +Z.
    /// </summary>
    public static class RimeStage
    {
        public static readonly Color Sky = new Color(0.62f, 0.68f, 0.74f);

        public static void Build()
        {
            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "Snow";
            floor.transform.localScale = new Vector3(20, 1, 20);
            floor.GetComponent<Renderer>().sharedMaterial = SlingerStage.Plain("snow", new Color(0.8f, 0.83f, 0.86f));
            Object.DestroyImmediate(floor.GetComponent<Collider>());
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.55f, 0.6f, 0.68f);
            RenderSettings.ambientEquatorColor = new Color(0.45f, 0.48f, 0.52f);
            RenderSettings.ambientGroundColor = new Color(0.4f, 0.42f, 0.45f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = Sky;
            RenderSettings.fogStartDistance = 30f;
            RenderSettings.fogEndDistance = 140f;
            Sun();
            Camera();
        }

        private static void Sun()
        {
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.95f, 0.88f);
            sun.intensity = 1.05f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.75f;
            sun.transform.rotation = Quaternion.Euler(45, -40, 0);
        }

        private static void Camera()
        {
            var camera = new GameObject("Camera", typeof(Camera)).GetComponent<Camera>();
            camera.tag = "MainCamera";
            camera.fieldOfView = 30;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 400f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Sky;
            SlingerStage.Aim(camera, new Vector3(9f, 5f, 12f), new Vector3(0f, 3f, 0f));
        }

        /// <summary>A plain 1.8 m stand-in for a player: body and head, facing the giant.</summary>
        public static GameObject Player(Vector3 at)
        {
            var root = new GameObject("Player");
            root.transform.position = at;
            var cloth = SlingerStage.Plain("player", new Color(0.3f, 0.24f, 0.18f));
            Part(PrimitiveType.Capsule, root.transform, new Vector3(0, 0.75f, 0), new Vector3(0.45f, 0.75f, 0.45f), cloth);
            Part(PrimitiveType.Sphere, root.transform, new Vector3(0, 1.62f, 0), Vector3.one * 0.3f, cloth);
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
