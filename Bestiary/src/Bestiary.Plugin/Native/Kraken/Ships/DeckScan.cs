// Portions Copyright (C) 2026 Gerald Glasgow. EliteCreaturesPack 0.7.2 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Changed by expDARE, 2026: Bestiary namespace, owned Crucible settings, rebuilt-only bundles and lifecycle adapters.
// SPDX-License-Identifier: GPL-3.0-or-later
using ExtraPotions.Bestiary.Runtime.Native.Kraken.Motion;
using UnityEngine;

namespace ExtraPotions.Bestiary.Runtime.Native.Kraken
{
    /// <summary>
    /// Reads the top of a ship along the line a tentacle will strike: straight down onto the ship's own solid parts (its
    /// hull's layers, <see cref="ShipHull.Solids"/>: hull, deck, rails, the cargo chest) every <see cref="Step"/> metres
    /// from the tentacle's base, so the tentacle lies on the ship as it is. Done once per strike on each machine, in the ship's own space, so the
    /// result rides the ship's rolling. Players and loose items are not in the layer and are never read as deck.
    /// </summary>
    public static class DeckScan
    {
        public const float Step = 0.4f;
        private const int Samples = 36;       // 14 metres: past the far side of the widest ship
        private const float Above = 4f;       // metres over the rail the rays start

        private static readonly RaycastHit[] hits = new RaycastHit[16];

        /// <summary>
        /// Heights above <paramref name="waterline"/> (the ship's y of the sea) along <paramref name="direction"/> (level, in
        /// the ship's space) from <paramref name="anchor"/> (the tentacle's base, in the ship's space).
        /// </summary>
        public static DeckProfile Along(Ship ship, Vector3 anchor, Vector3 direction, float waterline)
        {
            direction.y = 0f;
            direction = direction.sqrMagnitude > 1e-4f ? direction.normalized : Vector3.forward;
            var heights = new float[Samples];
            float top = ShipHull.Of(ship).Rail + Above;
            for (int i = 0; i < Samples; i++)
            {
                Vector3 local = anchor + direction * (i * Step);
                local.y = top;
                heights[i] = Surface(ship, local, top - waterline + 2f) - waterline;
            }
            return new DeckProfile(Step, heights);
        }

        // The highest point of the ship straight under a point in its space (its y), or NaN over open water.
        private static float Surface(Ship ship, Vector3 local, float reach)
        {
            Vector3 from = ship.transform.TransformPoint(local);
            int count = Physics.RaycastNonAlloc(from, -ship.transform.up, hits, reach, ShipHull.Of(ship).Solids, QueryTriggerInteraction.Ignore);
            float best = float.NaN;
            for (int i = 0; i < count; i++)
            {
                if (hits[i].collider.transform.IsChildOf(ship.transform))
                {
                    float y = ship.transform.InverseTransformPoint(hits[i].point).y;
                    best = float.IsNaN(best) ? y : Mathf.Max(best, y);
                }
            }
            return best;
        }
    }
}
