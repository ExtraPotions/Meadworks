using System.Linq;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Workshop.Vfx
{
    /// <summary>
    /// The preview set: an empty scene with dark earth underfoot, a 1.8 m stand-in player beside the effect for scale,
    /// and a camera looking at the effect from the spec's yaw, pitch and distance, rendering linear HDR with depth (for
    /// soft particles). Everything here uses the preview shaders; nothing is saved.
    /// </summary>
    public static class VfxStage
    {
        public static readonly Color Earth = new Color(0.26f, 0.23f, 0.19f);
        public static readonly Color Cloth = new Color(0.36f, 0.4f, 0.48f);
        public static readonly Color Sky = new Color(0.012f, 0.016f, 0.024f);

        public static Camera Build(PreviewSpec p)
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Part(PrimitiveType.Plane, null, new Vector3(0, 0, 0), new Vector3(6, 1, 6), Earth, 30f);
            if (p.figure)
                Figure(p.figure_offset.Length == 3 ? VfxCurves.Vector(p.figure_offset)
                    : new Vector3(p.references.Length > 0 ? p.reference_offset[0] * 0.5f : 1.3f, 0, 0.9f));
            return MakeCamera(p);
        }

        private static void Figure(Vector3 at)
        {
            var root = new GameObject("Figure 1.8 m").transform;
            root.position = at;
            Part(PrimitiveType.Capsule, root, new Vector3(0, 0.75f, 0), new Vector3(0.44f, 0.75f, 0.3f), Cloth, 1f);
            Part(PrimitiveType.Sphere, root, new Vector3(0, 1.62f, 0), Vector3.one * 0.3f, Cloth, 1f);
        }

        private static void Part(PrimitiveType type, Transform parent, Vector3 position, Vector3 scale, Color colour, float tiling)
        {
            var part = GameObject.CreatePrimitive(type);
            if (parent != null)
                Object.DestroyImmediate(part.GetComponent<Collider>());   // the ground keeps its collider for colliding particles
            part.transform.SetParent(parent, false);
            part.transform.localPosition = position;
            part.transform.localScale = scale;
            var filter = part.GetComponent<MeshFilter>();
            Mesh mesh = Object.Instantiate(filter.sharedMesh);
            mesh.colors = Enumerable.Repeat(Color.white, mesh.vertexCount).ToArray();
            filter.sharedMesh = mesh;
            var material = new Material(Shader.Find("Workshop/Vfx/Opaque")) { color = colour };
            material.SetFloat("_VfxTiling", tiling);
            material.SetFloat("_VfxSRGB", 1);
            part.GetComponent<MeshRenderer>().sharedMaterial = material;
        }

        private static Camera MakeCamera(PreviewSpec p)
        {
            var camera = new GameObject("Camera").AddComponent<Camera>();
            Vector3 target = new Vector3(p.reference_offset[0] * 0.5f * (p.references.Length > 0 ? 1 : 0), p.target_height, 0);
            camera.transform.position = target + Quaternion.Euler(p.pitch, p.yaw, 0f) * Vector3.back * p.distance;
            camera.transform.LookAt(target);
            camera.fieldOfView = 40f;
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 500f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Sky;
            camera.allowHDR = true;
            camera.allowMSAA = false;
            camera.depthTextureMode = DepthTextureMode.Depth;
            camera.renderingPath = RenderingPath.Forward;
            return camera;
        }
    }
}
