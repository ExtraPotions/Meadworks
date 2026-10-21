// Portions Copyright (C) 2026 Gerald Glasgow. EliteCreaturesPack 0.7.2 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Changed by expDARE, 2026: Bestiary namespace, owned Crucible settings, rebuilt-only bundles and lifecycle adapters.
// SPDX-License-Identifier: GPL-3.0-or-later
using System.Collections.Generic;
using UnityEngine;

namespace ExtraPotions.Bestiary.Runtime.Native.Kraken
{
    /// <summary>
    /// The kraken's view of ships and the sea: a ship by its ZDO id, the ships in reach that carry a crew, who is aboard,
    /// the height of the waves at a point and the depth of the water there.
    /// </summary>
    public static class KrakenShips
    {
        /// <summary>Metres beside the hull that still count as aboard: someone who just fell over the side is still in the fight.</summary>
        public const float Beside = 2.5f;

        private static readonly List<Player> crew = new List<Player>();
        private static WaterVolume? volume;

        public static Ship? Find(ZDOID id)
        {
            if (id.IsNone() || ZNetScene.instance == null)
            {
                return null;
            }
            GameObject? instance = ZNetScene.instance.FindInstance(id);
            return instance != null ? instance.GetComponent<Ship>() : null;
        }

        /// <summary>The living players aboard or right beside the ship. The list is reused: copy it to keep it.</summary>
        public static List<Player> Crew(Ship ship)
        {
            crew.Clear();
            ShipHull hull = ShipHull.Of(ship);
            foreach (Player player in Player.GetAllPlayers())
            {
                if (player != null && !player.IsDead() && hull.Holds(ship.transform.InverseTransformPoint(player.transform.position), Beside))
                {
                    crew.Add(player);
                }
            }
            return crew;
        }

        /// <summary>The nearest ship within <paramref name="range"/> metres that carries a crew, or null.</summary>
        public static Ship? Nearest(Vector3 from, float range)
        {
            Ship? best = null;
            float bestDistance = range;
            foreach (IMonoUpdater updater in Ship.Instances)
            {
                if (updater is Ship ship && ship != null && Level(ship.transform.position - from) < bestDistance && Crew(ship).Count > 0)
                {
                    best = ship;
                    bestDistance = Level(ship.transform.position - from);
                }
            }
            return best;
        }

        /// <summary>The height of the sea's surface at a point, waves included.</summary>
        public static float Water(Vector3 at)
        {
            float level = Floating.GetWaterLevel(at, ref volume);
            return level > -1000f ? level : ZoneSystem.instance != null ? ZoneSystem.instance.m_waterLevel : 30f;
        }

        /// <summary>Metres of water under a point's surface, down to the sea floor.</summary>
        public static float Depth(Vector3 at) =>
            ZoneSystem.instance == null ? 0f : ZoneSystem.instance.m_waterLevel - ZoneSystem.instance.GetGroundHeight(at);

        /// <summary>Where the sea's surface is in the ship's own space (its y), at the ship.</summary>
        public static float Waterline(Ship ship)
        {
            Vector3 at = ship.transform.position;
            return ship.transform.InverseTransformPoint(new Vector3(at.x, Water(at), at.z)).y;
        }

        public static float Level(Vector3 offset) => new Vector2(offset.x, offset.z).magnitude;
    }
}
