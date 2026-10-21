// Portions Copyright (C) 2026 Gerald Glasgow. EliteCreaturesPack 0.7.2 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Changed by expDARE, 2026: Bestiary namespace, owned Crucible settings, rebuilt-only bundles and lifecycle adapters.
// SPDX-License-Identifier: GPL-3.0-or-later
using ExtraPotions.Bestiary.Runtime.Native.Kraken.Motion;
using UnityEngine;

namespace ExtraPotions.Bestiary.Runtime.Native.Kraken
{
    /// <summary>
    /// The ship the kraken holds, as one machine draws the fight round it: its hull, the sea's height in its own space
    /// (smoothed, so the tentacles don't jitter with every wave), where each tentacle rises and the frames tentacle poses
    /// are built in. Everything the kraken draws while it holds a ship is placed in the ship's own space, so it rides the
    /// ship's rolling on every machine exactly as that machine sees the ship.
    /// </summary>
    public class ShipScene
    {
        private const float WaterFollow = 1.5f;

        public Ship? Ship { get; private set; }
        public ShipHull Hull { get; private set; } = null!;

        /// <summary>The sea's surface as the ship's y, smoothed.</summary>
        public float Waterline { get; private set; }

        public Transform Transform => Ship!.transform;

        public void Track(Ship ship, float dt)
        {
            if (ship != Ship)
            {
                Ship = ship;
                Hull = ShipHull.Of(ship);
                Waterline = KrakenShips.Waterline(ship);
                return;
            }
            Waterline = Mathf.Lerp(Waterline, KrakenShips.Waterline(ship), Ease.Follow(WaterFollow, dt));
        }

        public void Forget() => Ship = null;

        public Vector3 World(Vector3 local) => Transform.TransformPoint(local);

        public Vector3 Local(Vector3 world) => Transform.InverseTransformPoint(world);

        /// <summary>
        /// Where tentacle <paramref name="i"/> rises, in the ship's space, at the waterline; <paramref name="along"/>, when
        /// given, moves it along the hull (a tentacle gripping the rail beside the head).
        /// </summary>
        public Vector3 Anchor(int i, float? along = null)
        {
            Vector3 anchor = Hull.Anchor(i);
            anchor.y = Waterline;
            anchor.z = along ?? anchor.z;
            return anchor;
        }

        /// <summary>The frame of tentacle <paramref name="i"/> reaching towards a point in the ship's space.</summary>
        public TentacleFrame Frame(int i, Vector3 towards, float scale, float? along = null)
        {
            Vector3 anchor = Anchor(i, along);
            Vector3 direction = towards - anchor;
            direction.y = 0f;
            return new TentacleFrame(World(anchor), Transform.TransformDirection(direction), Transform.up, scale);
        }

        /// <summary>The frame of tentacle <paramref name="i"/> reaching straight across the ship.</summary>
        public TentacleFrame Across(int i, float scale, float? along = null) =>
            Frame(i, new Vector3(0f, 0f, Anchor(i, along).z), scale, along);

        /// <summary>The ship's top along the line from tentacle <paramref name="i"/> towards a point in the ship's space.</summary>
        public DeckProfile Deck(int i, Vector3 towards, float? along = null)
        {
            Vector3 anchor = Anchor(i, along);
            return DeckScan.Along(Ship!, anchor, towards - anchor, Waterline);
        }
    }
}
