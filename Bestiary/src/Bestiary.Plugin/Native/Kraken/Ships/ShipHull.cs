// Portions Copyright (C) 2026 Gerald Glasgow. EliteCreaturesPack 0.7.2 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Changed by expDARE, 2026: Bestiary namespace, owned Crucible settings, rebuilt-only bundles and lifecycle adapters.
// SPDX-License-Identifier: GPL-3.0-or-later
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace ExtraPotions.Bestiary.Runtime.Native.Kraken
{
    /// <summary>
    /// A ship's hull in its own space (x right, y up, z forward, from the ship's origin), measured once from its solid
    /// colliders below the mast: how wide and long it is, where its middle is, and how high the rail stands. Any ship
    /// works, whatever its size, the game's or a mod's: the game's own ships keep their hull on the vehicle layer (their
    /// seats, ladders and helm are interaction boxes on other layers and are left out); a ship with nothing on that layer
    /// is measured from all its solid colliders instead. The measure never exceeds the ship's own float box by more than a
    /// little, so a stray collider cannot make a ship look wider than it floats. The same prefab measures the same on
    /// every machine. From it come the six places round the hull where the kraken's tentacles rise: three down each
    /// side, well out from its widest point and spread along its whole length (never closer than a few metres, even
    /// round a raft).
    /// </summary>
    public class ShipHull
    {
        public const int Anchors = 6;
        private const float Tall = 3.5f;      // colliders whose middle is higher than this are the mast and sail
        private const float Clear = 1.8f;     // metres from the hull's widest point to where a tentacle rises
        private const float Spread = 0.8f;    // share of the half length the front and back tentacles stand from the middle
        private const float MinSpread = 3.5f; // metres, at the least, between neighbouring tentacles on a side
        private static readonly float[] AnchorAlong = { 1f, 0f, -1f };
        private static readonly ConditionalWeakTable<Ship, ShipHull> measured = new ConditionalWeakTable<Ship, ShipHull>();

        public float HalfWidth { get; private set; } = 2f;
        public float HalfLength { get; private set; } = 4f;
        public float MiddleZ { get; private set; }
        public float Rail { get; private set; } = 1.5f;

        /// <summary>The layers its hull is on: what a tentacle lies on and what the deck scan reads.</summary>
        public int Solids { get; private set; }

        public static ShipHull Of(Ship ship) => measured.GetValue(ship, Measure);

        /// <summary>Where tentacle <paramref name="i"/> rises, in the ship's space (y left 0: the caller sets the waterline).</summary>
        public Vector3 Anchor(int i)
        {
            float side = i < 3 ? -1f : 1f;
            float spacing = Mathf.Max(HalfLength * Spread, MinSpread);
            return new Vector3(side * (HalfWidth + Clear), 0f, MiddleZ + AnchorAlong[i % 3] * spacing);
        }

        /// <summary>+1 or -1: the side of the ship tentacle <paramref name="i"/> rises on.</summary>
        public static int SideOf(int i) => i < 3 ? -1 : 1;

        /// <summary>Whether a point in the ship's space is aboard or right beside it (within <paramref name="margin"/> metres).</summary>
        public bool Holds(Vector3 local, float margin) =>
            Mathf.Abs(local.x) <= HalfWidth + margin && Mathf.Abs(local.z - MiddleZ) <= HalfLength + margin
            && local.y > -8f && local.y < Rail + 6f;

        private static ShipHull Measure(Ship ship)
        {
            var hull = new ShipHull { Solids = LayerMask.GetMask("vehicle") };
            Bounds? box = Solid(ship, hull.Solids);
            if (box == null)
            {
                hull.Solids = ~LayerMask.GetMask("character", "character_net", "character_ghost", "character_noenv", "hitbox", "character_trigger");
                box = Solid(ship, hull.Solids);
            }
            return box is Bounds found ? hull.Fit(found, Floats(ship)) : hull;
        }

        // Every solid collider on the given layers below the mast, together.
        private static Bounds? Solid(Ship ship, int layers)
        {
            Bounds? box = null;
            foreach (Collider collider in ship.GetComponentsInChildren<Collider>())
            {
                Bounds? local = !collider.isTrigger && (layers & (1 << collider.gameObject.layer)) != 0 ? Local(ship.transform, collider) : null;
                if (local is Bounds b && b.center.y < Tall)
                {
                    box = box is Bounds all ? Union(all, b) : b;
                }
            }
            return box;
        }

        // The game's float box: how big the ship floats.
        private static Bounds? Floats(Ship ship) =>
            ship.m_floatCollider != null ? Local(ship.transform, ship.m_floatCollider) : null;

        private ShipHull Fit(Bounds box, Bounds? floats)
        {
            float width = Mathf.Max(Mathf.Abs(box.min.x), Mathf.Abs(box.max.x), 1f);
            float length = Mathf.Max(box.extents.z, 2f);
            if (floats is Bounds f)
            {
                width = Mathf.Min(width, Mathf.Max(Mathf.Abs(f.min.x), Mathf.Abs(f.max.x)) + 0.5f);
                length = Mathf.Min(length, f.extents.z + 1f);
            }
            (HalfWidth, HalfLength, MiddleZ, Rail) = (width, length, box.center.z, box.max.y);
            return this;
        }

        // A collider's own box turned into the ship's space.
        private static Bounds? Local(Transform ship, Collider collider)
        {
            Bounds own;
            switch (collider)
            {
                case BoxCollider box: own = new Bounds(box.center, box.size); break;
                case MeshCollider mesh when mesh.sharedMesh != null: own = mesh.sharedMesh.bounds; break;
                case CapsuleCollider capsule: own = new Bounds(capsule.center, Vector3.one * capsule.radius * 2f); break;
                case SphereCollider sphere: own = new Bounds(sphere.center, Vector3.one * sphere.radius * 2f); break;
                default: return null;
            }
            return Into(ship, collider.transform, own);
        }

        private static Bounds Into(Transform ship, Transform part, Bounds own)
        {
            var corners = new List<Vector3>(8);
            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = own.center + Vector3.Scale(own.extents, new Vector3((i & 1) * 2 - 1, (i & 2) - 1, (i & 4) / 2 - 1));
                corners.Add(ship.InverseTransformPoint(part.TransformPoint(corner)));
            }
            var bounds = new Bounds(corners[0], Vector3.zero);
            // A bound delegate would mutate a boxed copy of this Bounds struct.
            foreach (Vector3 corner in corners)
            {
                bounds.Encapsulate(corner);
            }
            return bounds;
        }

        private static Bounds Union(Bounds a, Bounds b)
        {
            a.Encapsulate(b);
            return a;
        }
    }
}
