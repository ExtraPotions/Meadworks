using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Workshop.Slinger;

namespace Workshop.RimeGiant
{
    /// <summary>
    /// Measures the Troll for the Blender parts and writes out/rimegiant/fit.json (<see cref="RimeFitData"/>):
    ///   Unity -batchmode -nographics -projectPath unity -executeMethod Workshop.RimeGiant.RimeFit.Run -workshopFit &lt;file&gt;
    /// Each plate is fitted in the idle's first frame: its centre and facing found on the skin, then the skin's height
    /// under a grid over its footprint, so the plate's back can follow the body. The crust's snow line comes from
    /// <see cref="RimeCrustFit"/> on the sleeping troll.
    /// </summary>
    public static class RimeFit
    {
        private const int Grid = 21;           // samples across and along a plate, the middle one on its centre
        private const float Reach = 3f;        // rays start this far out from the skin

        public static void Run()
        {
            int code = 1;
            try
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var troll = RimeReference.Troll();
                var poser = new RimePoser(troll);
                Measure(troll, poser).Write(SlingerStage.Argument("-workshopFit"));
                code = 0;
            }
            catch (Exception e)
            {
                Log.Error("rime fit failed: " + e);
            }
            EditorApplication.Exit(code);
        }

        public static RimeFitData Measure(GameObject troll, RimePoser poser)
        {
            poser.Pose("Idle", 0f);
            var body = new RimeBody(troll);
            var plates = Enumerable.Range(0, RimePlates.Count).Select(i => Plate(troll, body, i)).ToArray();
            return new RimeFitData { plates = plates, crust = RimeCrustFit.Measure(troll, poser) };
        }

        private static PlateFit Plate(GameObject troll, RimeBody body, int index)
        {
            RimePlates.Spec spec = RimePlates.Specs[index];
            Vector3 from = RimeReference.Bone(troll, spec.From).position, to = RimeReference.Bone(troll, spec.To).position;
            Vector3 anchor = Vector3.Lerp(from, to, spec.Along) + spec.Offset;
            Vector3 outward = Facing(body, spec, anchor, spec.Outward.normalized);
            Vector3 centre = Surface(body, spec, anchor, outward);
            Vector3 up = spec.UpAlongLimb ? from - to : Vector3.up;
            var fit = new PlateFit
            {
                index = index, name = RimePlates.Name(index), mount = spec.Mount, position = centre,
                rotation = Quaternion.LookRotation(outward, up), width = spec.Width, height = spec.Height, columns = Grid, rows = Grid,
            };
            fit.depth = Heights(body, spec, fit);
            Log.Info($"plate {index} on {spec.Mount}: centre {centre:F2} facing {outward:F2}, skin depth " +
                     $"{fit.depth.Where(d => d > RimeFitData.Miss).Min():F2}..{fit.depth.Max():F2}, misses {fit.depth.Count(d => d <= RimeFitData.Miss)}");
            return fit;
        }

        /// <summary>The skin's average facing over the middle of the plate's footprint, from rays along the rough direction.</summary>
        private static Vector3 Facing(RimeBody body, RimePlates.Spec spec, Vector3 anchor, Vector3 rough)
        {
            var frame = Quaternion.LookRotation(rough, Mathf.Abs(rough.y) > 0.9f ? Vector3.forward : Vector3.up);
            Vector3 sum = Vector3.zero;
            for (int i = -2; i <= 2; i++)
            {
                for (int j = -2; j <= 2; j++)
                {
                    Vector3 origin = anchor + frame * new Vector3(i * spec.Width * 0.1f, j * spec.Height * 0.1f, Reach);
                    if (body.Raycast(origin, -rough, spec.Region, out _, out Vector3 normal, out _))
                        sum += normal;
                }
            }
            return sum.sqrMagnitude > 0f ? sum.normalized : throw new InvalidOperationException($"plate at {anchor} finds no skin");
        }

        private static Vector3 Surface(RimeBody body, RimePlates.Spec spec, Vector3 anchor, Vector3 outward)
        {
            if (!body.Raycast(anchor + outward * Reach, -outward, spec.Region, out float distance, out _, out _))
                throw new InvalidOperationException($"plate at {anchor} finds no skin along {outward}");
            return anchor + outward * (Reach - distance);
        }

        /// <summary>The skin's height along the plate's +Z under each grid point, bottom row first.</summary>
        private static float[] Heights(RimeBody body, RimePlates.Spec spec, PlateFit fit)
        {
            var depth = new float[fit.columns * fit.rows];
            Vector3 inward = fit.rotation * Vector3.back;
            for (int row = 0; row < fit.rows; row++)
            {
                for (int column = 0; column < fit.columns; column++)
                {
                    float u = (column / (fit.columns - 1f) - 0.5f) * fit.width, v = (row / (fit.rows - 1f) - 0.5f) * fit.height;
                    Vector3 origin = fit.position + fit.rotation * new Vector3(u, v, Reach);
                    bool hit = body.Raycast(origin, inward, spec.Region, out float distance, out _, out _);
                    depth[row * fit.columns + column] = hit && distance < 2f * Reach ? Reach - distance : RimeFitData.Miss;
                }
            }
            return depth;
        }
    }
}
