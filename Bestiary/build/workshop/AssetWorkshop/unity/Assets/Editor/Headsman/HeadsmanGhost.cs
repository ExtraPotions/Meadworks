using UnityEngine;
using Workshop.Crossbow;

namespace Workshop.Headsman
{
    /// <summary>
    /// The new axe forming in the raised hands, for the preview: a ghostly copy of the axe hung from the right fist as
    /// the real one is while <see cref="HeadsmanMove.GhostShown"/>, held small for <see cref="Hold"/>, then growing about
    /// the grip to full size by the time it is solid, and gone when the real axe shows again. Blender draws it pale and
    /// see-through and, as it grows, turns its pieces to their own colour one at a time from the bottom of the haft up
    /// (assets/ecp_headsman/blender_reveal.py).
    /// </summary>
    public sealed class HeadsmanGhost
    {
        /// <summary>Seconds the new axe stays small and ghostly before it grows (blender_reveal.HOLD).</summary>
        public const float Hold = 0.5f;

        private readonly Transform pivot;
        private readonly Vector3 full;

        public HeadsmanGhost(GameObject boss, HeadsmanGrip grip)
        {
            Transform hand = XbowReference.Bone(boss, "RightHand");
            GameObject axe = HeadsmanPlayback.Hang(boss, grip, "ecp_headsman_ghost_axe");
            pivot = new GameObject("ecp_headsman_ghost").transform;
            pivot.position = hand.TransformPoint(grip.Right.position);
            pivot.localScale = Vector3.one;
            pivot.SetParent(hand, true);
            axe.transform.SetParent(pivot, true);
            full = pivot.localScale;
            Material glow = HeadsmanProps.Plain("ghost_axe", new Color(0.72f, 1f, 0.94f), true);
            Renderers = axe.GetComponentsInChildren<Renderer>(true);
            foreach (Renderer renderer in Renderers)
                renderer.sharedMaterial = glow;
            pivot.gameObject.SetActive(false);
        }

        public Renderer[] Renderers { get; }

        public void Update(HeadsmanMove move, float time)
        {
            bool shown = move != null && move.GhostShown(time);
            pivot.gameObject.SetActive(shown);
            if (!shown)
                return;
            float grow = Mathf.SmoothStep(0f, 1f, (time - move.Ghost - Hold) / (move.Solid - move.Ghost - Hold));
            pivot.localScale = full * (0.3f + 0.7f * grow);
        }
    }
}
