using UnityEngine;

namespace Workshop.Slinger
{
    /// <summary>
    /// Analytic two-bone IK on plain transforms (upper arm, forearm, hand): bends the elbow so the hand reaches the
    /// target, swings the arm onto it, then turns the elbow towards the hint. The pose is read back as muscles
    /// afterwards, so only the resulting rotations matter.
    /// </summary>
    public static class TwoBoneIk
    {
        private const float Epsilon = 1e-6f;

        public static void Solve(Transform upper, Transform lower, Transform end, Vector3 target, Vector3 hint)
        {
            Vector3 a = upper.position, b = lower.position, c = end.position;
            float ab = (b - a).magnitude, bc = (c - b).magnitude;
            float at = Mathf.Clamp((target - a).magnitude, Mathf.Abs(ab - bc) + 0.001f, ab + bc - 0.001f);
            float bend = 0.5f * (Angle((c - a).magnitude, ab, bc) - Angle(at, ab, bc));
            Vector3 axis = Vector3.Cross(b - a, c - b);
            if (axis.sqrMagnitude < Epsilon)
                axis = Vector3.Cross(hint - a, c - b);
            lower.rotation = Quaternion.AngleAxis(2f * bend * Mathf.Rad2Deg, axis.normalized) * lower.rotation;
            upper.rotation = Quaternion.FromToRotation(end.position - a, target - a) * upper.rotation;
            TurnElbow(upper, lower, end, hint);
        }

        /// <summary>Rolls the whole arm about the shoulder-to-hand line until the elbow points at the hint.</summary>
        private static void TurnElbow(Transform upper, Transform lower, Transform end, Vector3 hint)
        {
            Vector3 a = upper.position;
            Vector3 line = (end.position - a).normalized;
            Vector3 elbow = Vector3.ProjectOnPlane(lower.position - a, line);
            Vector3 wanted = Vector3.ProjectOnPlane(hint - a, line);
            if (elbow.sqrMagnitude > Epsilon && wanted.sqrMagnitude > Epsilon)
                upper.rotation = Quaternion.FromToRotation(elbow, wanted) * upper.rotation;
        }

        /// <summary>The angle between sides b and c of a triangle whose third side is a (law of cosines).</summary>
        private static float Angle(float a, float b, float c) =>
            Mathf.Acos(Mathf.Clamp((b * b + c * c - a * a) / (2f * b * c), -1f, 1f));
    }
}
