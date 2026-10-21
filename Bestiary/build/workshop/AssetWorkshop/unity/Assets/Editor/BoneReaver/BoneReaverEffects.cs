using UnityEditor;
using UnityEngine;

namespace Workshop
{
    /// <summary>Local cosmetic prefabs; gameplay chooses the impact point and dispatches the clip cues.</summary>
    public static class BoneReaverEffects
    {
        public static void Build(string folder)
        {
            Make(folder, "reaver_rock_impact", new Color(.30f,.29f,.25f), 26, .10f, 4f, false);
            Make(folder, "reaver_bone_shatter", new Color(.69f,.63f,.49f), 42, .075f, 5f, false);
            Make(folder, "reaver_blood_drip", new Color(.22f,.015f,.01f), 0, .018f, .04f, true);
            Make(folder, "reaver_axe_materialize", new Color(.40f,.05f,.72f), 35, .035f, .65f, false);
        }

        private static void Make(string folder, string name, Color color, short count, float size, float speed, bool loop)
        {
            var root = new GameObject(name);
            var ps = root.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            Main(ps, size, speed, loop);
            var emission = ps.emission; emission.rateOverTime = loop ? 2.5f : 0;
            if (!loop) emission.SetBursts(new[] { new ParticleSystem.Burst(0, count) });
            var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = loop ? 0 : 65; shape.radius = loop ? .005f : .10f;
            root.transform.rotation = Quaternion.Euler(loop ? 90 : -90, 0, 0);
            var renderer = root.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Mesh;
            renderer.mesh = Chunk(folder, name);
            renderer.sharedMaterial = Material(folder, name, color);
            PrefabUtility.SaveAsPrefabAsset(root, folder + "/" + name + ".prefab");
            Object.DestroyImmediate(root);
        }

        private static void Main(ParticleSystem ps, float size, float speed, bool loop)
        {
            var main = ps.main;
            main.duration = loop ? 2 : 1;
            main.loop = loop; main.playOnAwake = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(.4f, 1.1f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed * .5f, speed);
            main.startSize = new ParticleSystem.MinMaxCurve(size * .5f, size);
            main.gravityModifier = 1; main.maxParticles = 80;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var rotation = ps.rotationOverLifetime; rotation.enabled = true;
            rotation.z = new ParticleSystem.MinMaxCurve(-5f, 5f);
        }

        private static Mesh Chunk(string folder, string name)
        {
            string path = folder + "/" + name + "_chunk.asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh != null) return mesh;
            mesh = new Mesh { name = name + "_chunk" };
            mesh.vertices = new[] { new Vector3(0,.7f,0), new Vector3(-.4f,0,-.3f),
                new Vector3(.5f,0,-.2f), new Vector3(.1f,0,.4f), new Vector3(0,-.5f,0) };
            mesh.triangles = new[] {0,2,1,0,3,2,0,1,3,4,1,2,4,2,3,4,3,1};
            mesh.RecalculateNormals(); AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }

        private static Material Material(string folder, string name, Color color)
        {
            string path = folder + "/" + name + "_particle.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null) { mat = new Material(Shader.Find("Standard")); AssetDatabase.CreateAsset(mat, path); }
            mat.color = color; mat.SetFloat("_Glossiness", .1f);
            return mat;
        }
    }
}
