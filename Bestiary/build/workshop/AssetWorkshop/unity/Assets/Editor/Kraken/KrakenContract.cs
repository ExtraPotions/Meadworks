using System.Collections.Generic;
using UnityEngine;

namespace Workshop.Kraken
{
    /// <summary>
    /// What Elite Creatures Pack's kraken code relies on, written out independently of the Blender side so the check
    /// catches either drifting: names, parents, rest positions (with how exact each must be), the tentacle's length and
    /// radius profile, material names.
    /// </summary>
    public static class KrakenContract
    {
        public const string Tentacle = "ecp_kraken_tentacle", Head = "ecp_kraken_head";
        public const float Length = 8.0f, BoneLength = 0.5f;
        public const float ColumnBottom = -5.0f, TentacleRootY = -3.5f, TentacleRootRadius = 1.05f;
        public const int TentacleBones = 16;
        public static readonly string[] Inks = { "ecp_kraken_ink_0", "ecp_kraken_ink_1", "ecp_kraken_ink_2", "ecp_kraken_ink_3" };

        /// <summary>The radius profile along the tentacle (fraction of the length, metres).</summary>
        public static readonly (float u, float r)[] Radius =
            { (0f, 0.50f), (0.15f, 0.40f), (0.35f, 0.28f), (0.60f, 0.17f), (0.85f, 0.09f), (1f, 0.035f) };

        public static readonly string[] BodyBones = { "kh_body_1", "kh_body_2", "kh_body_3" };

        public static string TentacleBone(int i) => $"kt_{i:00}";

        /// <summary>(name, parent, position, tolerance in metres); parent "" is the prefab root.</summary>
        public static IEnumerable<(string name, string parent, Vector3 position, float tolerance)> TentacleJoints()
        {
            for (int i = 0; i < TentacleBones; i++)
                yield return (TentacleBone(i), i == 0 ? "" : TentacleBone(i - 1), new Vector3(0, 0, BoneLength * i), 0.001f);
            yield return ("kt_end", TentacleBone(TentacleBones - 1), new Vector3(0, 0, Length), 0.001f);
        }

        /// <summary>The head's joints. The column's spine is kh_body_1..3; the six tentacle markers ride kh_body_3, round
        /// the column at y -3.5.</summary>
        public static IEnumerable<(string name, string parent, Vector3 position, float tolerance)> HeadJoints()
        {
            yield return ("kh_root", "", Vector3.zero, 0.001f);
            yield return ("kh_neck", "kh_root", new Vector3(0, 0.8f, 0), 0.001f);
            yield return ("kh_mantle", "kh_neck", new Vector3(0, 2.6f, -0.4f), 0.05f);
            yield return ("kh_beak_upper", "kh_neck", new Vector3(0, 1.25f, 1.0f), 0.05f);
            yield return ("kh_beak_lower", "kh_neck", new Vector3(0, 1.25f, 1.0f), 0.05f);
            yield return ("kh_siphon", "kh_neck", new Vector3(0.9f, 1.5f, 1.0f), 0.45f);
            yield return ("kh_eye_l", "kh_neck", new Vector3(-1.2f, 2.2f, 0.55f), 0.3f);
            yield return ("kh_eye_r", "kh_neck", new Vector3(1.2f, 2.2f, 0.55f), 0.3f);
            yield return ("kh_body_1", "kh_root", new Vector3(0, -1.0f, 0), 0.001f);
            yield return ("kh_body_2", "kh_body_1", new Vector3(0, -2.4f, 0), 0.001f);
            yield return ("kh_body_3", "kh_body_2", new Vector3(0, -3.8f, 0), 0.001f);
            yield return ("kh_mouth", null, new Vector3(0, 1.25f, 1.5f), 0.3f);
            yield return ("kh_ink", null, new Vector3(0.95f, 1.5f, 1.3f), 0.3f);
            int[] yaws = { 60, 110, 160, 200, 250, 300 };
            for (int k = 0; k < yaws.Length; k++)
            {
                float a = yaws[k] * Mathf.Deg2Rad;
                var at = new Vector3(TentacleRootRadius * Mathf.Sin(a), TentacleRootY, TentacleRootRadius * Mathf.Cos(a));
                yield return ($"kh_tentacle_{k}", "kh_body_3", at, 0.06f);
            }
        }

        public static float RadiusAt(float u)
        {
            for (int i = 1; i < Radius.Length; i++)
                if (u <= Radius[i].u)
                    return Mathf.Lerp(Radius[i - 1].r, Radius[i].r, Mathf.InverseLerp(Radius[i - 1].u, Radius[i].u, u));
            return Radius[Radius.Length - 1].r;
        }
    }
}
