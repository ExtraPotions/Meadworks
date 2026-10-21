using System.Collections.Generic;
using ExtraPotions.Mantle.Embedded.BundlePrefabs;
using MagicaCloth2;
using UnityEngine;

namespace Mantle.Backpacks
{
    /// <summary>
    /// Keeps a cape from flapping out through the worn backpack. The game's capes are MagicaCloth meshes hanging from the
    /// shoulders; at rest their upper part lies inside the pack's volume, but running swings it backwards, out through the
    /// pack. While a pack hangs, every cloth of the wearer's cape gets MagicaCloth's max distance: the part the pack covers
    /// and <see cref="Below"/> more may stray only <see cref="Hold"/> from where the body's pose puts it, and further down the
    /// allowed distance grows with the length of cape hanging below that, so the hem still swings. The cape's own
    /// settings come back when the pack comes down (taken off, hidden) or the cape changes. One per wearer, on the pack's
    /// mount, on every client: only the look changes.
    /// </summary>
    public sealed class CapeHold : MonoBehaviour
    {
        /// <summary>Spine2 at rest in the player's frame (feet at 0): the packs' origin.</summary>
        private const float SpineHeight = 1.4524f;

        /// <summary>The game's capes in the player's frame: tops at 1.69 to 1.72, hems at 0.0 to 0.21 (their meshes).</summary>
        private const float CapeTop = 1.70f;

        private const float CapeLength = 1.58f;

        /// <summary>Metres the covered part may stray from the body's pose; a pack reaches 7 to 18 cm behind a cape at rest.</summary>
        private const float Hold = 0.05f;

        /// <summary>Metres of cape below the pack's bottom edge that are held as well (the user asked for it lower down).</summary>
        private const float Below = 0.35f;

        /// <summary>Metres of stray per metre of cape below the pack: room for a swing of about 95 degrees.</summary>
        private const float Swing = 1.5f;

        /// <summary>MagicaCloth reads a curve at 16 even steps of depth squared.</summary>
        private const int Samples = 16;

        private struct Held
        {
            public MagicaCloth Cloth;
            public bool Used;
            public CurveSerializeData Limit;
        }

        private readonly List<Held> held = new List<Held>();
        private CurveSerializeData limit;
        private List<GameObject> capes;

        /// <summary>
        /// A newly hung pack (its model already under the mount) starts holding the cape worn now, down to the pack's
        /// lowest point below Spine2 (the mount's upright frame is in metres).
        /// </summary>
        internal static void Attach(GameObject mount, List<GameObject> capes)
        {
            CapeHold hold = mount.AddComponent<CapeHold>();
            hold.limit = Limit(ModelBounds.In(mount, mount.transform).min.y);
            hold.HoldAll(capes);
        }

        /// <summary>Every frame while the pack hangs: a cape put on, changed or taken off since (the game makes a new list).</summary>
        internal static void Follow(Transform mount, List<GameObject> capes)
        {
            CapeHold hold = mount != null ? mount.GetComponent<CapeHold>() : null;
            if (hold == null || ReferenceEquals(capes, hold.capes))
                return;
            hold.Release();
            hold.HoldAll(capes);
        }

        /// <summary>The pack comes down: the cape gets its own settings back.</summary>
        internal static void Let(Transform mount)
        {
            CapeHold hold = mount.GetComponent<CapeHold>();
            if (hold != null)
                hold.Release();
        }

        private void HoldAll(List<GameObject> capes)
        {
            this.capes = capes;
            if (capes == null)
                return;
            foreach (GameObject cape in capes)
            {
                if (cape == null)
                    continue;
                foreach (MagicaCloth cloth in cape.GetComponentsInChildren<MagicaCloth>(true))
                    HoldCloth(cloth);
            }
        }

        /// <summary>
        /// Picked up at once by a built cloth; a cloth still building reads it when its build finishes (MagicaCloth syncs
        /// the parameters from the serialized data then).
        /// </summary>
        private void HoldCloth(MagicaCloth cloth)
        {
            MotionConstraint.SerializeData motion = cloth.SerializeData.motionConstraint;
            held.Add(new Held { Cloth = cloth, Used = motion.useMaxDistance, Limit = motion.maxDistance });
            motion.useMaxDistance = true;
            motion.maxDistance = limit.Clone();
            cloth.SetParameterChange();
        }

        /// <summary>A cape the game already replaced is destroyed, and has nothing to get back.</summary>
        private void Release()
        {
            foreach (Held cape in held)
            {
                if (cape.Cloth == null)
                    continue;
                MotionConstraint.SerializeData motion = cape.Cloth.SerializeData.motionConstraint;
                motion.useMaxDistance = cape.Used;
                motion.maxDistance = cape.Limit;
                cape.Cloth.SetParameterChange();
            }
            held.Clear();
            capes = null;
        }

        /// <summary>
        /// The max distance over the cape's depth: MagicaCloth's depth runs from 0 at the fixed top row to 1 at the hem,
        /// along the cloth, and the solver reads the curve at depth squared. Keys at the 16 steps it samples, so what it
        /// reads is exactly <see cref="Metres"/>.
        /// </summary>
        private static CurveSerializeData Limit(float bottom)
        {
            float covered = Mathf.Clamp((CapeTop - SpineHeight - bottom + Below) / CapeLength, 0.05f, 0.9f);
            float most = Metres(1f, covered);
            AnimationCurve curve = new AnimationCurve();
            for (int i = 0; i < Samples; i++)
            {
                float time = i / (Samples - 1f);
                curve.AddKey(new Keyframe(time, Metres(Mathf.Sqrt(time), covered) / most));
            }
            return new CurveSerializeData(most, curve);
        }

        /// <summary>How far a cape point at this depth may stray from the body's pose, with the pack covering down to <paramref name="covered"/>.</summary>
        private static float Metres(float depth, float covered) => Hold + Swing * CapeLength * Mathf.Max(0f, depth - covered);
    }
}
