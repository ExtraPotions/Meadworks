using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Workshop.Kraken
{
    /// <summary>The tentacle's own checks: along +Z, suckers and the paler skin on -Y, bends with its bones.</summary>
    public static class KrakenTentacleCheck
    {
        public static void Run(GameObject tentacle, string folder, Action<string> fail)
        {
            var skin = tentacle.GetComponentInChildren<SkinnedMeshRenderer>();
            if (skin == null || skin.sharedMesh == null)
                return;
            Vector3[] rest = skin.sharedMesh.vertices;
            Axis(rest, fail);
            Suckers(rest, fail);
            Underside(skin.sharedMesh, folder + "/ecp_kraken_tentacle_albedo.png", fail);
            Bend(tentacle, skin, rest, fail);
        }

        private static void Axis(Vector3[] v, Action<string> fail)
        {
            float minZ = v.Min(p => p.z), maxZ = v.Max(p => p.z);
            float side = v.Max(p => Mathf.Max(Mathf.Abs(p.x), Mathf.Abs(p.y)));
            float length = KrakenContract.Length;
            if (minZ < -0.15f || minZ > 0.05f || maxZ < length - 0.05f || maxZ > length + 0.05f || side > 0.62f)
                fail($"tentacle mesh spans z {minZ:F3}..{maxZ:F3}, sideways {side:F3}: expected z ~0..{length} along +Z, thin");
        }

        /// <summary>
        /// Sucker cups stand out of the skin, which is an ellipse round the axis (its radius r is the top's height; the
        /// underside flattened to 0.8 r): every vertex well outside it must be on the -Y side.
        /// </summary>
        private static void Suckers(Vector3[] v, Action<string> fail)
        {
            var top = new Dictionary<int, float>();
            foreach (var p in v)
            {
                int bin = Mathf.FloorToInt(p.z / 0.1f);
                top[bin] = Mathf.Max(top.TryGetValue(bin, out float y) ? y : 0f, p.y);
            }
            int under = 0, over = 0;
            foreach (var p in v)
            {
                float r = top[Mathf.FloorToInt(p.z / 0.1f)];
                if (p.z < 0.2f || p.z > KrakenContract.Length - 0.2f || r < 0.02f)
                    continue;
                float ry = p.y < 0 ? 0.8f * r : r, rx = 1.04f * r;
                if ((p.x / rx) * (p.x / rx) + (p.y / ry) * (p.y / ry) < 1.08f)
                    continue;
                if (p.y < 0) under++; else over++;
            }
            Log.Info($"tentacle suckers: {under} vertices stand out of the skin on -Y, {over} on +Y");
            if (under < 60 || over > 0)
                fail($"tentacle suckers not on -Y: {under} vertices stand out on -Y, {over} on +Y");
        }

        /// <summary>The baked albedo under the downward-facing skin is paler than under the upward-facing skin.</summary>
        private static void Underside(Mesh mesh, string albedo, Action<string> fail)
        {
            Texture2D texture = KrakenCheck.ReadTexture(albedo);
            Vector3[] v = mesh.vertices, n = mesh.normals;
            Vector2[] uv = mesh.uv;
            float up = 0, down = 0;
            int ups = 0, downs = 0;
            for (int i = 0; i < v.Length; i++)
            {
                if (v[i].z < 0.6f || v[i].z > KrakenContract.Length - 1f)
                    continue;
                float lum = texture.GetPixelBilinear(uv[i].x, uv[i].y).grayscale;
                if (n[i].y > 0.8f) { up += lum; ups++; }
                if (n[i].y < -0.8f) { down += lum; downs++; }
            }
            UnityEngine.Object.DestroyImmediate(texture);
            float ratio = downs > 0 && ups > 0 && up > 0 ? (down / downs) / (up / ups) : 0f;
            Log.Info($"tentacle underside: albedo {ratio:F2}x brighter than the top ({downs} / {ups} vertices)");
            if (ratio < 1.5f)
                fail($"tentacle's -Y side is not paler than its top (ratio {ratio:F2})");
        }

        /// <summary>Curls every joint 10 degrees towards -Y: the tip must follow kt_end and end up well below the base.</summary>
        private static void Bend(GameObject tentacle, SkinnedMeshRenderer skin, Vector3[] rest, Action<string> fail)
        {
            int tip = Enumerable.Range(0, rest.Length).OrderByDescending(i => rest[i].z).First();
            for (int i = 1; i < KrakenContract.TentacleBones; i++)
                KrakenCheck.Find(tentacle, KrakenContract.TentacleBone(i)).localRotation = Quaternion.Euler(10f, 0f, 0f);
            Vector3 end = tentacle.transform.InverseTransformPoint(KrakenCheck.Find(tentacle, "kt_end").position);
            Vector3 baked = KrakenCheck.Baked(skin)[tip];
            for (int i = 1; i < KrakenContract.TentacleBones; i++)
                KrakenCheck.Find(tentacle, KrakenContract.TentacleBone(i)).localRotation = Quaternion.identity;
            Log.Info($"tentacle curled 10 deg per joint: tip vertex at {baked:F3}, kt_end at {end:F3}");
            if (Vector3.Distance(baked, end) > 0.12f || end.y > -3f)
                fail($"tentacle skin does not follow its bones: tip at {baked:F2}, kt_end at {end:F2}");
        }
    }

    /// <summary>The head's own checks: face and beak on +Z, eyes and siphon on their sides, the jaws and neck move the skin.</summary>
    public static class KrakenHeadCheck
    {
        public static void Run(GameObject head, Action<string> fail)
        {
            var skins = head.GetComponentsInChildren<SkinnedMeshRenderer>();
            var skin = skins.FirstOrDefault(s => s.name == "mesh_skin");
            if (skin == null)
                return;
            Facing(head, fail);
            Beak(head, skin, fail);
            Eyes(skins.FirstOrDefault(s => s.name == "mesh_eyes"), fail);
            Neck(head, skin, fail);
            Column(head, skin, fail);
            KrakenBodyCheck.Run(head, skin, fail);
        }

        /// <summary>
        /// The column reaches down to about y -5 and stays put however the head leans: every vertex below y -1 is
        /// where it was with kh_neck at +40 and -40 degrees.
        /// </summary>
        private static void Column(GameObject head, SkinnedMeshRenderer skin, Action<string> fail)
        {
            Vector3[] rest = skin.sharedMesh.vertices;
            float bottom = rest.Min(p => p.y);
            if (Mathf.Abs(bottom - KrakenContract.ColumnBottom) > 0.3f)
                fail($"the column reaches down to y {bottom:F2}, expected about {KrakenContract.ColumnBottom}");
            int[] low = Enumerable.Range(0, rest.Length).Where(i => rest[i].y < -1f).ToArray();
            Transform neck = KrakenCheck.Find(head, "kh_neck");
            float moved = 0f;
            foreach (float angle in new[] { 40f, -40f })
            {
                neck.localRotation = Quaternion.Euler(angle, 0, 0);
                Vector3[] leaned = KrakenCheck.Baked(skin);
                moved = Mathf.Max(moved, low.Max(i => Vector3.Distance(leaned[i], rest[i])));
            }
            neck.localRotation = Quaternion.identity;
            Log.Info($"column: down to y {bottom:F2}; {low.Length} vertices below y -1 moved at most {moved * 1000f:F2} mm with the neck at +/-40 deg");
            if (low.Length == 0 || moved > 0.001f)
                fail($"the column below y -1 must not lean with kh_neck (moved {moved:F3} m)");
        }

        private static Vector3 At(GameObject head, string name)
        {
            Transform t = KrakenCheck.Find(head, name);
            return t == null ? Vector3.zero : head.transform.InverseTransformPoint(t.position);
        }

        private static void Facing(GameObject head, Action<string> fail)
        {
            Vector3 mouth = At(head, "kh_mouth"), neck = At(head, "kh_neck");
            if (mouth.z < 1.2f || mouth.z < neck.z + 1f)
                fail($"kh_mouth at {mouth:F2} is not at the front (+Z)");
            if (At(head, "kh_eye_l").x >= 0 || At(head, "kh_eye_r").x <= 0)
                fail("kh_eye_l must be on -X and kh_eye_r on +X");
            Vector3 ink = At(head, "kh_ink"), siphon = At(head, "kh_siphon");
            if (ink.x < 0.4f || ink.z < siphon.z + 0.3f)
                fail($"the siphon must be on the right (+X) opening forward: base {siphon:F2}, kh_ink {ink:F2}");
        }

        private static int[] Weighted(SkinnedMeshRenderer skin, params string[] bones)
        {
            var ids = bones.Select(b => Array.FindIndex(skin.bones, t => t.name == b)).ToArray();
            BoneWeight[] w = skin.sharedMesh.boneWeights;
            return Enumerable.Range(0, w.Length).Where(i => ids.Contains(w[i].boneIndex0) && w[i].weight0 > 0.5f).ToArray();
        }

        /// <summary>The beak juts forward, and opening the jaws (upper up, lower down about X) opens the mesh.</summary>
        private static void Beak(GameObject head, SkinnedMeshRenderer skin, Action<string> fail)
        {
            Vector3[] rest = skin.sharedMesh.vertices;
            int[] upper = Weighted(skin, "kh_beak_upper"), lower = Weighted(skin, "kh_beak_lower");
            if (upper.Length == 0 || lower.Length == 0 || upper.Concat(lower).Max(i => rest[i].z) < 1.4f)
                fail($"the beak ({upper.Length} + {lower.Length} vertices) does not jut forward to z 1.4 or more");
            if (upper.Length == 0 || lower.Length == 0)
                return;
            int upperTip = upper.OrderByDescending(i => rest[i].z).First(), lowerTip = lower.OrderByDescending(i => rest[i].z).First();
            KrakenCheck.Find(head, "kh_beak_upper").localRotation = Quaternion.Euler(-35f, 0, 0);
            KrakenCheck.Find(head, "kh_beak_lower").localRotation = Quaternion.Euler(35f, 0, 0);
            Vector3[] open = KrakenCheck.Baked(skin);
            KrakenCheck.Find(head, "kh_beak_upper").localRotation = Quaternion.identity;
            KrakenCheck.Find(head, "kh_beak_lower").localRotation = Quaternion.identity;
            float up = open[upperTip].y - rest[upperTip].y, down = rest[lowerTip].y - open[lowerTip].y;
            Log.Info($"beak opened 35 deg each way: upper tip {rest[upperTip]:F2} rose {up:F2} m, lower tip {rest[lowerTip]:F2} dropped {down:F2} m");
            if (up < 0.2f || down < 0.2f)
                fail($"opening the jaws does not open the beak (upper tip +{up:F2}, lower tip -{down:F2})");
        }

        private static void Eyes(SkinnedMeshRenderer eyes, Action<string> fail)
        {
            if (eyes == null)
                return;
            var names = eyes.bones.Select(b => b.name).ToArray();
            bool onEyes = eyes.sharedMesh.boneWeights.All(w => names[w.boneIndex0].StartsWith("kh_eye_") && w.weight0 > 0.99f);
            if (!onEyes)
                fail("the eyeballs must follow kh_eye_l / kh_eye_r");
        }

        /// <summary>Leaning the neck forward 20 degrees takes the mantle's top forward.</summary>
        private static void Neck(GameObject head, SkinnedMeshRenderer skin, Action<string> fail)
        {
            Vector3[] rest = skin.sharedMesh.vertices;
            int top = Enumerable.Range(0, rest.Length).OrderByDescending(i => rest[i].y).First();
            Transform neck = KrakenCheck.Find(head, "kh_neck");
            neck.localRotation = Quaternion.Euler(20f, 0, 0);
            Vector3 leaned = KrakenCheck.Baked(skin)[top];
            neck.localRotation = Quaternion.identity;
            Log.Info($"neck leaned 20 deg forward: mantle top from {rest[top]:F2} to {leaned:F2}");
            if (leaned.z < rest[top].z + 1f)
                fail("leaning kh_neck forward does not move the head forward");
        }
    }
}
