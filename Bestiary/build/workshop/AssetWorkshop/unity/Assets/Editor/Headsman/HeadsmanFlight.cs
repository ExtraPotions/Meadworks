using UnityEditor;
using UnityEngine;

namespace Workshop.Headsman
{
    /// <summary>
    /// The thrown axe for the preview: a copy of the axe leaving the fists at the throw from exactly where they let go
    /// of it, flying straight at the target at <see cref="Speed"/>, turning about its middle end over end (the hurl) or
    /// flat like a disc (the spin throw), and gone where it hits. The mod will do the same with a projectile.
    /// </summary>
    public sealed class HeadsmanFlight
    {
        public const float Speed = 16f;
        private static readonly Vector3 Middle = new Vector3(-0.143f, 0.75f, 0f);   // the axe's own middle (its bounds)
        private readonly Transform pivot;
        private readonly float scale;
        private Vector3 from, to, axis;
        private Quaternion start;
        private float launched = -1f, duration, turns;

        public HeadsmanFlight(float scale)
        {
            this.scale = scale;
            pivot = new GameObject("ecp_headsman_flying").transform;
            var axe = (GameObject)Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(HeadsmanAxe.Prefab));
            axe.name = "ecp_headsman_flying_axe";
            axe.transform.SetParent(pivot, false);
            axe.transform.localPosition = -Middle;
            pivot.localScale = Vector3.one * scale;
            pivot.gameObject.SetActive(false);
            Renderers = axe.GetComponentsInChildren<Renderer>(true);
        }

        public Renderer[] Renderers { get; }

        /// <summary>Lets go of the axe (its world pose as the fists held it) towards `target`.</summary>
        public void Launch(Transform held, Vector3 target, Flight flight, float now)
        {
            from = held.TransformPoint(Middle);
            to = target;
            start = held.rotation;
            duration = Vector3.Distance(from, to) / Speed;
            Vector3 along = (to - from).normalized;
            // Overhand, the head goes over the top and forward (a forward roll); flat, it keeps turning left as the spin threw it.
            (axis, turns) = flight == Flight.Tumble ? (Vector3.Cross(Vector3.up, along).normalized, 2.2f) : (Vector3.down, 3f);
            launched = now;
            pivot.gameObject.SetActive(true);
            Place(now);
        }

        /// <summary>Moves the axe; true on the frame it arrives (and is gone), with where.</summary>
        public bool Update(float now, out Vector3 hit)
        {
            hit = to;
            if (launched < 0f)
                return false;
            if (now - launched >= duration)
            {
                launched = -1f;
                pivot.gameObject.SetActive(false);
                return true;
            }
            Place(now);
            return false;
        }

        private void Place(float now)
        {
            float since = now - launched;
            pivot.position = Vector3.Lerp(from, to, since / duration);
            pivot.rotation = Quaternion.AngleAxis(turns * 360f * since, axis) * start;
            pivot.localScale = Vector3.one * scale;
        }
    }
}
