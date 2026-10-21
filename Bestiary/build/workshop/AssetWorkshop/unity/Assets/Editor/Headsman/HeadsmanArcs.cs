using UnityEngine;

namespace Workshop.Headsman
{
    /// <summary>
    /// Axe keys by the shape of the swing rather than by numbers, all in the turned body's frame (x right, y up, z
    /// forward). Angles are degrees.
    /// </summary>
    public static class HeadsmanArcs
    {
        /// <summary>
        /// A chop in the body's middle plane: the right fist `radius` from `pivot` at `hands` (0 straight up, 90 straight
        /// ahead, -90 straight behind), the haft at `haft` on the same dial. The edge faces the way the swing goes:
        /// towards larger angles (forward and down) or, `backward`, towards smaller ones (over the head to behind).
        /// </summary>
        public static AxeKey Chop(Vector3 pivot, float radius, float hands, float haft, bool backward = false)
        {
            Vector3 grip = pivot + radius * Dial(hands);
            Vector3 edge = new Vector3(0f, -Sin(haft), Cos(haft)) * (backward ? -1f : 1f);
            return new AxeKey(grip, Dial(haft), edge);
        }

        /// <summary>
        /// Held out flat round the body at `angle` (0 ahead, negative to the left), the right fist `reach` out at
        /// `height`, the haft dipping `droop` below level, the edge leading a turn to the left.
        /// </summary>
        public static AxeKey Level(float angle, float height, float reach, float droop)
        {
            Vector3 outward = new Vector3(Sin(angle), 0f, Cos(angle));
            Vector3 haft = outward * Cos(droop) + Vector3.down * Sin(droop);
            Vector3 lead = new Vector3(-Cos(angle), 0f, Sin(angle));
            return new AxeKey(new Vector3(0f, height, 0f) + outward * reach, haft, lead);
        }

        /// <summary>The axe turned by `haft` and `edge` and moved so the middle of its cutting edge is at `point`.</summary>
        public static AxeKey Touch(Vector3 point, Vector3 haft, Vector3 edge)
        {
            var key = new AxeKey(Vector3.zero, haft, edge);
            Pose axe = HeadsmanGrip.Axe(key);
            Vector3 at = axe.position + axe.rotation * HeadsmanAxe.Edge;
            return new AxeKey(point - at, key.Haft, key.Edge);
        }

        /// <summary>Where the middle of the edge is for a key (the ground strikes are checked with it).</summary>
        public static Vector3 EdgeOf(AxeKey key)
        {
            Pose axe = HeadsmanGrip.Axe(key);
            return axe.position + axe.rotation * HeadsmanAxe.Edge;
        }

        /// <summary>A point on the ground (or at `height`) `distance` out at `angle` round the body.</summary>
        public static Vector3 Round(float angle, float distance, float height = 0f) =>
            new Vector3(Sin(angle) * distance, height, Cos(angle) * distance);

        private static Vector3 Dial(float degrees) => new Vector3(0f, Cos(degrees), Sin(degrees));

        private static float Sin(float degrees) => Mathf.Sin(degrees * Mathf.Deg2Rad);

        private static float Cos(float degrees) => Mathf.Cos(degrees * Mathf.Deg2Rad);
    }
}
